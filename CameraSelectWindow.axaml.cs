using Avalonia.Controls;
using Avalonia.Controls.Shapes;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Threading;
using OpenCvSharp;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ShowWrite;

public partial class CameraSelectWindow : Avalonia.Controls.Window
{
    private readonly CameraService? _cameraService;
    private List<int> _availableCameras = new();
    private int _selectedCameraIndex = -1;

    public event Action<int>? CameraSelected;

    public CameraSelectWindow()
    {
        InitializeComponent();
    }

    public CameraSelectWindow(CameraService cameraService) : this()
    {
        _cameraService = cameraService;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    protected override void OnOpened(EventArgs e)
    {
        base.OnOpened(e);
        LoadCameras();
    }

    private async void LoadCameras()
    {
        var cameraList = this.FindControl<StackPanel>("CameraList");
        var noCameraText = this.FindControl<TextBlock>("NoCameraText");

        if (cameraList == null || noCameraText == null) return;

        cameraList.Children.Clear();
        noCameraText.Text = "正在扫描摄像头...";
        noCameraText.IsVisible = true;

        _availableCameras = await Task.Run(() =>
        {
            var cameras = new List<int>();
            for (int i = 0; i < 10; i++)
            {
                try
                {
                    using var test = new VideoCapture(i);
                    if (test.IsOpened())
                        cameras.Add(i);
                }
                catch { }
            }
            return cameras;
        });

        await Dispatcher.UIThread.InvokeAsync(() =>
        {
            cameraList.Children.Clear();

            if (_availableCameras.Count == 0)
            {
                noCameraText.Text = "未检测到摄像头";
                noCameraText.IsVisible = true;
                return;
            }

            noCameraText.IsVisible = false;

            int currentIndex = _cameraService?.CurrentCameraIndex ?? 0;

            for (int i = 0; i < _availableCameras.Count; i++)
            {
                var cameraIndex = _availableCameras[i];
                var btn = new Button
                {
                    Classes = { "camera-btn" },
                    Tag = cameraIndex,
                    Content = new StackPanel
                    {
                        Orientation = Orientation.Horizontal,
                        Spacing = 12,
                        Children =
                        {
                            new Viewbox
                            {
                                Width = 24,
                                Height = 24,
                                Child = new Path
                                {
                                    Fill = Brushes.White,
                                    Data = Geometry.Parse("M16 7a1 1 0 1 0 0 2h1a1 1 0 1 0 0-2zm-4 0a1 1 0 1 0 0 2h1a1 1 0 1 0 0-2zM7 8a1 1 0 0 1 1-1h1a1 1 0 1 1 0 2H8a1 1 0 0 1-1-1M5 4a3 3 0 0 0-3 3v6a3 3 0 0 0 3 3h11a3 3 0 0 0 3-3V7a3 3 0 0 0-3-3zm0 2h11a1 1 0 0 1 1 1v6a1 1 0 0 1-1 1H5a1 1 0 0 1-1-1V7a1 1 0 0 1 1-1m6.5 11a.5.5 0 0 0-.5.5v1a.5.5 0 0 0 .5.5h3a.5.5 0 0 0 .5-.5v-1a.5.5 0 0 0-.5-.5z")
                                }
                            },
                            new TextBlock
                            {
                                Text = $"摄像头 {cameraIndex}",
                                FontSize = 14,
                                Foreground = Brushes.White,
                                VerticalAlignment = VerticalAlignment.Center
                            },
                            new TextBlock
                            {
                                Text = cameraIndex == currentIndex ? "(当前)" : "",
                                FontSize = 12,
                                Foreground = Brush.Parse("#0078D4"),
                                VerticalAlignment = VerticalAlignment.Center
                            }
                        }
                    }
                };

                btn.Click += (s, e) =>
                {
                    if (s is Button button && button.Tag is int idx)
                    {
                        _selectedCameraIndex = idx;
                        CameraSelected?.Invoke(idx);
                        Close();
                    }
                };

                cameraList.Children.Add(btn);
            }
        });
    }

    private void RefreshBtn_Click(object? sender, RoutedEventArgs e)
    {
        LoadCameras();
    }

    private void CancelBtn_Click(object? sender, RoutedEventArgs e)
    {
        Close();
    }
}
