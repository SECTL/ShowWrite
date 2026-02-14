using ShowWrite.Services;
using System.Windows;

namespace ShowWrite
{
    public partial class App : System.Windows.Application
    {
        private SplashWindow _splashWindow;
        private MainWindow _mainWindow;

        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            try
            {
                Logger.Info("App", "应用程序启动开始");

                // 0. 加载语言设置
                LoadLanguageSettings();

                // 1. 显示启动图
                ShowSplashWindow();

                // 2. 等待一小段时间让启动图完全显示
                System.Threading.Thread.Sleep(500);

                // 3. 在主线程创建主窗口
                CreateMainWindow();

                // 4. 关闭启动图
                CloseSplashWindow();

                // 5. 显示主窗口
                _mainWindow?.Show();

                Logger.Info("App", "应用程序启动完成");
            }
            catch (Exception ex)
            {
                Logger.Error("App", $"启动失败: {ex.Message}", ex);
                System.Windows.MessageBox.Show($"启动失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                Shutdown();
            }
        }

        /// <summary>
        /// 加载语言设置
        /// </summary>
        private void LoadLanguageSettings()
        {
            try
            {
                Logger.Debug("App", "加载语言设置");

                var configPath = System.IO.Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "config.json");
                if (System.IO.File.Exists(configPath))
                {
                    var json = System.IO.File.ReadAllText(configPath, System.Text.Encoding.UTF8);
                    var config = Newtonsoft.Json.JsonConvert.DeserializeObject<Models.AppConfig>(json);
                    if (config != null)
                    {
                        LanguageManager.Instance.CurrentLanguage = (LanguageType)config.Language;
                        Logger.Info("App", $"语言设置已加载: {LanguageManager.Instance.CurrentLanguage}");
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("App", $"加载语言设置失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 显示启动图窗口
        /// </summary>
        private void ShowSplashWindow()
        {
            try
            {
                Logger.Debug("App", "显示启动图窗口");
                _splashWindow = new SplashWindow();
                _splashWindow.Show();
                _splashWindow.Activate();
                _splashWindow.Topmost = true;
                _splashWindow.UpdateLayout();

                // 确保启动图窗口处理消息
                Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Background);

                Logger.Debug("App", "启动图窗口已显示");
            }
            catch (Exception ex)
            {
                Logger.Error("App", $"显示启动图失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 创建主窗口
        /// </summary>
        private void CreateMainWindow()
        {
            try
            {
                Logger.Debug("App", "创建主窗口开始");

                // 同步创建主窗口，不使用 async
                _mainWindow = new MainWindow();

                Logger.Debug("App", "主窗口创建完成");
            }
            catch (Exception ex)
            {
                Logger.Error("App", $"创建主窗口失败: {ex.Message}", ex);
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
                    Logger.Debug("App", "关闭启动图窗口");

                    // 先隐藏再关闭
                    _splashWindow.Hide();
                    _splashWindow.Close();
                    _splashWindow = null;

                    // 强制垃圾回收
                    GC.Collect();
                    GC.WaitForPendingFinalizers();

                    Logger.Debug("App", "启动图窗口已关闭");
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
                Logger.Info("App", "应用程序退出");

                // 确保启动图被关闭
                if (_splashWindow != null)
                {
                    _splashWindow.Close();
                    _splashWindow = null;
                }

                // 确保主窗口被关闭
                if (_mainWindow != null)
                {
                    _mainWindow.Close();
                    _mainWindow = null;
                }
            }
            catch { }

            base.OnExit(e);
        }
    }
}