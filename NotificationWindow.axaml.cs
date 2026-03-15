using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;

namespace ShowWrite
{
    public partial class NotificationWindow : Window
    {
        private System.Timers.Timer? _closeTimer;

        public NotificationWindow()
        {
            InitializeComponent();
        }

        public void ShowNotification(string message, int durationMs = 3000)
        {
            MessageText.Text = message;

            Dispatcher.UIThread.Post(() =>
            {
                var mainWindow = App.MainWindow;
                if (mainWindow != null)
                {
                    var workingArea = mainWindow.Screens.Primary.WorkingArea;
                    var width = workingArea.Width;
                    var height = workingArea.Height;

                    Show(mainWindow);

                    Position = new PixelPoint(
                        (int)(width - Width - 20),
                        (int)(height - Height - 20)
                    );

                    _closeTimer?.Dispose();
                    _closeTimer = new System.Timers.Timer(durationMs);
                    _closeTimer.Elapsed += (s, e) =>
                    {
                        Dispatcher.UIThread.Post(() =>
                        {
                            Close();
                        });
                        _closeTimer?.Dispose();
                    };
                    _closeTimer.AutoReset = false;
                    _closeTimer.Start();
                }
            });
        }

        private void CloseButton_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            _closeTimer?.Dispose();
            Close();
        }

        protected override void OnClosing(WindowClosingEventArgs e)
        {
            _closeTimer?.Dispose();
            base.OnClosing(e);
        }
    }
}
