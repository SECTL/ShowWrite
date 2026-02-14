using ShowWrite.Services;
using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;

namespace ShowWrite
{
    public partial class SplashWindow : Window
    {
        public DateTime? StartTime { get; private set; }

        // 在线图片URL
        private const string OnlineImageUrl = "http://mhhuaji.web1337.net/mhblog/wp-content/uploads/2026/02/b_ccb4a817f77c8cf9a95d1c737964f3b0.jpg";

        private readonly LanguageManager _languageManager;

        public SplashWindow()
        {
            InitializeComponent();
            StartTime = DateTime.Now;

            _languageManager = LanguageManager.Instance;
            _languageManager.LanguageChanged += UpdateLanguage;

            // 设置窗口位置在屏幕中央
            this.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            this.Left = (SystemParameters.PrimaryScreenWidth - this.Width) / 2;
            this.Top = (SystemParameters.PrimaryScreenHeight - this.Height) / 2;

            // 添加淡入动画
            this.Opacity = 0;
            this.Loaded += (s, e) =>
            {
                var fadeIn = new DoubleAnimation
                {
                    From = 0,
                    To = 1,
                    Duration = TimeSpan.FromMilliseconds(500),
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                };
                this.BeginAnimation(OpacityProperty, fadeIn);

                // 窗口加载完成后开始加载图片
                LoadOnlineImage();
            };
        }

        private void UpdateLanguage()
        {
            // 更新窗口标题
            Title = _languageManager.GetTranslation("ShowWrite");

            // 强制刷新所有绑定
            this.Dispatcher.Invoke(() =>
            {
                InvalidateVisual();
                UpdateLayout();
            });
        }

        /// <summary>
        /// 异步加载在线图片
        /// </summary>
        private void LoadOnlineImage()
        {
            try
            {
                Logger.Debug("SplashWindow", "开始加载在线图片");

                // 创建BitmapImage
                var bitmapImage = new BitmapImage();
                bitmapImage.BeginInit();
                bitmapImage.UriSource = new Uri(OnlineImageUrl, UriKind.Absolute);
                bitmapImage.CacheOption = BitmapCacheOption.OnLoad; // 加载时缓存
                bitmapImage.DecodePixelWidth = 438; // 设置解码宽度为右半部分宽度
                bitmapImage.DecodePixelHeight = 475; // 设置解码高度
                bitmapImage.EndInit();

                // 设置图片源
                OnlineImage.Source = bitmapImage;

                // 图片加载完成后的淡入动画
                bitmapImage.DownloadCompleted += (s, e) =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        var imageFadeIn = new DoubleAnimation
                        {
                            From = 0,
                            To = 1,
                            Duration = TimeSpan.FromMilliseconds(800),
                            EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                        };
                        OnlineImage.BeginAnimation(OpacityProperty, imageFadeIn);
                        Logger.Debug("SplashWindow", "在线图片加载完成");
                    });
                };

                // 图片加载失败的处理
                bitmapImage.DownloadFailed += (s, e) =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        Logger.Error("SplashWindow", $"图片加载失败: {e.ErrorException?.Message}", e.ErrorException);
                        // 可以在这里显示默认图片或错误提示
                    });
                };
            }
            catch (Exception ex)
            {
                Logger.Error("SplashWindow", $"加载在线图片失败: {ex.Message}", ex);
            }
        }
        /// <summary>
        /// 关闭启动图（带淡出效果）
        /// </summary>
        public void CloseSplash()
        {
            try
            {
                // 添加淡出效果
                var fadeOut = new DoubleAnimation
                {
                    From = 1.0,
                    To = 0.0,
                    Duration = TimeSpan.FromMilliseconds(300),
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                };

                fadeOut.Completed += (s, e) =>
                {
                    try
                    {
                        this.Close();
                        Logger.Debug("SplashWindow", "启动图已关闭");
                    }
                    catch (Exception ex)
                    {
                        Logger.Error("SplashWindow", $"关闭窗口失败: {ex.Message}", ex);
                    }
                };

                this.BeginAnimation(OpacityProperty, fadeOut);
            }
            catch (Exception ex)
            {
                Logger.Error("SplashWindow", $"关闭启动图失败: {ex.Message}", ex);
                try
                {
                    this.Close();
                }
                catch { }
            }
        }

        protected override void OnSourceInitialized(EventArgs e)
        {
            base.OnSourceInitialized(e);

            // 确保窗口在屏幕中央
            this.Left = (SystemParameters.PrimaryScreenWidth - this.ActualWidth) / 2;
            this.Top = (SystemParameters.PrimaryScreenHeight - this.ActualHeight) / 2;
        }
    }
}