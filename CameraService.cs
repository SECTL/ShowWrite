using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using Avalonia.Threading;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Threading;
using System.Threading.Tasks;

namespace ShowWrite
{
    /// <summary>
    /// 摄像头服务类，直接使用 OpenCVSharp 捕获摄像头，提供帧数据和透视变换功能。
    /// </summary>
    public class CameraService : IDisposable
    {
        private VideoCapture? _capture;
        private volatile bool _cancelled = false;
        private readonly object _lock = new();
        private volatile bool _connectCancelled = false;
        // 每次 StartCapture 递增，Task.Run 捕获此代次，
        // 防止旧 Task.Run 在新 StartCapture 重置 _cancelled=false 后误以为未取消，
        // 导致旧 VideoCapture 覆盖新 _capture（资源泄漏 + 摄像头设备冲突）。
        private volatile int _captureGeneration = 0;

        private WriteableBitmap? _frameBitmap;

        // 采集线程独占 OpenCV 对象；帧通过双缓冲发布：
        // 采集线程只写“未发布”的缓冲，UI 线程在 _lock 内从“已发布”的缓冲拷贝，
        // 不存在跨线程同时读写同一 Mat（这正是之前 native 堆损坏的根因）。
        private Thread? _captureThread;
        private Mat? _bgraFront;            // 已发布给 UI 的最新帧（_lock 保护，BGRA）
        private int _uiPostPending;         // 1 = 已有待执行的 UI 呈现任务

        private int _frameWidth;
        private int _frameHeight;

        private readonly object _perspectiveLock = new();

        private int _currentCameraIndex = 0;
        private List<int> _availableCameraIndices = new();
        private Config _config = new();   // 使用全局 Config

        private Mat? _perspectiveMatrix;
        private bool _hasPerspectiveTransform = false;
        private Point2f[]? _sourcePoints;
        private Point2f[]? _destinationPoints;

        // 公共属性
        public WriteableBitmap? FrameBitmap => _frameBitmap;
        public int CameraCount => _availableCameraIndices.Count;
        public int CurrentCameraIndex => _currentCameraIndex;
        public bool HasPerspectiveTransform => _hasPerspectiveTransform;
        public bool IsConnected
        {
            get
            {
                lock (_lock)
                {
                    return _capture != null && !_cancelled;
                }
            }
        }

        /// <summary>
        /// 获取当前显示帧的独立副本（BGRA，已应用透视变换）。调用方用完必须 Dispose。
        /// </summary>
        public Mat? GetProcessedFrame()
        {
            lock (_lock)
            {
                if (_bgraFront == null || _bgraFront.Empty())
                    return null;

                // 已发布缓冲不会被采集线程写入（采集线程只写另一块），锁内 Clone 安全。
                return _bgraFront.Clone();
            }
        }

        /// <summary>
        /// 获取当前显示帧的稳定副本（持锁内 clone）。调用方用完自行 Dispose。
        /// </summary>
        public Mat? GetLatestFrameCopy()
        {
            return GetProcessedFrame();
        }

        /// <summary>
        /// 只读取当前帧尺寸（不访问 Mat 像素）。供高频 UI 调用使用。
        /// </summary>
        public bool TryGetLatestFrameSize(out int width, out int height)
        {
            lock (_lock)
            {
                width = _frameWidth;
                height = _frameHeight;
                return width > 0 && height > 0;
            }
        }

        // 事件
        public event Action<string>? ErrorOccurred;
        public event Action? FrameReady;
        public event Action? CameraStarted;
        public event Action? ScanComplete;
        public event Action? UsingCachedCameras;

        /// <summary>
        /// 检测并连接摄像头（基于缓存配置或扫描）
        /// </summary>
        public void DetectAndConnectCamera()
        {
            _connectCancelled = false;
            _config = Config.Load();   // 使用全局 Config 的静态方法

            if (_config.AvailableCameraIndices.Count > 0)
            {
                _availableCameraIndices = new List<int>(_config.AvailableCameraIndices);
                _currentCameraIndex = _config.CurrentCameraIndex;

                if (_currentCameraIndex >= _availableCameraIndices.Count)
                    _currentCameraIndex = 0;

                if (_availableCameraIndices.Count > 0)
                {
                    var cameraIdx = _availableCameraIndices[_currentCameraIndex];
                    UsingCachedCameras?.Invoke();
                    if (!_connectCancelled)
                    {
                        StartCapture(cameraIdx);
                    }
                    return;
                }
            }

            ScanCameras();
        }

