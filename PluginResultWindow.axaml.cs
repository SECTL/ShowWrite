using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using System;

namespace ShowWrite
{
    public partial class PluginResultWindow : Window
    {
        public PluginResultWindow()
        {
            InitializeComponent();
        }

        public void SetResult(string result, bool isUrl)
        {
            ResultText.Text = result;
            TypeText.Text = isUrl ? "类型: 链接" : "类型: 文本";
        }

        public void AddButton(string text, Action onClick)
        {
            var button = new Button
            {
                Content = text,
                Background = new SolidColorBrush(Color.FromRgb(0, 120, 212)),
                Foreground = Brushes.White,
                Padding = new Thickness(16, 8),
                CornerRadius = new CornerRadius(6),
                HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
                IsEnabled = true,
                IsVisible = true,
                Focusable = true
            };

            button.Click += (s, e) =>
            {
                onClick();
                Close();
            };

            ButtonPanel.Children.Add(button);
        }

        public void ClearButtons()
        {
            ButtonPanel.Children.Clear();
        }
    }
}
