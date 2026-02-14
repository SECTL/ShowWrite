using ShowWrite.Models;
using ShowWrite.Services;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;

namespace ShowWrite
{
    public partial class SettingsWindow : Window
    {
        private readonly AppConfig _config;
        private readonly List<string> _cameras;
        private readonly LanguageManager _languageManager;

        public SettingsWindow(AppConfig config, List<string> cameras)
        {
            InitializeComponent();
            _config = config ?? new AppConfig();
            _cameras = cameras ?? new List<string>();
            _languageManager = LanguageManager.Instance;
            Loaded += OnLoaded;
            _languageManager.LanguageChanged += UpdateLanguage;

            // 设置默认选中的导航项
            NavView.SelectedItem = NavView.MenuItems.OfType<UIElement>().FirstOrDefault();
        }

        private void UpdateLanguage()
        {
            // 更新窗口标题
            Title = _languageManager.GetTranslation("Settings");

            // 更新导航项
            foreach (var item in NavView.MenuItems.OfType<iNKORE.UI.WPF.Modern.Controls.NavigationViewItem>())
            {
                if (item.Tag?.ToString() == "General")
                    item.Content = _languageManager.GetTranslation("GeneralSettings");
                else if (item.Tag?.ToString() == "Advanced")
                    item.Content = _languageManager.GetTranslation("AdvancedSettings");
                else if (item.Tag?.ToString() == "Startup")
                    item.Content = _languageManager.GetTranslation("StartupSettings");
            }

            // 更新页脚项
            foreach (var item in NavView.FooterMenuItems.OfType<iNKORE.UI.WPF.Modern.Controls.NavigationViewItem>())
            {
                if (item.Tag?.ToString() == "About")
                    item.Content = _languageManager.GetTranslation("About");
            }

            // 更新语言下拉框选项
            UpdateLanguageComboBox();
        }

        private void UpdateLanguageComboBox()
        {
            foreach (ComboBoxItem item in LanguageComboBox.Items)
            {
                if (item.Tag?.ToString() == "0")
                    item.Content = "简体中文";
                else if (item.Tag?.ToString() == "1")
                    item.Content = "繁體中文";
                else if (item.Tag?.ToString() == "2")
                    item.Content = "文言文";
                else if (item.Tag?.ToString() == "3")
                    item.Content = "English";
            }
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            // 设置版本信息
            VersionText.Text = Assembly.GetExecutingAssembly().GetName().Version.ToString();

            // 初始化摄像头列表
            CameraComboBox.ItemsSource = _cameras;

            // 从配置文件加载当前设置
            LoadConfig();
        }

        private void LoadConfig()
        {
            // 语言设置
            foreach (ComboBoxItem item in LanguageComboBox.Items)
            {
                if (item.Tag?.ToString() == ((int)_languageManager.CurrentLanguage).ToString())
                {
                    item.IsSelected = true;
                    break;
                }
            }

            // 界面主题设置
            foreach (ComboBoxItem item in ThemeComboBox.Items)
            {
                if (item.Tag?.ToString() == _config.Theme)
                {
                    item.IsSelected = true;
                    break;
                }
            }

            // 启动设置
            StartMaximizedCheckBox.IsChecked = _config.StartMaximized;
            AutoStartCameraCheckBox.IsChecked = _config.AutoStartCamera;

            // 设置选中的摄像头
            if (_config.CameraIndex >= 0 && _config.CameraIndex < _cameras.Count)
            {
                CameraComboBox.SelectedIndex = _config.CameraIndex;
            }

            // 默认工具设置
            PenWidthSlider.Value = _config.DefaultPenWidth;

            // 设置画笔颜色
            foreach (ComboBoxItem item in PenColorComboBox.Items)
            {
                if (item.Tag?.ToString() == _config.DefaultPenColor)
                {
                    item.IsSelected = true;
                    break;
                }
            }

            // 高级设置
            EnableHardwareAccel.IsChecked = _config.EnableHardwareAcceleration;
            EnableFrameProcessing.IsChecked = _config.EnableFrameProcessing;

            // 帧率限制
            if (_config.FrameRateLimit >= 0 && _config.FrameRateLimit < FrameRateComboBox.Items.Count)
            {
                FrameRateComboBox.SelectedIndex = _config.FrameRateLimit;
            }

            // 开发者模式设置
            DeveloperModeCheckBox.IsChecked = _config.DeveloperMode;

            // 启动图设置
            StartupImageUrlTextBox.Text = _config.StartupImageUrl ?? "";
        }

        private void NavView_SelectionChanged(object sender, iNKORE.UI.WPF.Modern.Controls.NavigationViewSelectionChangedEventArgs e)
        {
            if (e.SelectedItem is iNKORE.UI.WPF.Modern.Controls.NavigationViewItem navItem)
            {
                string tag = navItem.Tag?.ToString() ?? "";

                // 根据选择显示对应的面板
                GeneralPanel.Visibility = tag == "General" ? Visibility.Visible : Visibility.Collapsed;
                AdvancedPanel.Visibility = tag == "Advanced" ? Visibility.Visible : Visibility.Collapsed;
                RUN.Visibility = tag == "Startup" ? Visibility.Visible : Visibility.Collapsed;
                AboutPanel.Visibility = tag == "About" ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            // 保存设置到配置对象
            
            // 语言设置
            if (LanguageComboBox.SelectedItem is ComboBoxItem languageItem)
            {
                if (int.TryParse(languageItem.Tag?.ToString(), out int languageValue))
                {
                    _languageManager.CurrentLanguage = (LanguageType)languageValue;
                }
            }

            // 界面主题
            if (ThemeComboBox.SelectedItem is ComboBoxItem themeItem)
            {
                _config.Theme = themeItem.Tag?.ToString() ?? "Light";
            }

            // 启动设置
            _config.StartMaximized = StartMaximizedCheckBox.IsChecked ?? true;
            _config.AutoStartCamera = AutoStartCameraCheckBox.IsChecked ?? true;
            _config.CameraIndex = CameraComboBox.SelectedIndex;
            _config.DefaultPenWidth = PenWidthSlider.Value;

            // 获取选中的画笔颜色
            if (PenColorComboBox.SelectedItem is ComboBoxItem colorItem)
            {
                _config.DefaultPenColor = colorItem.Tag?.ToString() ?? "#FF0000FF";
            }

            // 高级设置
            _config.EnableHardwareAcceleration = EnableHardwareAccel.IsChecked ?? true;
            _config.EnableFrameProcessing = EnableFrameProcessing.IsChecked ?? false;
            _config.FrameRateLimit = FrameRateComboBox.SelectedIndex;

            // 开发者模式设置
            _config.DeveloperMode = DeveloperModeCheckBox.IsChecked ?? false;

            // 启动图设置
            _config.StartupImageUrl = StartupImageUrlTextBox.Text?.Trim() ?? "";

            DialogResult = true;
            Close();
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }

        private void VisitWebsiteButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // 打开GitHub发布页
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "https://github.com/wwcrdrvf6u/ShowWrite/",
                    UseShellExecute = true // 必须设置为true才能打开URL
                });
            }
            catch (System.ComponentModel.Win32Exception ex)
            {
                // 处理可能的异常（如默认浏览器未设置）
                System.Windows.MessageBox.Show($"无法打开链接: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }
    }
}