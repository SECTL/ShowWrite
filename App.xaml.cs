using System;
using System.Threading.Tasks;
using System.Windows;

namespace ShowWrite
{
    public partial class App : System.Windows.Application
    {
        private SplashWindow _splashWindow;
        private MainWindow _mainWindow;

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            // 1. 先显示启动图
            ShowSplashWindow();

            try
            {
                // 2. 异步初始化主窗口
                await InitializeMainWindowAsync();

                // 3. 关闭启动图
                CloseSplashWindow();

                // 4. 显示主窗口
                _mainWindow.Show();
            }
            catch (Exception ex)
            {
                Logger.Error("App", $"启动失败: {ex.Message}", ex);
                System.Windows.MessageBox.Show($"启动失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
            }
        }

        /// <summary>
        /// 显示启动图窗口
        /// </summary>
        private void ShowSplashWindow()
        {
            try
            {
                _splashWindow = new SplashWindow();
                _splashWindow.Show();
                _splashWindow.Activate();
                _splashWindow.Topmost = true;
                _splashWindow.UpdateLayout();
            }
            catch (Exception ex)
            {
                Logger.Error("App", $"显示启动图失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 异步初始化主窗口
        /// </summary>
        private async Task InitializeMainWindowAsync()
        {
            try
            {
                Logger.Debug("App", "开始异步初始化主窗口");

                // 在后台线程创建和初始化主窗口
                await Task.Run(() =>
                {
                    Dispatcher.Invoke(() =>
                    {
                        _mainWindow = new MainWindow();
                        // 注意：这里我们修改了 MainWindow 构造函数，使其不显示启动图
                    });
                });

                Logger.Debug("App", "主窗口初始化完成");
            }
            catch (Exception ex)
            {
                Logger.Error("App", $"初始化主窗口失败: {ex.Message}", ex);
                throw;
            }
        }

        /// <summary>
        /// 关闭启动图窗口
        /// </summary>
        private void CloseSplashWindow()
        {
            try
            {
                if (_splashWindow != null)
                {
                    _splashWindow.CloseSplash();
                    _splashWindow = null;
                }
            }
            catch (Exception ex)
            {
                Logger.Error("App", $"关闭启动图失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 应用程序退出时的处理
        /// </summary>
        protected override void OnExit(ExitEventArgs e)
        {
            try
            {
                // 确保启动图被关闭
                if (_splashWindow != null && _splashWindow.IsLoaded)
                {
                    _splashWindow.Close();
                    _splashWindow = null;
                }
            }
            catch { }

            base.OnExit(e);
        }
    }
}