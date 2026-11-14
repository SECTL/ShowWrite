using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Threading;

namespace ShowWrite
{
    /// <summary>
    /// 单实例控制与进程间通信：同一运行模式下只保留一个实例。
    /// 后启动的实例把待打开的文件通过命名管道转发给已运行实例，并把它的窗口带回前台。
    /// </summary>
    internal static class SingleInstance
    {
        private const string NormalPipeName = "ShowWrite.Normal.SingleInstance.Pipe.v1";
        private const int AsfwAny = -1;

        private static Mutex? _mutex;

        [DllImport("user32.dll")]
        private static extern bool AllowSetForegroundWindow(int dwProcessId);

        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        public sealed class OpenRequest
        {
            public List<string> Files { get; set; } = new();
        }

        /// <summary>
        /// 尝试占用单实例锁。返回 false 表示已有实例在运行（文件已转发给该实例，应退出）。
        /// </summary>
        public static bool TryAcquire(bool randomNoteMode, List<string> filesToOpen)
        {
            var mutexName = randomNoteMode
                ? @"Local\ShowWrite.RandomNote.SingleInstance.v1"
                : @"Local\ShowWrite.Normal.SingleInstance.v1";

            try
            {
                _mutex = new Mutex(true, mutexName, out bool createdNew);
                if (createdNew)
                {
                    // 闪记模式没有可导入文件的主窗口，无需管道服务
                    if (!randomNoteMode)
                        StartPipeServer(NormalPipeName);
                    return true;
                }
            }
            catch
            {
                // 互斥量创建失败时不阻止程序启动
                return true;
            }

            if (!randomNoteMode)
                SendToRunningInstance(NormalPipeName, filesToOpen);

            return false;
        }

        private static void StartPipeServer(string pipeName)
        {
            var thread = new Thread(() => PipeServerLoopAsync(pipeName).GetAwaiter().GetResult())
            {
                IsBackground = true,
                Name = "ShowWrite 单实例通信"
            };
            thread.Start();
        }

        private static async Task PipeServerLoopAsync(string pipeName)
        {
            while (true)
            {
                try
                {
                    using var server = new NamedPipeServerStream(
                        pipeName, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous);

                    await server.WaitForConnectionAsync().ConfigureAwait(false);

                    using var reader = new StreamReader(server, new UTF8Encoding(false));
                    var line = await reader.ReadLineAsync().ConfigureAwait(false);
                    if (!string.IsNullOrEmpty(line))
                    {
                        var request = JsonSerializer.Deserialize<OpenRequest>(line);
                        if (request != null)
                            HandleRequest(request);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[SingleInstance] 管道服务异常: {ex.Message}");
                    await Task.Delay(500).ConfigureAwait(false);
                }
            }
        }

        private static void HandleRequest(OpenRequest request)
        {
            Dispatcher.UIThread.Post(() =>
            {
                try
                {
                    var window = App.MainWindow;
                    if (window == null)
                    {
                        // 主窗口尚未就绪（启动图阶段），先缓存，等主窗口创建后导入
                        Program.AddPendingFiles(request.Files);
                        return;
                    }

                    // 先激活窗口，再把导入排到低优先级，避免激活与导入同时占用 UI 线程造成卡顿
                    ActivateWindow(window);

                    if (request.Files.Count > 0)
                    {
                        var files = request.Files;
                        Dispatcher.UIThread.Post(() =>
                        {
                            try
                            {
                                window.OpenFiles(files);
                            }
                            catch (Exception ex)
                            {
                                Debug.WriteLine($"[SingleInstance] 导入转发文件失败: {ex.Message}");
                            }
                        }, DispatcherPriority.Background);
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[SingleInstance] 处理转发请求失败: {ex.Message}");
                }
            });
        }

        /// <summary>把已运行实例的窗口带回前台。</summary>
        public static void ActivateWindow(Window window)
        {
            if (!window.IsVisible)
                window.Show();

            if (window.WindowState == WindowState.Minimized)
                window.WindowState = WindowState.Normal;

            window.Activate();

            var handle = window.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
            if (handle != IntPtr.Zero)
                SetForegroundWindow(handle);
        }

        private static void SendToRunningInstance(string pipeName, List<string> files)
        {
            try
            {
                // 新进程刚由用户启动，拥有前台权限；转交给已运行实例后再由它抢回前台
                AllowSetForegroundWindow(AsfwAny);

                using var client = new NamedPipeClientStream(".", pipeName, PipeDirection.Out);
                client.Connect(3000);

                using var writer = new StreamWriter(client, new UTF8Encoding(false));
                writer.WriteLine(JsonSerializer.Serialize(new OpenRequest { Files = files }));
                writer.Flush();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SingleInstance] 转发到已运行实例失败: {ex.Message}");
            }
        }
    }
}