        /// <summary>
        /// 扫描可用摄像头（0-4）
        /// </summary>
        public void ScanCameras()
        {
            Task.Run(() =>
            {
                var foundCameras = new List<int>();

                for (int i = 0; i < 5; i++)
                {
                    try
                    {
                        using var test = new VideoCapture(i, VideoCaptureAPIs.MSMF);
                        if (test.IsOpened())
                            foundCameras.Add(i);
                    }
                    catch { }
                }

                _availableCameraIndices = foundCameras;
                _config.AvailableCameraIndices = foundCameras;
                _config.AvailableCameraNames = GetCameraDeviceNames();
                _config.LastScanTime = DateTime.Now;
                _config.Save();   // 保存到全局配置

                Dispatcher.UIThread.Post(() =>
                {
                    ScanComplete?.Invoke();

                    if (_availableCameraIndices.Count > 0)
                    {
                        _currentCameraIndex = 0;
                        _config.CurrentCameraIndex = 0;
                        _config.Save();
                        if (!_connectCancelled)
                        {
                            StartCapture(_availableCameraIndices[0]);
                        }
                    }
                    else
                    {
                        ErrorOccurred?.Invoke("未检测到摄像头");
                    }
                });
            });
        }

        public List<string> GetAvailableCameraNames()
        {
            var names = new List<string>();
            var deviceNames = GetCameraDeviceNames();

            for (int i = 0; i < _availableCameraIndices.Count; i++)
            {
                int idx = _availableCameraIndices[i];
                if (deviceNames.TryGetValue(idx, out string? deviceName))
                {
                    names.Add(deviceName);
                }
                else
                {
                    names.Add($"摄像头 {idx}");
                }
            }
            return names;
        }

        public Dictionary<int, string> GetCameraDeviceNames()
        {
            var result = new Dictionary<int, string>();
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT * FROM Win32_PnPEntity WHERE PNPClass='Camera' OR PNPClass='Image'");
                int index = 0;
                foreach (ManagementObject obj in searcher.Get())
                {
                    string? name = obj["Name"]?.ToString();
                    if (!string.IsNullOrEmpty(name))
                    {
                        result[index] = name;
                        index++;
                    }
                }
            }
            catch
            {
            }
            return result;
        }

        public int GetCameraIndexByName(string name)
        {
            var deviceNames = GetCameraDeviceNames();
            for (int i = 0; i < _availableCameraIndices.Count; i++)
            {
                int idx = _availableCameraIndices[i];
                if (deviceNames.TryGetValue(idx, out string? deviceName) && deviceName == name)
                    return idx;
                if ($"摄像头 {idx}" == name)
                    return idx;
            }
            return -1;
        }

