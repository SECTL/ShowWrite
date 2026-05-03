using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Microsoft.Toolkit.Uwp.Notifications;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AvaloniaWindow = Avalonia.Controls.Window;

namespace ShowWrite
{
    public partial class RandomNoteSettingsWindow : AvaloniaWindow
    {
        private readonly CameraService _cameraService;
        private string _currentShortcut;
        private string _currentSavePath;
        private ToggleSwitch? _enableToggleSwitch;
        private ComboBox? _cameraComboBox;
        private ComboBox? _microphoneComboBox;
        private TextBox? _shortcutTextBox;
        private TextBox? _savePathTextBox;
        private NumericUpDown? _durationNumericUpDown;
        private Button? _setShortcutButton;
        private Button? _browseButton;
        private Button? _testMicrophoneButton;
        private StackPanel? _microphoneTestPanel;
        private ProgressBar? _microphoneLevelBar;

        private List<string> _microphoneNames = new List<string>();
        private WaveInEvent? _waveIn;
        private bool _isTestingMicrophone;

        // 托盘相关：从 App 中获取的静态引用
        private static TrayIcon? _trayIcon;
        private static NativeMenuItem? _startMenuItem;
        private static NativeMenuItem? _stopMenuItem;

        // 录制控制
        private static CancellationTokenSource? _currentRecordingCts;
        private static Task? _currentRecordingTask;

        static RandomNoteSettingsWindow()
        {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.Exit += OnApplicationExit;
            }
        }

        public RandomNoteSettingsWindow(CameraService cameraService)
        {
            _cameraService = cameraService ?? throw new ArgumentNullException(nameof(cameraService));
            InitializeComponent();

            // 查找控件
            _enableToggleSwitch = this.FindControl<ToggleSwitch>("EnableToggleSwitch");
            _cameraComboBox = this.FindControl<ComboBox>("CameraComboBox");
            _microphoneComboBox = this.FindControl<ComboBox>("MicrophoneComboBox");
            _shortcutTextBox = this.FindControl<TextBox>("ShortcutTextBox");
            _savePathTextBox = this.FindControl<TextBox>("SavePathTextBox");
            _durationNumericUpDown = this.FindControl<NumericUpDown>("DurationNumericUpDown");
            _setShortcutButton = this.FindControl<Button>("SetShortcutButton");
            _browseButton = this.FindControl<Button>("BrowseButton");
            _testMicrophoneButton = this.FindControl<Button>("TestMicrophoneButton");
            _microphoneTestPanel = this.FindControl<StackPanel>("MicrophoneTestPanel");
            _microphoneLevelBar = this.FindControl<ProgressBar>("MicrophoneLevelBar");

            // 绑定摄像头列表
            if (_cameraComboBox != null)
                _cameraComboBox.ItemsSource = _cameraService.GetAvailableCameraNames();

            // 绑定麦克风列表
            InitializeMicrophoneList();

            // 加载设置
            var config = Config.Load();
            var settings = config.RandomNote;
            if (_enableToggleSwitch != null)
                _enableToggleSwitch.IsChecked = settings.Enabled;
            if (_cameraComboBox != null)
            {
                var cameraNames = _cameraService.GetAvailableCameraNames();
                if (settings.DefaultCameraIndex >= 0 && settings.DefaultCameraIndex < cameraNames.Count)
                    _cameraComboBox.SelectedIndex = settings.DefaultCameraIndex;
                else if (cameraNames.Any())
                    _cameraComboBox.SelectedIndex = 0;
            }

            // 初始化保存路径
            _currentSavePath = settings.SavePath;
            if (string.IsNullOrEmpty(_currentSavePath))
                _currentSavePath = GetDefaultSavePath();
            if (_savePathTextBox != null)
                _savePathTextBox.Text = _currentSavePath;

            // 初始化麦克风选择
            if (_microphoneComboBox != null && settings.MicrophoneIndex >= 0 && settings.MicrophoneIndex < _microphoneNames.Count)
                _microphoneComboBox.SelectedIndex = settings.MicrophoneIndex;

            _currentShortcut = settings.ShortcutKey;
            if (_shortcutTextBox != null)
                _shortcutTextBox.Text = settings.ShortcutKey;
            if (_durationNumericUpDown != null)
                _durationNumericUpDown.Value = settings.RecordingDurationMinutes;

            if (_setShortcutButton != null)
                _setShortcutButton.Click += SetShortcutButton_Click;
            if (_browseButton != null)
                _browseButton.Click += BrowseButton_Click;
            if (_testMicrophoneButton != null)
                _testMicrophoneButton.Click += TestMicrophoneButton_Click;

            // 初始化托盘引用
            InitializeTrayIcon();

            // 根据设置更新托盘状态
            RefreshTrayIconState();
        }

