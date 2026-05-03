using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using ShowWrite;
using ZXing;
using ZXing.Windows.Compatibility;

namespace TestPlugin
{
    public class SamplePlugin : IBottomToolbarPlugin, IPluginWindow
    {
        public string Name => "扫一扫插件";
        public string Version => "1.0.0";
        public string Description => "二维码扫描插件";
        public string Author => "ShowWrite Team";

        private bool _isScanning = false;
        private string? _lastScannedResult = null;
        private Action? _refreshToolbarCallback = null;
        private Action<bool>? _setToolbarVisibilityCallback = null;
        private Action<string, bool, List<(string Text, Action Callback)>>? _showResultWindowCallback = null;
        private Action? _cancelCallback = null;
        private Action<string, int>? _showNotificationCallback = null;
        private Avalonia.Controls.Border? _pluginOverlay = null;
        private BarcodeReader? _barcodeReader = null;

        public void Initialize()
        {
            Console.WriteLine($"{Name} 初始化完成");
            _barcodeReader = new BarcodeReader();
            _barcodeReader.Options = new ZXing.Common.DecodingOptions
            {
                TryHarder = true,
                PossibleFormats = new List<BarcodeFormat> { BarcodeFormat.QR_CODE }
            };
        }

        public void OnLoad()
        {
            Console.WriteLine($"{Name} 已加载");
        }

        public void OnUnload()
        {
            Console.WriteLine($"{Name} 已卸载");
            StopScanning();
        }

        public void SetRefreshToolbarCallback(Action callback)
        {
            _refreshToolbarCallback = callback;
        }

        public void SetPluginOverlay(Avalonia.Controls.Border overlay)
        {
            _pluginOverlay = overlay;
        }

        public void SetToolbarVisibilityCallback(Action<bool> callback)
        {
            _setToolbarVisibilityCallback = callback;
        }

        public void SetShowResultWindowCallback(Action<string, bool, List<(string Text, Action Callback)>>? callback)
        {
            _showResultWindowCallback = callback;
        }

        public void SetCancelCallback(Action callback)
        {
            _cancelCallback = callback;
        }

        public void SetShowNotificationCallback(Action<string, int>? callback)
        {
            _showNotificationCallback = callback;
        }

        public void OnPluginActivated()
        {
            _setToolbarVisibilityCallback?.Invoke(false);
        }

        public void OnPluginDeactivated()
        {
            _setToolbarVisibilityCallback?.Invoke(true);
        }

        public void UpdateToolbarButtons()
        {
            _refreshToolbarCallback?.Invoke();
        }

        public List<PluginToolbarButton> GetToolbarButtons()
        {
            return new List<PluginToolbarButton>
            {
                new PluginToolbarButton
                {
                    IconPath = "M3 5v4h2V5h4V3H5c-1.1 0-2 .9-2 2zm2 10H3v4c0 1.1.9 2 2 2h4v-2H5v-4zm14 4h-4v2h4c1.1 0 2-.9 2-2v-4h-2v4zm0-16h-4v2h4v4h2V5c0-1.1-.9-2-2-2zm-6 8h-2v2h2v-2zm-4 0H7v2h2v-2zm8 0h-2v2h2v-2zm-4-4H9v2h2V9zm0 8H9v2h2v-2z",
                    Label = "扫一扫",
                    Order = 10,
                    IsEnabled = true,
                    OnClick = () => StartScanning()
                }
            };
        }

        private void StartScanning()
        {
            _isScanning = true;
            _lastScannedResult = null;
            Console.WriteLine("开始扫描二维码...");
            
            OnPluginActivated();
        }

        private void StopScanning()
        {
            _isScanning = false;
            _lastScannedResult = null;
            Console.WriteLine("停止扫描二维码...");
            
            OnPluginDeactivated();
        }

        public void OnCameraFrame(Avalonia.Media.Imaging.Bitmap? bitmap)
        {
            if (!_isScanning || bitmap == null || _lastScannedResult != null) return;

            try
            {
                using var stream = new System.IO.MemoryStream();
                bitmap.Save(stream);
                stream.Position = 0;

                var image = new System.Drawing.Bitmap(stream);
                var result = _barcodeReader?.Decode(image);

                if (result != null && !string.IsNullOrEmpty(result.Text))
                {
                    _lastScannedResult = result.Text;
                    Console.WriteLine($"扫描到二维码: {result.Text}");

                    _isScanning = false;
                    OnPluginDeactivated();

                    _showNotificationCallback?.Invoke("扫描成功", 2000);

                    var isUrl = IsUrl(result.Text);
                    var buttons = new List<(string Text, Action Callback)>
                    {
                        ("复制", () => CopyToClipboard(result.Text))
                    };

                    if (isUrl)
                    {
                        buttons.Add(("打开链接", () => OpenUrl(result.Text)));
                    }

                    buttons.Add(("取消", () => { }));

                    _showResultWindowCallback?.Invoke(result.Text, isUrl, buttons);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"扫描二维码失败: {ex.Message}");
            }
        }

        private bool IsUrl(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            return Regex.IsMatch(text, @"^https?://", RegexOptions.IgnoreCase);
        }

        private void CopyToClipboard(string text)
        {
            try
            {
                if (App.MainWindow != null)
                {
                    var clipboard = App.MainWindow.Clipboard;
                    clipboard.SetTextAsync(text).Wait();
                    Console.WriteLine($"已复制到剪贴板: {text}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"复制失败: {ex.Message}");
            }
        }

        private void OpenUrl(string url)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
                Console.WriteLine($"正在打开链接: {url}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"打开链接失败: {ex.Message}");
            }
        }
    }
}