        /// <summary>
        /// 启动指定索引的摄像头。本方法由 UI 线程调用。摄像头初始化（new VideoCapture + 设置 + 丢弃前几帧）
        /// 下到线程池避免阻塞 UI，完成后 Post 回 UI 线程启动 DispatcherTimer。
        /// capture.Read 在 DispatcherTimer Tick 中调用——所有 OpenCV 调用单线程完成。
        /// 使用 _captureGeneration 代次计数器：新 StartCapture 递增代次使旧 Task.Run 失效，
        /// 避免旧 Task.Run 在 _cancelled 被 StartCapture 重置为 false 后继续执行，
        /// 向 UI Post 过期 VideoCapture 覆盖新 _capture（资源泄漏 + 设备冲突）。
        /// 带重试打开：从照片模式退出后摄像头驱动可能未立即释放设备，需短暂等待重试。
        /// </summary>
        public void StartCapture(int cameraIndex)
        {
            if (_connectCancelled) return;

            StopCaptureInternal();
            _cancelled = false;
            int generation = ++_captureGeneration;

            Task.Run(() =>
            {
                VideoCapture? capture = null;
                Mat? mat = null;

                try
                {
                    // 重试打开摄像头：从照片模式退出后 VideoCapture.Release 到驱动实际释放设备
                    // 可能有延迟，立即重新打开会因 "设备被占用" 失败。最多重试 5 次，每次间隔 300ms。
                    int maxRetries = 5;
                    for (int attempt = 0; attempt < maxRetries; attempt++)
                    {
                        if (_captureGeneration != generation || _connectCancelled)
                            return;

                        capture = new VideoCapture(cameraIndex, VideoCaptureAPIs.MSMF);
                        if (capture.IsOpened())
                            break;

                        capture.Dispose();
                        capture = null;

                        if (attempt < maxRetries - 1)
                            Thread.Sleep(300);
                    }

                    if (capture == null || !capture.IsOpened())
                    {
                        capture?.Dispose();
                        Dispatcher.UIThread.Post(() =>
                        {
                            ErrorOccurred?.Invoke($"无法打开摄像头 {cameraIndex}");
                        });
                        return;
                    }

                    // 设置分辨率（可选）
                    capture.Set(VideoCaptureProperties.FrameWidth, 1280);
                    capture.Set(VideoCaptureProperties.FrameHeight, 720);
                    capture.Set(VideoCaptureProperties.Fps, 30);

                    var width = (int)capture.Get(VideoCaptureProperties.FrameWidth);
                    var height = (int)capture.Get(VideoCaptureProperties.FrameHeight);

                    mat = new Mat(height, width, MatType.CV_8UC3);

                    // 丢弃前几帧（让摄像头稳定）
                    for (int i = 0; i < 5; i++)
                    {
                        if (_captureGeneration != generation)
                        {
                            capture?.Release();
                            capture?.Dispose();
                            mat?.Dispose();
                            return;
                        }
                        capture.Read(mat);
                    }

                    Dispatcher.UIThread.Post(() =>
                    {
                        lock (_lock)
                        {
                            // 代次不匹配：本次 StartCapture 已被更新的 StartCapture 取代，
                            // 释放本次资源，不覆盖 _capture（避免旧 VideoCapture 覆盖新连接）。
                            if (_captureGeneration != generation)
                            {
                                capture?.Release();
                                capture?.Dispose();
                                mat?.Dispose();
                                return;
                            }

                            _capture = capture;

                            _frameWidth = width;
                            _frameHeight = height;

                            // 仅在尺寸变化或首次创建时重建位图，并释放旧位图。
                            // 旧实现每次 StartCapture 都新建 WriteableBitmap 且不 Dispose，
                            // 导致原生 UnmanagedBlob 泄漏，最终 Lock() 的转码分配抛 OutOfMemoryException。
                            if (_frameBitmap == null
                                || _frameBitmap.PixelSize.Width != width
                                || _frameBitmap.PixelSize.Height != height)
                            {
                                _frameBitmap?.Dispose();
                                // 使用平台原生支持的 Bgra8888 格式，避免 Avalonia 在 Lock
                                // 释放时为非原生格式（Bgr24）分配临时转码缓冲与额外 staging 内存。
                                _frameBitmap = new WriteableBitmap(
                                    new PixelSize(width, height),
                                    new Vector(96, 96),
                                    PixelFormats.Bgra8888,
                                    AlphaFormat.Premul);
                            }

                            // 启动独立采集线程：Read/转码/透视全部在该线程完成，UI 只做整块拷贝。
                            StartCaptureThread(capture!, mat!, generation);

                            CameraStarted?.Invoke();
                        }
                    });
                }
                catch (Exception ex)
                {
                    capture?.Release();
                    capture?.Dispose();
                    mat?.Dispose();

                    Dispatcher.UIThread.Post(() =>
                    {
                        ErrorOccurred?.Invoke($"启动摄像头失败: {ex.Message}");
                    });
                }
            });
        }

        /// <summary>
        /// 启动独立采集线程。OpenCV 对象（VideoCapture/Mat）只在该线程内使用，
        /// 满足“OpenCV 单线程调用”的约束；UI 线程不直接操作采集 Mat。
        /// </summary>
        private void StartCaptureThread(VideoCapture capture, Mat frame, int generation)
        {
            var thread = new Thread(() => CaptureLoop(capture, frame, generation))
            {
                IsBackground = true,
                Name = "ShowWrite.CameraCapture",
                Priority = ThreadPriority.AboveNormal
            };

            _captureThread = thread;
            thread.Start();
        }