        private void InitializeMicrophoneList()
        {
            _microphoneNames.Clear();
            for (int i = 0; i < WaveInEvent.DeviceCount; i++)
            {
                var capabilities = WaveInEvent.GetCapabilities(i);
                _microphoneNames.Add(capabilities.ProductName);
            }
            if (_microphoneComboBox != null)
                _microphoneComboBox.ItemsSource = _microphoneNames.ToList();
        }

        private static string GetDefaultSavePath()
        {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "ShowWrite", "RandomNote");
        }

        private async void BrowseButton_Click(object? sender, RoutedEventArgs e)
        {
            var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
            {
                Title = "选择保存路径",
                AllowMultiple = false
            });

            if (folders.Count > 0)
            {
                _currentSavePath = folders[0].Path.LocalPath;
                if (_savePathTextBox != null)
                    _savePathTextBox.Text = _currentSavePath;
            }
        }

        private void TestMicrophoneButton_Click(object? sender, RoutedEventArgs e)
        {
            if (_isTestingMicrophone)
            {
                StopMicrophoneTest();
            }
            else
            {
                StartMicrophoneTest();
            }
        }

        private void StartMicrophoneTest()
        {
            if (_microphoneComboBox == null || _microphoneComboBox.SelectedIndex < 0)
                return;

            int deviceIndex = _microphoneComboBox.SelectedIndex;
            try
            {
                _waveIn = new WaveInEvent
                {
                    DeviceNumber = deviceIndex,
                    WaveFormat = new WaveFormat(16000, 1)
                };
                _waveIn.DataAvailable += WaveIn_DataAvailable;
                _waveIn.StartRecording();

                _isTestingMicrophone = true;
                if (_testMicrophoneButton != null)
                    _testMicrophoneButton.Content = "停止";
                if (_microphoneTestPanel != null)
                    _microphoneTestPanel.IsVisible = true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"麦克风测试失败: {ex.Message}");
                ShowNotification("随心记", "无法启动麦克风测试");
            }
        }

        private void StopMicrophoneTest()
        {
            if (_waveIn != null)
            {
                _waveIn.StopRecording();
                _waveIn.DataAvailable -= WaveIn_DataAvailable;
                _waveIn.Dispose();
                _waveIn = null;
            }

            _isTestingMicrophone = false;
            if (_testMicrophoneButton != null)
                _testMicrophoneButton.Content = "测试";
            if (_microphoneTestPanel != null)
                _microphoneTestPanel.IsVisible = false;
            if (_microphoneLevelBar != null)
                _microphoneLevelBar.Value = 0;
        }

        private void WaveIn_DataAvailable(object? sender, WaveInEventArgs e)
        {
            float max = 0;
            for (int i = 0; i < e.BytesRecorded; i += 2)
            {
                short sample = (short)(e.Buffer[i + 1] << 8 | e.Buffer[i]);
                float sample32 = sample / 32768f;
                if (sample32 > max) max = sample32;
            }

            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                if (_microphoneLevelBar != null)
                    _microphoneLevelBar.Value = max * 100;
            });
        }

        private static void InitializeTrayIcon()
        {
            if (_trayIcon != null) return;

            _trayIcon = App.RandomNoteTrayIcon;
            _startMenuItem = App.StartMenuItem;
            _stopMenuItem = App.StopMenuItem;

            if (_trayIcon != null)
            {
                if (_startMenuItem != null)
                    _startMenuItem.Click += (s, e) => StartRecordingFromTray();
                if (_stopMenuItem != null)
                    _stopMenuItem.Click += (s, e) => StopRecording();

                if (_trayIcon.Menu is NativeMenu menu)
                {
                    var settingsItem = menu.Items.OfType<NativeMenuItem>().FirstOrDefault(item => item.Header?.ToString() == "设置");
                    if (settingsItem != null)
                        settingsItem.Click += (s, e) => OpenSettingsWindow();
                }

                _trayIcon.Clicked += (s, e) => OpenSettingsWindow();
            }
        }

        private static void RefreshTrayIconState()
        {
            var settings = Config.Load().RandomNote;
            if (_trayIcon != null)
            {
                _trayIcon.IsVisible = settings.Enabled;
                UpdateTrayMenuState();
            }
        }

        private static void UpdateTrayMenuState()
        {
            bool isRecording = IsRecording();
            if (_startMenuItem != null)
                _startMenuItem.IsEnabled = !isRecording;
            if (_stopMenuItem != null)
                _stopMenuItem.IsEnabled = isRecording;
        }

        public static bool IsRecording() => _currentRecordingCts != null && !_currentRecordingCts.IsCancellationRequested;

        public static async void StartRecordingFromTray(CameraService? cameraService = null, Action<string>? showNotification = null)
        {
            var settings = Config.Load().RandomNote;
            if (!settings.Enabled) return;

            cameraService ??= Application.Current?.Resources["CameraService"] as CameraService;
            if (cameraService == null)
            {
                showNotification?.Invoke("摄像头服务不可用");
                ShowNotification("随心记", "摄像头服务不可用");
                return;
            }

            var cameraNames = cameraService.GetAvailableCameraNames();
            if (cameraNames.Count == 0)
            {
                showNotification?.Invoke("未检测到可用摄像头");
                ShowNotification("随心记", "未检测到可用摄像头");
                return;
            }

            if (settings.DefaultCameraIndex < 0 || settings.DefaultCameraIndex >= cameraNames.Count)
            {
                showNotification?.Invoke("默认摄像头配置无效，请在设置中重新选择");
                ShowNotification("随心记", "默认摄像头配置无效，请在设置中重新选择");
                return;
            }

            var cameraName = cameraNames[settings.DefaultCameraIndex];
            var cameraIndex = cameraService.GetCameraIndexByName(cameraName);
            if (cameraIndex < 0)
            {
                showNotification?.Invoke($"无法找到摄像头 \"{cameraName}\"");
                ShowNotification("随心记", $"无法找到摄像头 \"{cameraName}\"");
                return;
            }

            StopRecording();

            _currentRecordingCts = new CancellationTokenSource();
            var duration = TimeSpan.FromMinutes(settings.RecordingDurationMinutes);
            _currentRecordingTask = Task.Run(() =>
            {
                var recorder = new RandomNoteRecorder();
                recorder.StartRecording(cameraIndex, duration, _currentRecordingCts.Token);
            });

            showNotification?.Invoke($"开始录制，时长 {settings.RecordingDurationMinutes} 分钟");
            ShowNotification("随心记", $"开始录制，时长 {settings.RecordingDurationMinutes} 分钟");
            UpdateTrayMenuState();
        }

        public static void StopRecording()
        {
            if (_currentRecordingCts != null)
            {
                _currentRecordingCts.Cancel();
                try
                {
                    _currentRecordingTask?.Wait(3000);
                }
                catch (AggregateException) { }
                _currentRecordingCts.Dispose();
                _currentRecordingCts = null;
                _currentRecordingTask = null;
                ShowNotification("随心记", "录制已停止");
                UpdateTrayMenuState();
            }
        }

        private static void OpenSettingsWindow()
        {
            var cameraService = Application.Current?.Resources["CameraService"] as CameraService;
            if (cameraService == null) return;

            var window = new RandomNoteSettingsWindow(cameraService);
            window.Show();
        }

        public static void ShowNotification(string title, string message)
        {
            new ToastContentBuilder()
                .AddText(title)
                .AddText(message)
                .Show();
        }

        private async void SetShortcutButton_Click(object? sender, RoutedEventArgs e)
        {
            var dialog = new ShortcutInputDialog();
            var result = await dialog.ShowDialog<string?>(this);
            if (!string.IsNullOrEmpty(result))
            {
                _currentShortcut = result;
                if (_shortcutTextBox != null)
                    _shortcutTextBox.Text = result;
            }
        }

        private void OkButton_Click(object? sender, RoutedEventArgs e)
        {
            StopMicrophoneTest();

            var config = Config.Load();
            config.RandomNote.Enabled = _enableToggleSwitch?.IsChecked == true;
            config.RandomNote.DefaultCameraIndex = _cameraComboBox?.SelectedIndex ?? -1;
            config.RandomNote.MicrophoneIndex = _microphoneComboBox?.SelectedIndex ?? -1;
            config.RandomNote.SavePath = _currentSavePath;
            config.RandomNote.ShortcutKey = _currentShortcut;
            config.RandomNote.RecordingDurationMinutes = (int)(_durationNumericUpDown?.Value ?? 5);
            config.Save();

            RefreshTrayIconState();

            Close();
        }

        private void CancelButton_Click(object? sender, RoutedEventArgs e)
        {
            StopMicrophoneTest();
            Close();
        }

        private static void OnApplicationExit(object? sender, ControlledApplicationLifetimeExitEventArgs e)
        {
            StopRecording();
        }

        public static bool TryTriggerShortcut(string pressedKeyString, CameraService cameraService, Action<string> showNotification)
        {
            var settings = Config.Load().RandomNote;
            if (!settings.Enabled) return false;
            if (string.IsNullOrEmpty(settings.ShortcutKey)) return false;
            if (pressedKeyString != settings.ShortcutKey) return false;

            var cameraNames = cameraService.GetAvailableCameraNames();
            if (cameraNames.Count == 0)
            {
                showNotification("未检测到可用摄像头");
                return false;
            }

            if (settings.DefaultCameraIndex < 0 || settings.DefaultCameraIndex >= cameraNames.Count)
            {
                showNotification("默认摄像头配置无效，请在设置中重新选择");
                return false;
            }

            var cameraName = cameraNames[settings.DefaultCameraIndex];
            var cameraIndex = cameraService.GetCameraIndexByName(cameraName);
            if (cameraIndex < 0)
            {
                showNotification($"无法找到摄像头 \"{cameraName}\"");
                return false;
            }

            StopRecording();

            _currentRecordingCts = new CancellationTokenSource();
            var duration = TimeSpan.FromMinutes(settings.RecordingDurationMinutes);
            _currentRecordingTask = Task.Run(() =>
            {
                var recorder = new RandomNoteRecorder();
                recorder.StartRecording(cameraIndex, duration, _currentRecordingCts.Token);
            });

            showNotification($"开始录制，时长 {settings.RecordingDurationMinutes} 分钟");
            UpdateTrayMenuState();
            return true;
        }
    }

    // 快捷键设置对话框
    public class ShortcutInputDialog : AvaloniaWindow
    {
        private TextBlock _infoText;
        private TextBlock _keyText;
        private string? _result;

        public ShortcutInputDialog()
        {
            Title = "设置快捷键";
            Width = 300;
            Height = 150;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            CanResize = false;

            var stack = new StackPanel { Margin = new Thickness(20), Spacing = 10 };
            _infoText = new TextBlock { Text = "请按下快捷键组合（例如 Alt+Z）" };
            _keyText = new TextBlock { FontSize = 16, Text = "等待按键..." };
            var btnCancel = new Button { Content = "取消", HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center, Width = 80 };
            btnCancel.Click += (s, e) => { _result = null; Close(); };

            stack.Children.Add(_infoText);
            stack.Children.Add(_keyText);
            stack.Children.Add(btnCancel);
            Content = stack;

            KeyDown += OnKeyDown;
        }

        private void OnKeyDown(object? sender, KeyEventArgs e)
        {
            var modifiers = e.KeyModifiers;
            var key = e.Key;
            if (key == Key.Escape)
            {
                _result = null;
                Close();
                return;
            }
            // 排除仅修饰键
            if (key == Key.LeftAlt || key == Key.RightAlt || key == Key.LeftCtrl || key == Key.RightCtrl ||
                key == Key.LeftShift || key == Key.RightShift || key == Key.LWin || key == Key.RWin)
                return;

            var parts = new List<string>();
            if (modifiers.HasFlag(KeyModifiers.Control)) parts.Add("Ctrl");
            if (modifiers.HasFlag(KeyModifiers.Alt)) parts.Add("Alt");
            if (modifiers.HasFlag(KeyModifiers.Shift)) parts.Add("Shift");
            parts.Add(key.ToString());
            _result = string.Join("+", parts);
            _keyText.Text = _result;
            Close();
        }

        public async Task<string?> ShowDialog(AvaloniaWindow parent)
        {
            await ShowDialog(parent);
            return _result;
        }
    }

    // 录制器类（支持取消）
    public class RandomNoteRecorder
    {
        public void StartRecording(int cameraIndex, TimeSpan duration, CancellationToken cancellationToken = default)
        {
            Task.Run(() => DoRecording(cameraIndex, duration, cancellationToken));
        }

        private void DoRecording(int cameraIndex, TimeSpan duration, CancellationToken cancellationToken)
        {
            try
            {
                using var capture = new VideoCapture(cameraIndex, VideoCaptureAPIs.MSMF);
                if (!capture.IsOpened()) return;

                capture.Set(VideoCaptureProperties.FrameWidth, 1280);
                capture.Set(VideoCaptureProperties.FrameHeight, 720);
                int width = (int)capture.Get(VideoCaptureProperties.FrameWidth);
                int height = (int)capture.Get(VideoCaptureProperties.FrameHeight);
                double fps = capture.Get(VideoCaptureProperties.Fps);
                if (fps <= 0) fps = 30;

                string dir = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyVideos), "ShowWrite", "RandomNote");
                Directory.CreateDirectory(dir);
                string fileName = $"随心记_{DateTime.Now:yyyyMMdd_HHmmss}.mp4";
                string filePath = Path.Combine(dir, fileName);

                using var writer = new VideoWriter(filePath, FourCC.MP4V, fps, new OpenCvSharp.Size(width, height));
                if (!writer.IsOpened()) return;

                var frame = new Mat();
                DateTime start = DateTime.Now;
                while ((DateTime.Now - start) < duration)
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    if (!capture.Read(frame) || frame.Empty())
                        break;
                    writer.Write(frame);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"录制失败: {ex.Message}");
            }
        }
    }
}