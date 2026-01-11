using System;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace ShowWrite
{
    public partial class SplashWindow : Window
    {
        public DateTime? StartTime { get; private set; }

        public SplashWindow(string imagePath)
        {
            InitializeComponent();
            StartTime = DateTime.Now;
            LoadImage(imagePath);

            // 设置窗口位置在屏幕中央
            this.WindowStartupLocation = WindowStartupLocation.CenterScreen;
        }

        private void LoadImage(string imagePath)
        {
            try
            {
                if (File.Exists(imagePath))
                {
                    // 使用BitmapImage以支持高DPI
                    var bitmapImage = new BitmapImage();
                    bitmapImage.BeginInit();

                    // 设置高DPI相关属性
                    bitmapImage.DecodePixelWidth = 800; // 限制最大宽度
                    bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
                    bitmapImage.CreateOptions = BitmapCreateOptions.IgnoreImageCache;

                    // 支持高DPI缩放
                    bitmapImage.DecodePixelWidth = (int)(SystemParameters.PrimaryScreenWidth * 0.5);

                    // 设置URI
                    bitmapImage.UriSource = new Uri(imagePath, UriKind.RelativeOrAbsolute);

                    bitmapImage.EndInit();

                    // 检查图像是否有效
                    if (bitmapImage.Width > 0 && bitmapImage.Height > 0)
                    {
                        SplashImage.Source = bitmapImage;

                        // 根据图像尺寸调整窗口大小
                        AdjustWindowSize(bitmapImage.PixelWidth, bitmapImage.PixelHeight);
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("SplashWindow", $"加载启动图失败: {ex.Message}", ex);
            }
        }

        private void AdjustWindowSize(int imageWidth, int imageHeight)
        {
            try
            {
                // 获取屏幕尺寸
                double screenWidth = SystemParameters.PrimaryScreenWidth;
                double screenHeight = SystemParameters.PrimaryScreenHeight;

                // 计算最大尺寸（屏幕的80%）
                double maxWidth = screenWidth * 0.8;
                double maxHeight = screenHeight * 0.8;

                // 计算缩放比例
                double widthRatio = maxWidth / imageWidth;
                double heightRatio = maxHeight / imageHeight;
                double scaleRatio = Math.Min(widthRatio, heightRatio);

                // 如果图像比屏幕小，保持原尺寸
                if (scaleRatio > 1)
                {
                    scaleRatio = 1;
                }

                // 设置图像控件尺寸
                SplashImage.Width = imageWidth * scaleRatio;
                SplashImage.Height = imageHeight * scaleRatio;

                // 设置窗口尺寸（包含边框和边距）
                this.Width = SplashImage.Width + 40; // 边距
                this.Height = SplashImage.Height + 60; // 边距和进度条高度

                // 重新居中
                this.WindowStartupLocation = WindowStartupLocation.CenterScreen;
            }
            catch (Exception ex)
            {
                Logger.Error("SplashWindow", $"调整窗口尺寸失败: {ex.Message}", ex);
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