        /// <summary>
        /// 采集线程主循环：独占 VideoCapture/Mat，按摄像头节奏持续 Read。
        /// 每帧完成后用双缓冲直接覆盖旧帧，不做排队，因此延迟固定在一帧左右，
        /// 不会像“UI 定时器逐帧读取”那样因消费慢于生产而不断积压。
        /// </summary>
        private void CaptureLoop(VideoCapture capture, Mat frame, int generation)
        {
            var bgraA = new Mat(frame.Height, frame.Width, MatType.CV_8UC4);
            var bgraB = new Mat(frame.Height, frame.Width, MatType.CV_8UC4);
            var writeMat = bgraA;
            var warped = new Mat();

            try
            {
                while (_captureGeneration == generation && !_cancelled)
                {
                    if (!capture.Read(frame) || frame.Empty())
                    {
                        Thread.Sleep(1);
                        continue;
                    }

                    // 透视 + BGR→BGRA 都在采集线程完成，UI 线程不再碰 OpenCV。
                    lock (_perspectiveLock)
                    {
                        Mat source = frame;
                        if (_hasPerspectiveTransform && _perspectiveMatrix != null)
                        {
                            Cv2.WarpPerspective(frame, warped, _perspectiveMatrix, frame.Size());
                            source = warped;
                        }
                        Cv2.CvtColor(source, writeMat, ColorConversionCodes.BGR2BGRA);
                    }

                    // 发布：把刚写好的缓冲换成“已发布”，另一块作为下一次写入目标。
                    lock (_lock)
                    {
                        var previous = _bgraFront;
                        _bgraFront = writeMat;
                        writeMat = previous ?? (ReferenceEquals(writeMat, bgraA) ? bgraB : bgraA);
                        _frameWidth = frame.Width;
                        _frameHeight = frame.Height;
                    }

                    SignalUi(generation);
                }
            }
            catch
            {
                // 设备断开/退出时 Read 可能抛异常，统一由 finally 清理
            }
            finally
            {
                lock (_lock)
                {
                    if (ReferenceEquals(_capture, capture)) _capture = null;
                    _bgraFront = null;
                }

                try { capture.Release(); capture.Dispose(); } catch { }

                bgraA.Dispose();
                bgraB.Dispose();
                warped.Dispose();
                frame.Dispose();
            }
        }

        /// <summary>
        /// 通知 UI 呈现最新帧。用 Interlocked 合并请求：UI 忙时最多只有一个待处理任务，
        /// 不会像每帧 Post 那样堆积，也不会长时间不刷新。
        /// </summary>
        private void SignalUi(int generation)
        {
            if (Interlocked.Exchange(ref _uiPostPending, 1) != 0)
                return;

            Dispatcher.UIThread.Post(() => PresentLatestFrame(generation), DispatcherPriority.Background);
        }

        /// <summary>
        /// UI 线程：把最新 BGRA 帧整块拷贝进 WriteableBitmap 并触发 FrameReady。
        /// 只做 memcpy（约 1ms/720p），不再做阻塞式 Read 和逐像素转换。
        /// </summary>
        private unsafe void PresentLatestFrame(int generation)
        {
            Interlocked.Exchange(ref _uiPostPending, 0);
            if (_captureGeneration != generation || _cancelled)
                return;

            lock (_lock)
            {
                if (_frameBitmap == null || _bgraFront == null || _bgraFront.Empty()
                    || _frameWidth <= 0 || _frameHeight <= 0)
                    return;

                using var locked = _frameBitmap.Lock();
                byte* src = (byte*)_bgraFront.DataPointer;
                byte* dst = (byte*)locked.Address;
                int srcStride = (int)_bgraFront.Step();
                int dstStride = locked.RowBytes;
                int rowBytes = _frameWidth * 4;

                for (int y = 0; y < _frameHeight; y++)
                {
                    Buffer.MemoryCopy(src + y * srcStride, dst + y * dstStride, dstStride, rowBytes);
                }
            }

            // 锁外触发事件：插件回调等耗时不影响采集线程发布下一帧
            FrameReady?.Invoke();
        }

