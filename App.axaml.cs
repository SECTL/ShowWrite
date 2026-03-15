using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;

namespace ShowWrite
{
    public partial class App : Application
    {
        public static MainWindow? MainWindow { get; private set; }

        public override void Initialize()
        {
            ThemeManager.InitializeTheme(this);

            AvaloniaXamlLoader.Load(this);

            var config = Config.Load();
            ThemeType themeType = config.Theme switch
            {
                "Light" => ThemeType.Light,
                "LightMinimal" => ThemeType.LightMinimal,
                _ => ThemeType.Dark
            };

            if (themeType != ThemeType.Dark)
            {
                ThemeManager.SetTheme(themeType);
                ThemeManager.ApplyTheme(this, themeType);
            }
        }

        public override void OnFrameworkInitializationCompleted()
        {
            if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
            {
                desktop.MainWindow = new MainWindow();
                MainWindow = desktop.MainWindow as MainWindow;
            }

            base.OnFrameworkInitializationCompleted();
        }
    }
}