        /// <summary>
        /// 停止捕获。本方法由 UI 线程调用；等待采集线程退出后再清理引用。
        /// </summary>
        public void StopCapture()
        {
            StopCaptureInternal();
        }

        /// <summary>
        /// 取消正在进行的连接
        /// </summary>
        public void CancelConnecting()
        {
            _connectCancelled = true;
            StopCapture();
        }

        /// <summary>
        /// 停止捕获。采集线程可能正阻塞在 capture.Read，最多等待 2 秒；
        /// 超时也不强杀线程（避免在 Read 中途释放原生资源），由线程自身 finally 清理。
        /// </summary>
        private void StopCaptureInternal()
        {
            _cancelled = true;
            // 递增代次：在途 Task.Run 与旧采集线程都会据此退出
            _captureGeneration++;

            var thread = _captureThread;
            _captureThread = null;
            if (thread != null && thread.IsAlive && thread != Thread.CurrentThread)
            {
                thread.Join(2000);
            }

            lock (_lock)
            {
                _capture = null;
                _bgraFront = null;
                _frameWidth = 0;
                _frameHeight = 0;
            }
        }

        /// <summary>
        /// 切换到下一个可用摄像头
        /// </summary>
        public void SwitchCamera()
        {
            if (_availableCameraIndices.Count <= 1)
                return;

            _currentCameraIndex = (_currentCameraIndex + 1) % _availableCameraIndices.Count;
            _config.CurrentCameraIndex = _currentCameraIndex;
            _config.Save();

            StartCapture(_availableCameraIndices[_currentCameraIndex]);
        }

        /// <summary>
        /// 切换到指定索引的摄像头
        /// </summary>
        public void SwitchToCamera(int cameraIndex)
        {
            if (_availableCameraIndices.Count == 0)
                return;

            var pos = _availableCameraIndices.IndexOf(cameraIndex);
            if (pos < 0)
                return;

            _currentCameraIndex = pos;
            _config.CurrentCameraIndex = _currentCameraIndex;
            _config.Save();

            StartCapture(cameraIndex);
        }

        // ---------- 透视变换相关 ----------
        public void SetPerspectiveTransform(Point2f[] sourcePoints, Point2f[] destPoints)
        {
            lock (_perspectiveLock)
            {
                _sourcePoints = sourcePoints;
                _destinationPoints = destPoints;

                if (_sourcePoints != null && _destinationPoints != null &&
                    _sourcePoints.Length == 4 && _destinationPoints.Length == 4)
                {
                    _perspectiveMatrix?.Dispose();
                    _perspectiveMatrix = Cv2.GetPerspectiveTransform(_sourcePoints, _destinationPoints);
                    _hasPerspectiveTransform = true;
                }
                else
                {
                    _perspectiveMatrix?.Dispose();
                    _perspectiveMatrix = null;
                    _hasPerspectiveTransform = false;
                }
            }
        }

        public void ResetPerspectiveTransform()
        {
            lock (_perspectiveLock)
            {
                _perspectiveMatrix?.Dispose();
                _perspectiveMatrix = null;
                _hasPerspectiveTransform = false;
                _sourcePoints = null;
                _destinationPoints = null;
            }
        }

        public Point2f[]? GetSourcePoints()
        {
            return _sourcePoints;
        }

        public Point2f[] GetDefaultSourcePoints(int width, int height)
        {
            var margin = 50;
            return new Point2f[]
            {
                new Point2f(margin, margin),
                new Point2f(width - margin, margin),
                new Point2f(width - margin, height - margin),
                new Point2f(margin, height - margin)
            };
        }

        public Point2f[] GetDefaultDestPoints(int width, int height)
        {
            return new Point2f[]
            {
                new Point2f(0, 0),
                new Point2f(width, 0),
                new Point2f(width, height),
                new Point2f(0, height)
            };
        }

        // ---------- 资源释放 ----------
        public void Dispose()
        {
            StopCapture();
            _frameBitmap?.Dispose();
            _perspectiveMatrix?.Dispose();
        }
    }
}