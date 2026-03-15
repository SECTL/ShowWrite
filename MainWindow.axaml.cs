using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Shapes;
using Avalonia.Data.Converters;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.LogicalTree;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Styling;
using Avalonia.Threading;
using Avalonia.VisualTree;
using OpenCvSharp;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Path = System.IO.Path;
using Point = Avalonia.Point;
using Rect = Avalonia.Rect;

namespace ShowWrite
{
    public partial class MainWindow : Avalonia.Controls.Window, INotifyPropertyChanged
    {
        private readonly CameraService _cameraService;
        private List<IPluginWindow> _pluginWindows = new();

        private Control[] _loadingElements;
        private CancellationTokenSource? _loadingAnimationCts;
        private bool _isLoadingAnimationRunning;

        private double _zoom = 1.0;
        private Point _panOffset = new Point(0, 0);
        private Point _lastPanPoint;
        private bool _isPanning;

        private int _videoWidth;
        private int _videoHeight;

        private TransformGroup? _videoTransform;
        private ScaleTransform? _scaleTransform;
        private TranslateTransform? _translateTransform;

        public static readonly double MinZoom = 0.1;
        public static readonly double MaxZoom = 5.0;
        public static readonly double ZoomStep = 0.1;

        public ObservableCollection<PhotoItem> Photos { get; } = new();

        private static readonly SKColor[] PenColors = new SKColor[]
        {
            SKColors.Red, SKColors.Orange, SKColors.Yellow, SKColors.Green, SKColors.Cyan,
            SKColors.Blue, SKColors.Purple, SKColors.Magenta, SKColors.Brown, SKColors.Gray,
            SKColors.Black, SKColors.White,
            new SKColor(255, 182, 193), new SKColor(144, 238, 144), new SKColor(173, 216, 230),
            new SKColor(255, 218, 185), new SKColor(221, 160, 221), new SKColor(240, 230, 140),
            new SKColor(255, 99, 71), new SKColor(50, 205, 50), new SKColor(30, 144, 255),
            new SKColor(255, 20, 147), new SKColor(255, 165, 0), new SKColor(0, 206, 209),
            new SKColor(138, 43, 226), new SKColor(255, 105, 180), new SKColor(0, 128, 0),
            new SKColor(0, 0, 139), new SKColor(139, 69, 19), new SKColor(105, 105, 105)
        };

        private static readonly int[] PenSizes = new int[] { 2, 4, 8, 12 };

        private int _currentPenSizeIndex = 1;
        private int _currentPenColorIndex = 0;

        private Popup? _clearSlidePopup;
        private PhotoItem? _selectedPhoto;
        private bool _isPhotoAnnotationMode = false;
        private bool _isSelectMode;
        private bool _includeInkAnnotations = true;

        public bool IsSelectMode
        {
            get => _isSelectMode;
            set
            {
                if (_isSelectMode != value)
                {
                    _isSelectMode = value;
                    PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(IsSelectMode)));
                }
            }
        }

        public bool IncludeInkAnnotations
        {
            get => _includeInkAnnotations;
            set
            {
                if (_includeInkAnnotations != value)
                {
                    _includeInkAnnotations = value;
                    PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(IncludeInkAnnotations)));
                }
            }
        }

        public new event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

        private StackPanel? _normalButtons;
        private StackPanel? _selectModeButtons;
        private Border? _leftButtonGroup;
        private Border? _keystoneButtonGroup;
        private Border? _centerButtonGroup;
        private Border? _rightButtonGroup;
        private Canvas? _keystoneOverlayCanvas;
        private Polygon? _keystonePolygon;
        private Border? _keystonePointTL;
        private Border? _keystonePointTR;
        private Border? _keystonePointBR;
        private Border? _keystonePointBL;

        private Border? _toolSliderBackground;

        private bool _isKeystoneCorrectionMode = false;
        private bool _isDraggingKeystonePoint = false;
        private Border? _draggingKeystonePoint = null;
        private Point _keystonePointOffset;

        private OpenCvSharp.Point2f[] _keystonePoints = new OpenCvSharp.Point2f[4];
        private OpenCvSharp.Point2f[] _originalKeystonePoints = new OpenCvSharp.Point2f[4];

        private bool _wasMinimized = false;

        private WhiteboardManager? _whiteboardManager;

        public ObservableCollection<WhiteboardPageThumbnail> WhiteboardPageThumbnails => _whiteboardManager?.WhiteboardPageThumbnails ?? new ObservableCollection<WhiteboardPageThumbnail>();

        public MainWindow()
        {
            InitializeComponent();
            this.WindowState = WindowState.FullScreen;

            _clearSlidePopup = this.FindControl<Popup>("ClearSlidePopup");
            _normalButtons = this.FindControl<StackPanel>("NormalButtons");
            _selectModeButtons = this.FindControl<StackPanel>("SelectModeButtons");
            _leftButtonGroup = this.FindControl<Border>("LeftButtonGroup");
            _keystoneButtonGroup = this.FindControl<Border>("KeystoneButtonGroup");
            _centerButtonGroup = this.FindControl<Border>("CenterButtonGroup");
            _rightButtonGroup = this.FindControl<Border>("RightButtonGroup");
            _keystoneOverlayCanvas = this.FindControl<Canvas>("KeystoneOverlayCanvas");
            _keystonePolygon = this.FindControl<Polygon>("KeystonePolygon");
            _keystonePointTL = this.FindControl<Border>("KeystonePointTL");
            _keystonePointTR = this.FindControl<Border>("KeystonePointTR");
            _keystonePointBR = this.FindControl<Border>("KeystonePointBR");
            _keystonePointBL = this.FindControl<Border>("KeystonePointBL");
            _toolSliderBackground = this.FindControl<Border>("ToolSliderBackground");

            var whiteboardBackground = this.FindControl<Border>("WhiteboardBackground");
            var whiteboardModeButtons = this.FindControl<StackPanel>("WhiteboardModeButtons");
            var whiteboardPageButtons = this.FindControl<StackPanel>("WhiteboardPageButtons");
            var pagePanel = this.FindControl<Border>("PagePanel");
            var pageInfoText = this.FindControl<TextBlock>("PageInfoText");
            var nextPagePath = this.FindControl<Avalonia.Controls.Shapes.Path>("NextPagePath");
            var nextPageText = this.FindControl<TextBlock>("NextPageText");
            var pageImportingOverlay = this.FindControl<Border>("PageImportingOverlay");

            _whiteboardManager = new WhiteboardManager(InkCanvasOverlay, VideoImage, _cameraService);
            _whiteboardManager.Initialize(
                whiteboardBackground,
                whiteboardModeButtons,
                whiteboardPageButtons,
                pagePanel,
                pageInfoText,
                nextPagePath,
                nextPageText,
                pageImportingOverlay);
            _whiteboardManager.ImportPptRequested += OnImportPptRequested;

            var moreButton = this.FindControl<Button>("MoreButton");
            if (moreButton != null && MoreMenuPopup != null)
            {
                MoreMenuPopup.PlacementTarget = moreButton;
            }

            DataContext = this;

            InitializeKeystonePoints();

            var includeInkCheckBox = this.FindControl<CheckBox>("IncludeInkCheckBox");
            if (includeInkCheckBox != null)
            {
                var isCheckedBinding = new Avalonia.Data.Binding(nameof(IncludeInkAnnotations))
                {
                    Source = this,
                    Mode = Avalonia.Data.BindingMode.TwoWay
                };
                var isVisibleBinding = new Avalonia.Data.Binding(nameof(IsSelectMode))
                {
                    Source = this,
                    Mode = Avalonia.Data.BindingMode.OneWay
                };
                includeInkCheckBox.Bind(CheckBox.IsCheckedProperty, isCheckedBinding);
                includeInkCheckBox.Bind(CheckBox.IsVisibleProperty, isVisibleBinding);
            }

            this.PropertyChanged += (s, e) =>
            {
                if (e.PropertyName == nameof(IsSelectMode))
                {
                    UpdateButtonVisibility();
                }
            };

            ThemeManager.ThemeChanged += OnThemeChanged;
            UpdateButtonGroupStyles();

            ((Avalonia.AvaloniaObject)this).PropertyChanged += (s, e) =>
            {
                if (e.Property == Avalonia.Controls.Window.WindowStateProperty)
                {
                    if (WindowState == WindowState.Minimized)
                    {
                        _wasMinimized = true;
                    }
                    else if (_wasMinimized)
                    {
                        _wasMinimized = false;
                        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                        {
                            WindowState = WindowState.FullScreen;
                        });
                    }
                }
            };

            _scaleTransform = new ScaleTransform(1, 1);
            _translateTransform = new TranslateTransform(0, 0);
            _videoTransform = new TransformGroup();
            _videoTransform.Children.Add(_scaleTransform);
            _videoTransform.Children.Add(_translateTransform);

            VideoImage.RenderTransform = _videoTransform;
            VideoImage.RenderTransformOrigin = new RelativePoint(0, 0, RelativeUnit.Absolute);

            InkCanvasOverlay.SetVideoImage(VideoImage);

            _cameraService = new CameraService();
            _cameraService.ErrorOccurred += OnCameraError;
            _cameraService.FrameReady += OnFrameReady;
            _cameraService.CameraStarted += OnCameraStarted;
            _cameraService.ScanComplete += OnScanComplete;
            _cameraService.UsingCachedCameras += OnUsingCachedCameras;

            VideoAreaContainer.AddHandler(InputElement.PointerWheelChangedEvent, OnPointerWheelChanged, RoutingStrategies.Tunnel);
            VideoAreaContainer.AddHandler(InputElement.PointerPressedEvent, OnCanvasPointerPressed, RoutingStrategies.Tunnel);
            VideoAreaContainer.AddHandler(InputElement.PointerMovedEvent, OnCanvasPointerMoved, RoutingStrategies.Tunnel);
            VideoAreaContainer.AddHandler(InputElement.PointerReleasedEvent, OnCanvasPointerReleased, RoutingStrategies.Tunnel);

            InkCanvasOverlay.AddHandler(InputElement.PointerPressedEvent, OnCanvasPointerPressed, RoutingStrategies.Tunnel);
            InkCanvasOverlay.AddHandler(InputElement.PointerMovedEvent, OnCanvasPointerMoved, RoutingStrategies.Tunnel);
            InkCanvasOverlay.AddHandler(InputElement.PointerReleasedEvent, OnCanvasPointerReleased, RoutingStrategies.Tunnel);

            PenBtn.AddHandler(PointerPressedEvent, PenBtn_PointerPressed, RoutingStrategies.Tunnel);
            EraserBtn.AddHandler(PointerPressedEvent, EraserBtn_PointerPressed, RoutingStrategies.Tunnel);

            InitializePenSettings();
            InitializeClearSlider();

            PluginManager.Instance.LoadPlugins();
            PluginDebugger.PrintPluginStatus();
            InitializePluginButtons();
        }

        private void UpdateButtonVisibility()
        {
            var normalButtons = this.FindControl<StackPanel>("NormalButtons");
            var selectModeButtons = this.FindControl<StackPanel>("SelectModeButtons");

            if (normalButtons != null)
                normalButtons.IsVisible = !IsSelectMode;
            if (selectModeButtons != null)
                selectModeButtons.IsVisible = IsSelectMode;
        }

        private void PenBtn_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            _penWasCheckedBeforeClick = PenBtn.IsChecked ?? false;
        }

        private void EraserBtn_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            _eraserWasCheckedBeforeClick = EraserBtn.IsChecked ?? false;
        }

        private void InitializePenSettings()
        {
            for (int i = 0; i < PenColors.Length && i < 30; i++)
            {
                var color = PenColors[i];
                var row = i / 5;
                var col = i % 5;

                var colorBtn = new Border
                {
                    Width = 32,
                    Height = 32,
                    CornerRadius = new CornerRadius(4),
                    Background = new SolidColorBrush(Color.FromArgb(color.Alpha, color.Red, color.Green, color.Blue)),
                    Margin = new Thickness(2),
                    Cursor = new Cursor(StandardCursorType.Hand),
                    Tag = i
                };

                colorBtn.PointerPressed += ColorBtn_PointerPressed;

                Grid.SetRow(colorBtn, row);
                Grid.SetColumn(colorBtn, col);
                ColorGrid.Children.Add(colorBtn);
            }

            for (int i = 0; i < PenSizes.Length; i++)
            {
                var size = PenSizes[i];
                var sizeBtn = new Border
                {
                    Width = 40,
                    Height = 40,
                    CornerRadius = new CornerRadius(6),
                    Background = new SolidColorBrush(Color.FromArgb(60, 255, 255, 255)),
                    Margin = new Thickness(2),
                    Cursor = new Cursor(StandardCursorType.Hand),
                    Tag = i
                };

                var circle = new Ellipse
                {
                    Width = Math.Min(size + 6, 28),
                    Height = Math.Min(size + 6, 28),
                    Fill = Brushes.White,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center
                };

                sizeBtn.Child = circle;
                sizeBtn.PointerPressed += SizeBtn_PointerPressed;

                SizePanel.Children.Add(sizeBtn);
            }

            UpdatePenSettingsSelection();

            LoadPenSettingsToInkCanvas();
        }

        private void LoadPenSettingsToInkCanvas()
        {
            var config = Config.Load();
            var settings = config.PenSettings ?? new PenSettings();

            InkCanvasOverlay.PenSettings = settings;
            InkCanvasOverlay.PalmEraserThreshold = settings.PalmEraserThreshold;
            InkCanvasOverlay.EnablePalmEraser = settings.EnablePalmEraser;
        }

        private void ColorBtn_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (sender is Border border && border.Tag is int index)
            {
                _currentPenColorIndex = index;
                var color = PenColors[index];
                InkCanvasOverlay.SetPenColor(color);
                UpdatePenSettingsSelection();
            }
        }

        private void SizeBtn_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (sender is Border border && border.Tag is int index)
            {
                _currentPenSizeIndex = index;
                var size = PenSizes[index];
                InkCanvasOverlay.SetPenSize(size);
                UpdatePenSettingsSelection();
            }
        }

        private void UpdatePenSettingsSelection()
        {
            for (int i = 0; i < ColorGrid.Children.Count; i++)
            {
                if (ColorGrid.Children[i] is Border border)
                {
                    border.BorderThickness = i == _currentPenColorIndex ? new Thickness(3) : new Thickness(0);
                    border.BorderBrush = i == _currentPenColorIndex ? Brushes.White : null;
                }
            }

            for (int i = 0; i < SizePanel.Children.Count; i++)
            {
                if (SizePanel.Children[i] is Border border)
                {
                    border.BorderThickness = i == _currentPenSizeIndex ? new Thickness(2) : new Thickness(0);
                    border.BorderBrush = i == _currentPenSizeIndex ? Brushes.White : null;
                }
            }
        }

        private bool _isSliderDragging = false;
        private double _sliderStartX = 0;
        private double _sliderStartOffset = 0;
        private Border? _slideTrack;
        private Border? _slideThumb;

        private void InitializeClearSlider()
        {
            _slideTrack = this.FindControl<Border>("SlideTrack");
            _slideThumb = this.FindControl<Border>("SlideThumb");

            if (_slideThumb != null)
            {
                _slideThumb.PointerPressed += SlideThumb_PointerPressed;
                _slideThumb.PointerMoved += SlideThumb_PointerMoved;
                _slideThumb.PointerReleased += SlideThumb_PointerReleased;
            }
        }

        private void InitializePluginButtons()
        {
            var normalBottomButtons = this.FindControl<StackPanel>("NormalBottomButtons");
            if (normalBottomButtons == null)
            {
                Console.WriteLine("错误: 找不到 NormalBottomButtons 控件");
                return;
            }

            Console.WriteLine($"NormalBottomButtons 当前子元素数量: {normalBottomButtons.Children.Count}");

            var pluginOverlay = this.FindControl<Border>("PluginOverlay");

            foreach (var plugin in PluginManager.Instance.Plugins.Where(p => p.IsEnabled && p.IsLoaded))
            {
                if (plugin.PluginInstance is IBottomToolbarPlugin bottomToolbarPlugin)
                {
                    try
                    {
                        bottomToolbarPlugin.SetRefreshToolbarCallback(RefreshPluginButtons);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"设置插件 {plugin.Name} 的刷新回调失败: {ex.Message}");
                    }
                }

                if (plugin.PluginInstance is IPluginWindow pluginWindow)
                {
                    try
                    {
                        if (pluginOverlay != null)
                        {
                            pluginWindow.SetPluginOverlay(pluginOverlay);
                        }
                        pluginWindow.SetToolbarVisibilityCallback(SetToolbarVisibility);
                        pluginWindow.SetShowResultWindowCallback(ShowPluginResultWindow);
                        pluginWindow.SetCancelCallback(() =>
                        {
                            pluginWindow.OnPluginDeactivated();
                        });
                        pluginWindow.SetShowNotificationCallback(ShowNotification);
                        _pluginWindows.Add(pluginWindow);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"设置插件 {plugin.Name} 的窗口回调失败: {ex.Message}");
                    }
                }
            }

            var pluginButtons = PluginManager.Instance.GetBottomToolbarButtons();
            Console.WriteLine($"获取到 {pluginButtons.Count} 个插件按钮");

            foreach (var pluginButton in pluginButtons)
            {
                try
                {
                    Console.WriteLine($"正在创建插件按钮: {pluginButton.Label}");

                    var button = new Button
                    {
                        Width = 56,
                        Height = 56,
                        IsEnabled = pluginButton.IsEnabled,
                        Tag = "plugin"
                    };
                    button.Classes.Add("tool-btn-square");

                    var stackPanel = new StackPanel
                    {
                        Orientation = Avalonia.Layout.Orientation.Vertical,
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
                    };

                    var viewbox = new Viewbox
                    {
                        Width = 28,
                        Height = 28
                    };

                    var path = new Avalonia.Controls.Shapes.Path
                    {
                        Fill = new SolidColorBrush(Colors.White),
                        Data = Geometry.Parse(pluginButton.IconPath)
                    };

                    viewbox.Child = path;

                    var textBlock = new TextBlock
                    {
                        Text = pluginButton.Label,
                        FontSize = 10,
                        Foreground = new SolidColorBrush(Color.FromRgb(204, 204, 204)),
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                        Margin = new Thickness(0, 4, 0, 0)
                    };

                    stackPanel.Children.Add(viewbox);
                    stackPanel.Children.Add(textBlock);

                    button.Content = stackPanel;

                    if (pluginButton.OnClick != null)
                    {
                        button.Click += (s, e) => pluginButton.OnClick();
                    }

                    normalBottomButtons.Children.Add(button);
                    Console.WriteLine($"成功添加按钮: {pluginButton.Label}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"创建插件按钮失败: {ex.Message}");
                    Console.WriteLine($"堆栈跟踪: {ex.StackTrace}");
                }
            }

            Console.WriteLine($"NormalBottomButtons 最终子元素数量: {normalBottomButtons.Children.Count}");
        }

        public void SetToolbarVisibility(bool visible)
        {
            var bottomToolbar = this.FindControl<Border>("BottomToolbar");
            var cancelButton = this.FindControl<Button>("PluginCancelButton");

            if (bottomToolbar != null)
                bottomToolbar.IsVisible = visible;
            if (cancelButton != null)
                cancelButton.IsVisible = !visible;
        }

        public void ShowPluginResultWindow(string result, bool isUrl, List<(string Text, Action Callback)> buttons)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                var window = new PluginResultWindow();
                window.SetResult(result, isUrl);
                window.ClearButtons();

                foreach (var (text, callback) in buttons)
                {
                    window.AddButton(text, callback);
                }

                window.ShowDialog(this);
            });
        }

        private Action? _pluginCancelCallback = null;

        public void SetPluginCancelCallback(Action callback)
        {
            _pluginCancelCallback = callback;
        }

        private void PluginCancelButton_Click(object sender, Avalonia.Interactivity.RoutedEventArgs e)
        {
            _pluginCancelCallback?.Invoke();
            SetToolbarVisibility(true);
        }

        public void ShowNotification(string message, int durationMs = 3000)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                var notificationWindow = new NotificationWindow();
                notificationWindow.ShowNotification(message, durationMs);
            });
        }

        public void RefreshPluginButtons()
        {
            var normalBottomButtons = this.FindControl<StackPanel>("NormalBottomButtons");
            if (normalBottomButtons == null)
            {
                Console.WriteLine("错误: 找不到 NormalBottomButtons 控件");
                return;
            }

            Console.WriteLine("正在刷新插件按钮...");

            var existingPluginButtons = normalBottomButtons.Children
                .OfType<Button>()
                .Where(b => b.Tag as string == "plugin")
                .ToList();

            foreach (var btn in existingPluginButtons)
            {
                normalBottomButtons.Children.Remove(btn);
            }

            var pluginButtons = PluginManager.Instance.GetBottomToolbarButtons();
            Console.WriteLine($"获取到 {pluginButtons.Count} 个插件按钮");

            foreach (var pluginButton in pluginButtons)
            {
                try
                {
                    Console.WriteLine($"正在创建插件按钮: {pluginButton.Label}");

                    var button = new Button
                    {
                        Width = 56,
                        Height = 56,
                        IsEnabled = pluginButton.IsEnabled,
                        Tag = "plugin"
                    };
                    button.Classes.Add("tool-btn-square");

                    var stackPanel = new StackPanel
                    {
                        Orientation = Avalonia.Layout.Orientation.Vertical,
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center
                    };

                    var viewbox = new Viewbox
                    {
                        Width = 28,
                        Height = 28
                    };

                    var path = new Avalonia.Controls.Shapes.Path
                    {
                        Fill = new SolidColorBrush(Colors.White),
                        Data = Geometry.Parse(pluginButton.IconPath)
                    };

                    viewbox.Child = path;

                    var textBlock = new TextBlock
                    {
                        Text = pluginButton.Label,
                        FontSize = 10,
                        Foreground = new SolidColorBrush(Color.FromRgb(204, 204, 204)),
                        HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center,
                        Margin = new Thickness(0, 4, 0, 0)
                    };

                    stackPanel.Children.Add(viewbox);
                    stackPanel.Children.Add(textBlock);

                    button.Content = stackPanel;

                    if (pluginButton.OnClick != null)
                    {
                        button.Click += (s, e) => pluginButton.OnClick();
                    }

                    normalBottomButtons.Children.Add(button);
                    Console.WriteLine($"成功添加按钮: {pluginButton.Label}");
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"创建插件按钮失败: {ex.Message}");
                    Console.WriteLine($"堆栈跟踪: {ex.StackTrace}");
                }
            }

            Console.WriteLine($"NormalBottomButtons 最终子元素数量: {normalBottomButtons.Children.Count}");
        }

        private void InitializeLoadingElements()
        {
            _loadingElements = new Control[]
            {
                this.FindControl<Canvas>("ElemPyro"),
                this.FindControl<Canvas>("ElemHydro"),
                this.FindControl<Canvas>("ElemAnemo"),
                this.FindControl<Canvas>("ElemElectro"),
                this.FindControl<Canvas>("ElemDendro"),
                this.FindControl<Canvas>("ElemCryo"),
                this.FindControl<Canvas>("ElemGeo")
            };

            foreach (var elem in _loadingElements)
            {
                if (elem != null)
                {
                    elem.Opacity = 1.0;
                    elem.Clip = new RectangleGeometry(new Rect(0, 0, 0, 50));
                }
            }
        }

        private void StartLoadingAnimation()
        {
            if (_isLoadingAnimationRunning) return;

            _isLoadingAnimationRunning = true;
            _loadingAnimationCts = new CancellationTokenSource();

            Task.Run(() => RunLoadingAnimationAsync(_loadingAnimationCts.Token));
        }

        private void StopLoadingAnimation()
        {
            _isLoadingAnimationRunning = false;
            _loadingAnimationCts?.Cancel();
            _loadingAnimationCts?.Dispose();
            _loadingAnimationCts = null;
        }

        private async Task RunLoadingAnimationAsync(CancellationToken token)
        {
            if (_loadingElements == null || _loadingElements.Length == 0) return;

            double iconWidth = 50;
            double spacing = 20;
            double totalWidth = iconWidth * _loadingElements.Length + spacing * (_loadingElements.Length - 1);
            double animationDuration = 5000;

            while (!token.IsCancellationRequested)
            {
                var startTime = DateTime.UtcNow;
                var duration = TimeSpan.FromMilliseconds(animationDuration);

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    foreach (var elem in _loadingElements)
                    {
                        if (elem != null)
                        {
                            elem.Opacity = 1.0;
                            elem.Clip = new RectangleGeometry(new Rect(0, 0, 0, 50));
                        }
                    }
                });

                while (true)
                {
                    if (token.IsCancellationRequested) break;

                    var elapsed = DateTime.UtcNow - startTime;
                    if (elapsed >= duration) break;

                    var progress = elapsed.TotalMilliseconds / duration.TotalMilliseconds;
                    var currentWidth = totalWidth * progress;

                    await Dispatcher.UIThread.InvokeAsync(() =>
                    {
                        double currentX = 0;

                        for (int i = 0; i < _loadingElements.Length; i++)
                        {
                            var elem = _loadingElements[i];
                            if (elem == null) continue;

                            double elemStartX = currentX;
                            double elemEndX = currentX + iconWidth;

                            if (currentWidth >= elemEndX)
                            {
                                elem.Clip = new RectangleGeometry(new Rect(0, 0, iconWidth, 50));
                            }
                            else if (currentWidth > elemStartX)
                            {
                                double fillWidth = currentWidth - elemStartX;
                                elem.Clip = new RectangleGeometry(new Rect(0, 0, fillWidth, 50));
                            }
                            else
                            {
                                elem.Clip = new RectangleGeometry(new Rect(0, 0, 0, 50));
                            }

                            currentX += iconWidth + spacing;
                        }
                    });

                    await Task.Delay(16, token);
                }

                if (token.IsCancellationRequested) break;

                await Dispatcher.UIThread.InvokeAsync(() =>
                {
                    foreach (var elem in _loadingElements)
                    {
                        if (elem != null)
                        {
                            elem.Clip = new RectangleGeometry(new Rect(0, 0, iconWidth, 50));
                        }
                    }
                });

                await Task.Delay(2000, token);
            }
        }

        private void SlideThumb_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (_slideTrack == null || _slideThumb == null) return;

            _isSliderDragging = true;
            _sliderStartX = e.GetPosition(_slideTrack).X;
            _sliderStartOffset = _slideThumb.Margin.Left;
            _slideThumb.Background = new SolidColorBrush(Color.FromRgb(255, 100, 100));
            e.Pointer.Capture(_slideThumb);
            e.Handled = true;
        }

        private void SlideThumb_PointerMoved(object? sender, PointerEventArgs e)
        {
            if (!_isSliderDragging || _slideTrack == null || _slideThumb == null) return;

            var currentX = e.GetPosition(_slideTrack).X;
            var trackWidth = _slideTrack.Bounds.Width;
            var thumbWidth = _slideThumb.Width;

            var maxOffset = trackWidth - thumbWidth;
            var deltaX = currentX - _sliderStartX;
            var newOffset = Math.Clamp(_sliderStartOffset + deltaX, 0, maxOffset);

            _slideThumb.Margin = new Thickness(newOffset, 0, 0, 0);

            var progress = newOffset / maxOffset;
            if (progress > 0.8)
            {
                _slideThumb.Background = new SolidColorBrush(Color.FromRgb(76, 175, 80));
            }
            else
            {
                _slideThumb.Background = new SolidColorBrush(Color.FromRgb(255, 100, 100));
            }
        }

        private void SlideThumb_PointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (!_isSliderDragging || _slideTrack == null || _slideThumb == null) return;

            _isSliderDragging = false;
            e.Pointer.Capture(null);

            var currentOffset = _slideThumb.Margin.Left;
            var trackWidth = _slideTrack.Bounds.Width;
            var thumbWidth = _slideThumb.Width;
            var maxOffset = trackWidth - thumbWidth;
            var progress = currentOffset / maxOffset;

            _slideThumb.Margin = new Thickness(0, 0, 0, 0);
            _slideThumb.Background = new SolidColorBrush(Color.FromRgb(255, 107, 107));

            if (progress > 0.8)
            {
                InkCanvasOverlay.ClearAll();
                if (_clearSlidePopup != null)
                {
                    _clearSlidePopup.IsOpen = false;
                }
            }
        }

        #region Window 生命周期

        private void CloseLoadingWindow()
        {
            StopLoadingAnimation();
            var loadingContainer = this.FindControl<StackPanel>("LoadingContainer");
            if (loadingContainer != null)
            {
                loadingContainer.IsVisible = false;
            }
        }

        private void Window_Opened(object? sender, EventArgs e)
        {
            this.WindowState = WindowState.FullScreen;

            InitializeLoadingElements();

            _ = InitializeLicenseAsync();

            _cameraService.DetectAndConnectCamera();
        }

        private async Task InitializeLicenseAsync()
        {
            try
            {
                var uuid = await LicenseManager.Instance.GetOrCreateLicenseAsync();
                System.Diagnostics.Debug.WriteLine($"[MainWindow] 许可证初始化成功，UUID: {uuid}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MainWindow] 许可证初始化失败: {ex.Message}");
                StatusText.Inlines?.Clear();
                StatusText.Inlines?.Add(new Run($"许可证验证失败: {ex.Message}"));
                StatusText.IsVisible = true;
            }
        }

        private void Window_Closing(object? sender, WindowClosingEventArgs e)
        {
            StopLoadingAnimation();
            _cameraService.StopCapture();
            _cameraService.Dispose();

            // 清理PowerPoint资源
            _whiteboardManager?.ClosePowerPointPresentation();
            _whiteboardManager?.Dispose();

            InkCanvasOverlay.Dispose();
        }

        #endregion

        #region 摄像头事件处理

        private void OnCameraError(string message)
        {
            StatusText.Inlines?.Clear();
            StatusText.Inlines?.Add(new Run(message));
            StatusText.IsVisible = true;
        }

        private void OnUsingCachedCameras()
        {
            if (_cameraService.IsConnected) return;
            StatusText.Inlines?.Clear();
            StatusText.Inlines?.Add(new Run("正在连接摄像头..."));
            StatusText.IsVisible = true;
            var loadingPanel = this.FindControl<StackPanel>("LoadingElementsPanel");
            if (loadingPanel != null)
            {
                loadingPanel.IsVisible = true;
            }
            StartLoadingAnimation();
        }

        private void OnScanComplete()
        {
            StatusText.IsVisible = false;
        }

        private void OnFrameReady()
        {
            if (_cameraService.FrameBitmap == null)
            {
                Console.WriteLine("[MainWindow] FrameBitmap is null!");
                return;
            }

            VideoImage.Source = _cameraService.FrameBitmap;
            VideoImage.InvalidateVisual();
            InkCanvasOverlay.SetVideoFrame(_cameraService.LatestFrame);
            InkCanvasOverlay.InvalidateVisual();

            foreach (var pluginWindow in _pluginWindows)
            {
                try
                {
                    pluginWindow.OnCameraFrame(_cameraService.FrameBitmap);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"插件处理摄像头帧失败: {ex.Message}");
                }
            }
        }

        private void OnCameraStarted()
        {
            if (_isPhotoAnnotationMode) return;

            CloseLoadingWindow();

            VideoImage.Source = _cameraService.FrameBitmap;

            var frame = _cameraService.LatestFrame;
            if (frame != null && !frame.Empty())
            {
                _videoWidth = frame.Width;
                _videoHeight = frame.Height;

                VideoImage.Width = _videoWidth;
                VideoImage.Height = _videoHeight;

                InkCanvasOverlay.SetVideoFrame(frame);

                CenterVideo();

                LoadKeystoneSettings();
            }
        }

        private void CenterVideo()
        {
            if (_videoWidth <= 0 || _videoHeight <= 0) return;

            var containerWidth = VideoAreaContainer.Bounds.Width;
            var containerHeight = VideoAreaContainer.Bounds.Height;

            var scaleX = containerWidth / _videoWidth;
            var scaleY = containerHeight / _videoHeight;
            _zoom = Math.Min(scaleX, scaleY);

            var scaledWidth = _videoWidth * _zoom;
            var scaledHeight = _videoHeight * _zoom;

            _panOffset = new Point(
                (containerWidth - scaledWidth) / 2,
                (containerHeight - scaledHeight) / 2
            );

            UpdateTransform();
        }

        #endregion

        #region 缩放和平移

        private void OnPointerWheelChanged(object? sender, PointerWheelEventArgs e)
        {
            if (_whiteboardManager?.HandlePointerWheel() == true) return;

            var delta = e.Delta.Y;
            if (Math.Abs(delta) < 0.001) return;

            var mousePos = e.GetPosition(VideoAreaContainer);

            var videoPos = new Point(
                (mousePos.X - _panOffset.X) / _zoom,
                (mousePos.Y - _panOffset.Y) / _zoom
            );

            _zoom = Math.Clamp(_zoom + delta * ZoomStep, MinZoom, MaxZoom);

            _panOffset = new Point(
                mousePos.X - videoPos.X * _zoom,
                mousePos.Y - videoPos.Y * _zoom
            );

            UpdateTransform();
        }

        private void OnCanvasPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            // 如果处于梯形校正模式且点击的元素是校正点或校正画布的子元素，则忽略平移
            if (_isKeystoneCorrectionMode)
            {
                var source = e.Source as Visual;
                while (source != null)
                {
                    if (source == _keystonePointTL || source == _keystonePointTR ||
                        source == _keystonePointBR || source == _keystonePointBL ||
                        source == _keystoneOverlayCanvas)
                    {
                        // 不处理平移，让事件继续传递给校正点
                        return;
                    }
                    source = source.GetVisualParent(); // 使用扩展方法
                }
            }

            if (_whiteboardManager?.HandleCanvasPointerPressed() == true) return;

            var props = e.GetCurrentPoint(VideoAreaContainer).Properties;
            bool left = props.IsLeftButtonPressed;
            bool wantPan =
                props.IsMiddleButtonPressed ||
                props.IsRightButtonPressed ||
                (left && (!InkCanvasOverlay.IsPenMode && !InkCanvasOverlay.IsEraserMode));

            if (!wantPan) return;

            _isPanning = true;
            _lastPanPoint = e.GetPosition(VideoAreaContainer);
            e.Pointer.Capture(VideoAreaContainer);
            VideoAreaContainer.Cursor = new Cursor(StandardCursorType.SizeAll);
            e.Handled = true;
        }

        private void OnCanvasPointerMoved(object? sender, PointerEventArgs e)
        {
            if (!_isPanning) return;

            var currentPoint = e.GetPosition(VideoAreaContainer);
            var delta = currentPoint - _lastPanPoint;
            _panOffset = new Point(_panOffset.X + delta.X, _panOffset.Y + delta.Y);
            _lastPanPoint = currentPoint;

            UpdateTransform();
        }

        private void OnCanvasPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (_isPanning)
            {
                _isPanning = false;
                e.Pointer.Capture(null);
                VideoAreaContainer.Cursor = Cursor.Default;
            }
        }

        private void UpdateTransform()
        {
            if (_scaleTransform == null || _translateTransform == null) return;

            _scaleTransform.ScaleX = _zoom;
            _scaleTransform.ScaleY = _zoom;
            _translateTransform.X = _panOffset.X;
            _translateTransform.Y = _panOffset.Y;

            InkCanvasOverlay.SetTransform(_zoom, _panOffset);
            InkCanvasOverlay.InvalidateVisual();

            if (_isKeystoneCorrectionMode)
            {
                UpdateKeystonePointsDisplay();
                UpdateKeystonePolygon();
            }
        }

        #endregion

        #region 所有按钮事件

        private void MoreButton_Click(object? sender, RoutedEventArgs e)
        {
            MoreMenuPopup.IsOpen = !MoreMenuPopup.IsOpen;
        }

        private void Minimize_Click(object? sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private enum ToolMode { Move, Pen, Eraser }

        private async Task ApplyToolMode(ToolMode mode)
        {
            PenSettingsPopup.IsOpen = false;
            if (_clearSlidePopup != null) _clearSlidePopup.IsOpen = false;

            switch (mode)
            {
                case ToolMode.Move:
                    MoveBtn.IsChecked = true;
                    PenBtn.IsChecked = false;
                    EraserBtn.IsChecked = false;
                    InkCanvasOverlay.SetMoveMode();
                    VideoAreaContainer.Cursor = Cursor.Default;
                    if (_toolSliderBackground != null)
                        await UIAnimations.SlideToolBackground(_toolSliderBackground, 0);
                    break;
                case ToolMode.Pen:
                    PenBtn.IsChecked = true;
                    MoveBtn.IsChecked = false;
                    EraserBtn.IsChecked = false;
                    InkCanvasOverlay.SetPenMode();
                    if (_toolSliderBackground != null)
                        await UIAnimations.SlideToolBackground(_toolSliderBackground, ThemeManager.CurrentColors.SliderIndicatorSize);
                    break;
                case ToolMode.Eraser:
                    EraserBtn.IsChecked = true;
                    MoveBtn.IsChecked = false;
                    PenBtn.IsChecked = false;
                    InkCanvasOverlay.SetEraserMode();
                    if (_toolSliderBackground != null)
                        await UIAnimations.SlideToolBackground(_toolSliderBackground, ThemeManager.CurrentColors.SliderIndicatorSize * 2);
                    break;
            }
        }

        private async void MoveBtn_Click(object? sender, RoutedEventArgs e)
        {
            await ApplyToolMode(ToolMode.Move);
        }

        private bool _penWasCheckedBeforeClick = false;

        private async void PenBtn_Click(object? sender, RoutedEventArgs e)
        {
            if (_penWasCheckedBeforeClick)
            {
                PenBtn.IsChecked = true;
                PenSettingsPopup.IsOpen = !PenSettingsPopup.IsOpen;
            }
            else
            {
                await ApplyToolMode(ToolMode.Pen);
            }
        }

        private bool _eraserWasCheckedBeforeClick = false;

        private async void EraserBtn_Click(object? sender, RoutedEventArgs e)
        {
            if (_eraserWasCheckedBeforeClick)
            {
                EraserBtn.IsChecked = true;
                if (_clearSlidePopup != null) _clearSlidePopup.IsOpen = !_clearSlidePopup.IsOpen;
            }
            else
            {
                await ApplyToolMode(ToolMode.Eraser);
            }
        }

        private void UndoInk_Click(object? sender, RoutedEventArgs e)
        {
            InkCanvasOverlay.Undo();
        }

        private void ClearInk_Click(object? sender, RoutedEventArgs e)
        {
            InkCanvasOverlay.ClearAll();
        }

        private void Capture_Click(object? sender, RoutedEventArgs e)
        {
            var frame = _cameraService.LatestFrame;
            if (frame == null || frame.Empty())
                return;

            var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
            var fileName = $"photo_{timestamp}.jpg";

            var dir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                "ShowWrite");

            Directory.CreateDirectory(dir);

            var path = Path.Combine(dir, fileName);
            Cv2.ImWrite(path, frame);

            var thumbnail = CreateThumbnail(path);

            Photos.Add(new PhotoItem
            {
                Index = Photos.Count + 1,
                FilePath = path,
                Timestamp = DateTime.Now,
                Thumbnail = thumbnail
            });

            ShowNotification("照片已保存");
        }

        private void ScanDocument_Click(object? sender, RoutedEventArgs e) { }

        private void PictureInPicture_Click(object? sender, RoutedEventArgs e)
        {
            if (_whiteboardManager == null) return;

            _cameraService?.CancelConnecting();
            _cameraService?.StopCapture();
            CloseLoadingWindow();

            var normalBottomButtons = this.FindControl<StackPanel>("NormalBottomButtons");
            var captureBtn = this.FindControl<Button>("CaptureBtn");
            var scanBtn = this.FindControl<Button>("ScanBtn");
            var connectDeviceBtn = this.FindControl<Button>("ConnectDeviceBtn");
            var pipBtn = this.FindControl<Button>("PictureInPictureBtn");
            var normalRightButtons = this.FindControl<StackPanel>("NormalRightButtons");

            _whiteboardManager.EnterWhiteboardMode(
                normalBottomButtons,
                captureBtn,
                scanBtn,
                connectDeviceBtn,
                pipBtn,
                normalRightButtons);
        }

        private void ExitWhiteboard_Click(object? sender, RoutedEventArgs e)
        {
            if (_whiteboardManager == null) return;

            // 关闭PowerPoint放映
            if (_whiteboardManager.IsPowerPointMode)
            {
                _whiteboardManager.ClosePowerPointPresentation();
            }

            var normalBottomButtons = this.FindControl<StackPanel>("NormalBottomButtons");
            var captureBtn = this.FindControl<Button>("CaptureBtn");
            var scanBtn = this.FindControl<Button>("ScanBtn");
            var connectDeviceBtn = this.FindControl<Button>("ConnectDeviceBtn");
            var pipBtn = this.FindControl<Button>("PictureInPictureBtn");
            var normalRightButtons = this.FindControl<StackPanel>("NormalRightButtons");
            var loadingContainer = this.FindControl<StackPanel>("LoadingContainer");
            var loadingPanel = this.FindControl<StackPanel>("LoadingElementsPanel");

            _whiteboardManager.ExitWhiteboardMode(
                normalBottomButtons,
                captureBtn,
                scanBtn,
                connectDeviceBtn,
                pipBtn,
                normalRightButtons,
                MoveBtn,
                PenBtn,
                EraserBtn,
                _toolSliderBackground,
                loadingContainer,
                loadingPanel,
                StartLoadingAnimation);
        }

        private void PrevPage_Click(object? sender, RoutedEventArgs e)
        {
            // PowerPoint模式
            if (_whiteboardManager?.IsPowerPointMode == true)
            {
                _whiteboardManager.PowerPointPreviousSlide();
                return;
            }

            // Aspose模式
            if (_whiteboardManager?.IsAsposePresentationOpen == true)
            {
                _whiteboardManager.AsposePreviousSlide();
                var slideBitmap = _whiteboardManager.RenderAsposeCurrentSlide();
                if (slideBitmap is not null)
                {
                    VideoImage.Source = slideBitmap;
                    VideoImage.Width = slideBitmap.Size.Width;
                    VideoImage.Height = slideBitmap.Size.Height;
                    InkCanvasOverlay.SetPhotoMode(slideBitmap.Size.Width, slideBitmap.Size.Height);
                    InkCanvasOverlay.ClearStrokes();
                    CenterVideo();
                }
                return;
            }

            // 白板模式
            _whiteboardManager?.PrevPage_Click(sender, e);
        }

        private void NextPage_Click(object? sender, RoutedEventArgs e)
        {
            // PowerPoint模式
            if (_whiteboardManager?.IsPowerPointMode == true)
            {
                _whiteboardManager.PowerPointNextSlide();
                return;
            }

            // Aspose模式
            if (_whiteboardManager?.IsAsposePresentationOpen == true)
            {
                _whiteboardManager.AsposeNextSlide();
                var slideBitmap = _whiteboardManager.RenderAsposeCurrentSlide();
                if (slideBitmap is not null)
                {
                    VideoImage.Source = slideBitmap;
                    VideoImage.Width = slideBitmap.Size.Width;
                    VideoImage.Height = slideBitmap.Size.Height;
                    InkCanvasOverlay.SetPhotoMode(slideBitmap.Size.Width, slideBitmap.Size.Height);
                    InkCanvasOverlay.ClearStrokes();
                    CenterVideo();
                }
                return;
            }

            // 白板模式
            _whiteboardManager?.NextPage_Click(sender, e);
        }

        private void PageInfoBorder_PointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
        {
            _whiteboardManager?.PageInfoBorder_PointerPressed(sender, e);
        }

        private void PageItem_PointerPressed(object? sender, Avalonia.Input.PointerPressedEventArgs e)
        {
            _whiteboardManager?.PageItem_PointerPressed(sender, e);
        }

        private void AddPageBtn_Click(object? sender, RoutedEventArgs e)
        {
            _whiteboardManager?.AddPageBtn_Click(sender, e);
        }

        private async void ImportPptBtn_Click(object? sender, RoutedEventArgs e)
        {
            await OpenPptFilePicker();
        }

        private async void OnImportPptRequested()
        {
            await OpenPptFilePicker();
        }

        private async Task OpenPptFilePicker()
        {
            var storageProvider = StorageProvider;
            if (storageProvider == null) return;

            var files = await storageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions
            {
                Title = "选择PPT文件",
                AllowMultiple = false,
                FileTypeFilter = new[]
                {
                    new Avalonia.Platform.Storage.FilePickerFileType("PowerPoint文件")
                    {
                        Patterns = new[] { "*.pptx", "*.ppt" },
                        MimeTypes = new[] { "application/vnd.ms-powerpoint", "application/vnd.openxmlformats-officedocument.presentationml.presentation" }
                    }
                }
            });

            if (files.Count > 0)
            {
                var filePath = files[0].Path.LocalPath;
                await LoadAndPlayPowerPoint(filePath);
            }
        }

        private async Task LoadAndPlayPowerPoint(string filePath)
        {
            if (_whiteboardManager == null) return;

            try
            {
                // 进入白板模式
                if (!_whiteboardManager.IsWhiteboardMode)
                {
                    var normalBottomButtons = this.FindControl<StackPanel>("NormalBottomButtons");
                    var captureBtn = this.FindControl<Button>("CaptureBtn");
                    var scanBtn = this.FindControl<Button>("ScanBtn");
                    var connectDeviceBtn = this.FindControl<Button>("ConnectDeviceBtn");
                    var pipBtn = this.FindControl<Button>("PipBtn");
                    var normalRightButtons = this.FindControl<StackPanel>("NormalRightButtons");

                    _whiteboardManager.EnterWhiteboardMode(
                        normalBottomButtons!,
                        captureBtn!,
                        scanBtn!,
                        connectDeviceBtn!,
                        pipBtn!,
                        normalRightButtons!);
                }

                // 打开PPT文件
                if (_whiteboardManager.OpenPowerPointPresentation(filePath))
                {
                    // 获取白板背景控件作为宿主窗口
                    var whiteboardBackground = this.FindControl<Border>("WhiteboardBackground");
                    if (whiteboardBackground != null)
                    {
                        // 获取窗口句柄
                        var windowHandle = GetWindowHandle();

                        // 获取白板区域大小
                        var bounds = whiteboardBackground.Bounds;
                        int width = (int)bounds.Width;
                        int height = (int)bounds.Height;

                        // 开始放映
                        if (_whiteboardManager.StartPowerPointSlideShow(windowHandle, width, height))
                        {
                            StatusText.Inlines?.Clear();
                            StatusText.Inlines?.Add(new Run($"已加载PPT: {System.IO.Path.GetFileName(filePath)}"));
                            StatusText.IsVisible = true;
                        }
                        else
                        {
                            StatusText.Inlines?.Clear();
                            StatusText.Inlines?.Add(new Run("PPT放映启动失败"));
                            StatusText.IsVisible = true;
                        }
                    }
                }
                else
                {
                    StatusText.Inlines?.Clear();
                    StatusText.Inlines?.Add(new Run("无法打开PPT文件"));
                    StatusText.IsVisible = true;
                }
            }
            catch (Exception ex)
            {
                StatusText.Inlines?.Clear();
                StatusText.Inlines?.Add(new Run($"加载PPT失败: {ex.Message}"));
                StatusText.IsVisible = true;
            }
        }

        private IntPtr GetWindowHandle()
        {
            // 获取Avalonia窗口的本地句柄
            var platformHandle = this.TryGetPlatformHandle();
            return platformHandle?.Handle ?? IntPtr.Zero;
        }

        private bool _photoPanelOpen = false;

        private async void TogglePhotoPanel_Click(object? sender, RoutedEventArgs e)
        {
            var photoPanel = this.FindControl<Border>("PhotoPanel");
            if (photoPanel == null) return;

            if (!_photoPanelOpen)
            {
                _photoPanelOpen = true;
                await UIAnimations.SlideInFromRight(photoPanel, 280);
            }
            else
            {
                _photoPanelOpen = false;
                await UIAnimations.SlideOutToRight(photoPanel, 280);
                photoPanel.IsVisible = false;
            }
        }


        private void ConnectDeviceBtn_Click(object? sender, RoutedEventArgs e)
        {
            if (_isPhotoAnnotationMode)
            {
                ExitPhotoAnnotationMode();
            }

            var dialog = new CameraSelectWindow(_cameraService);
            dialog.CameraSelected += (cameraIndex) =>
            {
                var loadingContainer = this.FindControl<StackPanel>("LoadingContainer");
                if (loadingContainer != null)
                {
                    loadingContainer.IsVisible = true;
                }
                var loadingPanel = this.FindControl<StackPanel>("LoadingElementsPanel");
                if (loadingPanel != null)
                {
                    loadingPanel.IsVisible = true;
                }
                StartLoadingAnimation();

                _cameraService?.StartCapture(cameraIndex);
            };
            dialog.ShowDialog(this);
        }

        private void SavePhotoBtn_Click(object? sender, RoutedEventArgs e)
        {
            IsSelectMode = true;
        }

        private async void ConfirmSavePhotos_Click(object? sender, RoutedEventArgs e)
        {
            var checkedPhotos = Photos.Where(p => p.IsChecked).ToList();
            if (checkedPhotos.Count == 0)
            {
                IsSelectMode = false;
                return;
            }

            var storageProvider = this.StorageProvider;
            if (storageProvider == null) return;

            var folder = await storageProvider.OpenFolderPickerAsync(new Avalonia.Platform.Storage.FolderPickerOpenOptions
            {
                Title = "选择保存位置"
            });

            if (folder.Count == 0)
            {
                return;
            }

            var destDir = folder[0].Path.LocalPath;
            Directory.CreateDirectory(destDir);

            foreach (var photo in checkedPhotos)
            {
                if (string.IsNullOrEmpty(photo.FilePath) || !File.Exists(photo.FilePath))
                    continue;

                var destPath = Path.Combine(destDir, Path.GetFileName(photo.FilePath));

                if (IncludeInkAnnotations && photo.Strokes.Count > 0)
                {
                    SavePhotoWithAnnotations(photo, destPath);
                }
                else
                {
                    File.Copy(photo.FilePath, destPath, true);
                }
            }

            IsSelectMode = false;
            foreach (var p in Photos) p.IsChecked = false;
        }

        private void CancelSelectMode_Click(object? sender, RoutedEventArgs e)
        {
            IsSelectMode = false;
            foreach (var p in Photos) p.IsChecked = false;
        }

        private void InvertSelection_Click(object? sender, RoutedEventArgs e)
        {
            foreach (var p in Photos)
            {
                p.IsChecked = !p.IsChecked;
            }
        }

        private void SavePhotoWithAnnotations(PhotoItem photo, string destPath)
        {
            try
            {
                using var originalBitmap = SKBitmap.Decode(photo.FilePath);
                if (originalBitmap == null)
                {
                    File.Copy(photo.FilePath, destPath, true);
                    return;
                }

                using var surface = SKSurface.Create(new SKImageInfo(originalBitmap.Width, originalBitmap.Height));
                var canvas = surface.Canvas;

                canvas.DrawBitmap(originalBitmap, 0, 0);

                foreach (var stroke in photo.Strokes)
                {
                    if (stroke.VideoPoints == null || stroke.PointWidths == null) continue;

                    using var paint = new SKPaint
                    {
                        Style = SKPaintStyle.Fill,
                        IsAntialias = true,
                        Color = stroke.Color
                    };

                    for (int i = 0; i < stroke.VideoPoints.Count; i++)
                    {
                        var point = stroke.VideoPoints[i];
                        var width = stroke.PointWidths[i];
                        canvas.DrawCircle(point.X, point.Y, width / 2, paint);
                    }
                }

                using var image = surface.Snapshot();
                using var data = image.Encode(SKEncodedImageFormat.Jpeg, 90);
                using var fileStream = File.OpenWrite(destPath);
                data.SaveTo(fileStream);
            }
            catch
            {
                File.Copy(photo.FilePath, destPath, true);
            }
        }

        private async void ImportPhotoBtn_Click(object? sender, RoutedEventArgs e)
        {
            var storageProvider = this.StorageProvider;
            if (storageProvider == null) return;

            var files = await storageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions
            {
                Title = "选择照片或PDF文件",
                AllowMultiple = true,
                FileTypeFilter = new[]
                {
                    new Avalonia.Platform.Storage.FilePickerFileType("图片文件")
                    {
                        Patterns = new[] { "*.jpg", "*.jpeg", "*.png", "*.bmp", "*.gif" }
                    },
                    new Avalonia.Platform.Storage.FilePickerFileType("PDF文件")
                    {
                        Patterns = new[] { "*.pdf" }
                    },
                    new Avalonia.Platform.Storage.FilePickerFileType("所有支持的文件")
                    {
                        Patterns = new[] { "*.jpg", "*.jpeg", "*.png", "*.bmp", "*.gif", "*.pdf" }
                    }
                }
            });

            if (files.Count == 0) return;

            ImportingOverlay.IsVisible = true;

            try
            {
                await System.Threading.Tasks.Task.Run(async () =>
                {
                    foreach (var file in files)
                    {
                        var path = file.Path.LocalPath;

                        if (path.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))
                        {
                            var imagePaths = PdfService.ConvertPdfToImages(path);

                            await Dispatcher.UIThread.InvokeAsync(() =>
                            {
                                foreach (var imagePath in imagePaths)
                                {
                                    var thumbnail = CreateThumbnail(imagePath);

                                    Photos.Add(new PhotoItem
                                    {
                                        Index = Photos.Count + 1,
                                        FilePath = imagePath,
                                        Timestamp = DateTime.Now,
                                        Thumbnail = thumbnail
                                    });
                                }
                            });
                        }
                        else
                        {
                            await Dispatcher.UIThread.InvokeAsync(() =>
                            {
                                var thumbnail = CreateThumbnail(path);

                                Photos.Add(new PhotoItem
                                {
                                    Index = Photos.Count + 1,
                                    FilePath = path,
                                    Timestamp = DateTime.Now,
                                    Thumbnail = thumbnail
                                });
                            });
                        }
                    }
                });
            }
            finally
            {
                ImportingOverlay.IsVisible = false;
            }
        }

        private void PhotoItem_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (sender is Border border && border.Tag is PhotoItem photo)
            {
                if (photo.IsSelected && _isPhotoAnnotationMode)
                {
                    ExitPhotoAnnotationMode();
                    return;
                }

                if (_selectedPhoto != null && _isPhotoAnnotationMode)
                {
                    _selectedPhoto.Strokes = InkCanvasOverlay.GetStrokes();
                }

                foreach (var p in Photos)
                {
                    p.IsSelected = false;
                }

                photo.IsSelected = true;
                _selectedPhoto = photo;

                ShowPhotoForAnnotation(photo);
            }
        }

        private void ShowPhotoForAnnotation(PhotoItem photo)
        {
            if (string.IsNullOrEmpty(photo.FilePath) || !File.Exists(photo.FilePath))
                return;

            _isPhotoAnnotationMode = true;

            _cameraService?.CancelConnecting();
            _cameraService?.StopCapture();
            CloseLoadingWindow();

            using var stream = File.OpenRead(photo.FilePath);
            var bitmap = new Bitmap(stream);

            if (bitmap != null)
            {
                VideoImage.Source = bitmap;
                VideoImage.Width = bitmap.Size.Width;
                VideoImage.Height = bitmap.Size.Height;

                InkCanvasOverlay.SetPhotoMode(bitmap.Size.Width, bitmap.Size.Height);
                InkCanvasOverlay.SetStrokes(photo.Strokes);
            }

            StatusText.IsVisible = false;
        }

        public void ExitPhotoAnnotationMode()
        {
            if (!_isPhotoAnnotationMode) return;

            _isPhotoAnnotationMode = false;

            if (_selectedPhoto != null)
            {
                _selectedPhoto.Strokes = InkCanvasOverlay.GetStrokes();
                _selectedPhoto.IsSelected = false;
                _selectedPhoto = null;
            }

            InkCanvasOverlay.ExitPhotoMode();
            InkCanvasOverlay.ClearStrokes();

            if (!_cameraService.IsConnected)
            {
                var loadingContainer = this.FindControl<StackPanel>("LoadingContainer");
                if (loadingContainer != null)
                {
                    loadingContainer.IsVisible = true;
                }
                var loadingPanel = this.FindControl<StackPanel>("LoadingElementsPanel");
                if (loadingPanel != null)
                {
                    loadingPanel.IsVisible = true;
                }
                StartLoadingAnimation();
            }
            _cameraService?.DetectAndConnectCamera();
        }

        private void DeletePhoto_Click(object? sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is PhotoItem photo)
            {
                photo.IsDeleting = true;
            }
            e.Handled = true;
        }

        private void ConfirmDeletePhoto_Click(object? sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is PhotoItem photo)
            {
                if (_selectedPhoto == photo && _isPhotoAnnotationMode)
                {
                    ExitPhotoAnnotationMode();
                }

                if (!string.IsNullOrEmpty(photo.FilePath) && File.Exists(photo.FilePath))
                {
                    try
                    {
                        File.Delete(photo.FilePath);
                    }
                    catch { }
                }

                Photos.Remove(photo);

                for (int i = 0; i < Photos.Count; i++)
                {
                    Photos[i].Index = i + 1;
                }
            }
            e.Handled = true;
        }

        private void CancelDeletePhoto_Click(object? sender, RoutedEventArgs e)
        {
            if (sender is Button button && button.Tag is PhotoItem photo)
            {
                photo.IsDeleting = false;
            }
            e.Handled = true;
        }

        private Bitmap? CreateThumbnail(string imagePath)
        {
            try
            {
                using var stream = File.OpenRead(imagePath);
                var decodeTask = Task.Run(() => SkiaSharp.SKBitmap.Decode(stream));
                decodeTask.Wait();

                using var originalBitmap = decodeTask.Result;
                if (originalBitmap == null) return null;

                int thumbWidth = 260;
                int thumbHeight = (int)(originalBitmap.Height * ((double)thumbWidth / originalBitmap.Width));

                using var resizedBitmap = originalBitmap.Resize(new SkiaSharp.SKImageInfo(thumbWidth, thumbHeight), SkiaSharp.SKFilterQuality.Medium);
                if (resizedBitmap == null) return null;

                using var image = SkiaSharp.SKImage.FromBitmap(resizedBitmap);
                using var data = image.Encode(SkiaSharp.SKEncodedImageFormat.Png, 80);

                var ms = new MemoryStream(data.ToArray());
                return new Bitmap(ms);
            }
            catch
            {
                return null;
            }
        }

        private void SwitchCamera_Click(object? sender, RoutedEventArgs e)
        {
            MoreMenuPopup.IsOpen = false;

            if (_isPhotoAnnotationMode)
            {
                ExitPhotoAnnotationMode();
            }

            var win = new CameraSelectWindow(_cameraService);
            win.CameraSelected += idx =>
            {
                var loadingContainer = this.FindControl<StackPanel>("LoadingContainer");
                if (loadingContainer != null)
                {
                    loadingContainer.IsVisible = true;
                }
                var loadingPanel = this.FindControl<StackPanel>("LoadingElementsPanel");
                if (loadingPanel != null)
                {
                    loadingPanel.IsVisible = true;
                }
                StartLoadingAnimation();

                _cameraService?.SwitchToCamera(idx);
            };
            win.ShowDialog(this);
        }

        private void OpenSettings_Click(object? sender, RoutedEventArgs e)
        {
            MoreMenuPopup.IsOpen = false;
            var settingsWindow = new SettingsWindow();
            settingsWindow.ShowDialog(this);
        }

        private void Exit_Click(object? sender, RoutedEventArgs e)
        {
            this.Close();
        }

        #endregion

        #region 梯形校正

        private void InitializeKeystonePoints()
        {
            if (_keystonePointTL != null)
            {
                _keystonePointTL.PointerPressed += KeystonePoint_PointerPressed;
                _keystonePointTL.PointerMoved += KeystonePoint_PointerMoved;
                _keystonePointTL.PointerReleased += KeystonePoint_PointerReleased;
            }
            if (_keystonePointTR != null)
            {
                _keystonePointTR.PointerPressed += KeystonePoint_PointerPressed;
                _keystonePointTR.PointerMoved += KeystonePoint_PointerMoved;
                _keystonePointTR.PointerReleased += KeystonePoint_PointerReleased;
            }
            if (_keystonePointBR != null)
            {
                _keystonePointBR.PointerPressed += KeystonePoint_PointerPressed;
                _keystonePointBR.PointerMoved += KeystonePoint_PointerMoved;
                _keystonePointBR.PointerReleased += KeystonePoint_PointerReleased;
            }
            if (_keystonePointBL != null)
            {
                _keystonePointBL.PointerPressed += KeystonePoint_PointerPressed;
                _keystonePointBL.PointerMoved += KeystonePoint_PointerMoved;
                _keystonePointBL.PointerReleased += KeystonePoint_PointerReleased;
            }
        }

        private void KeystoneCorrection_Click(object? sender, RoutedEventArgs e)
        {
            MoreMenuPopup.IsOpen = false;
            EnterKeystoneCorrectionMode();
        }

        private void EnterKeystoneCorrectionMode()
        {
            _isKeystoneCorrectionMode = true;

            // 将 KeystoneButtonGroup 移动到中间列并居中
            if (_keystoneButtonGroup != null)
            {
                Grid.SetColumn(_keystoneButtonGroup, 1);
                _keystoneButtonGroup.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Center;
                _keystoneButtonGroup.IsVisible = true;
            }

            // 隐藏其他按钮组
            if (_leftButtonGroup != null)
                _leftButtonGroup.IsVisible = false;
            if (_centerButtonGroup != null)
                _centerButtonGroup.IsVisible = false;
            if (_rightButtonGroup != null)
                _rightButtonGroup.IsVisible = false;

            if (_keystoneOverlayCanvas != null)
                _keystoneOverlayCanvas.IsVisible = true;

            // 初始化校正点（原有代码）
            var existingPoints = _cameraService.GetSourcePoints();
            if (existingPoints != null && existingPoints.Length == 4)
            {
                for (int i = 0; i < 4; i++)
                {
                    _keystonePoints[i] = existingPoints[i];
                    _originalKeystonePoints[i] = existingPoints[i];
                }
                _cameraService.ResetPerspectiveTransform();
            }
            else
            {
                var defaultPoints = _cameraService.GetDefaultSourcePoints(_videoWidth, _videoHeight);
                for (int i = 0; i < 4; i++)
                {
                    _keystonePoints[i] = defaultPoints[i];
                    _originalKeystonePoints[i] = defaultPoints[i];
                }
            }

            UpdateKeystonePointsDisplay();
            UpdateKeystonePolygon();
        }

        private void ExitKeystoneCorrectionMode()
        {
            _isKeystoneCorrectionMode = false;

            // 将 KeystoneButtonGroup 移回左侧列并左对齐
            if (_keystoneButtonGroup != null)
            {
                Grid.SetColumn(_keystoneButtonGroup, 0);
                _keystoneButtonGroup.HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left;
                _keystoneButtonGroup.IsVisible = false;
            }

            // 恢复其他按钮组
            if (_leftButtonGroup != null)
                _leftButtonGroup.IsVisible = true;
            if (_centerButtonGroup != null)
                _centerButtonGroup.IsVisible = true;
            if (_rightButtonGroup != null)
                _rightButtonGroup.IsVisible = true;

            if (_keystoneOverlayCanvas != null)
                _keystoneOverlayCanvas.IsVisible = false;
        }

        private void ConfirmKeystone_Click(object? sender, RoutedEventArgs e)
        {
            var destPoints = _cameraService.GetDefaultDestPoints(_videoWidth, _videoHeight);
            _cameraService.SetPerspectiveTransform(_keystonePoints, destPoints);

            SaveKeystoneSettings();
            ExitKeystoneCorrectionMode();
        }

        private void CancelKeystone_Click(object? sender, RoutedEventArgs e)
        {
            ExitKeystoneCorrectionMode();
        }

        private void ResetKeystone_Click(object? sender, RoutedEventArgs e)
        {
            var defaultPoints = _cameraService.GetDefaultSourcePoints(_videoWidth, _videoHeight);
            for (int i = 0; i < 4; i++)
            {
                _keystonePoints[i] = defaultPoints[i];
            }
            UpdateKeystonePointsDisplay();
            UpdateKeystonePolygon();
        }

        private void ClearKeystone_Click(object? sender, RoutedEventArgs e)
        {
            MoreMenuPopup.IsOpen = false;
            _cameraService.ResetPerspectiveTransform();

            var config = Config.Load();
            var cameraIndex = _cameraService.CurrentCameraIndex;
            if (config.CameraKeystoneSettings.ContainsKey(cameraIndex))
            {
                config.CameraKeystoneSettings.Remove(cameraIndex);
                config.Save();
            }
        }

        private void SaveKeystoneSettings()
        {
            var config = Config.Load();
            var cameraIndex = _cameraService.CurrentCameraIndex;

            config.CameraKeystoneSettings[cameraIndex] = new KeystonePoints
            {
                TLX = _keystonePoints[0].X,
                TLY = _keystonePoints[0].Y,
                TRX = _keystonePoints[1].X,
                TRY = _keystonePoints[1].Y,
                BRX = _keystonePoints[2].X,
                BRY = _keystonePoints[2].Y,
                BLX = _keystonePoints[3].X,
                BLY = _keystonePoints[3].Y
            };

            config.Save();
        }

        private void LoadKeystoneSettings()
        {
            var config = Config.Load();
            var cameraIndex = _cameraService.CurrentCameraIndex;

            if (config.CameraKeystoneSettings.TryGetValue(cameraIndex, out var points))
            {
                var sourcePoints = new OpenCvSharp.Point2f[]
                {
                    new OpenCvSharp.Point2f(points.TLX, points.TLY),
                    new OpenCvSharp.Point2f(points.TRX, points.TRY),
                    new OpenCvSharp.Point2f(points.BRX, points.BRY),
                    new OpenCvSharp.Point2f(points.BLX, points.BLY)
                };

                var destPoints = _cameraService.GetDefaultDestPoints(_videoWidth, _videoHeight);
                _cameraService.SetPerspectiveTransform(sourcePoints, destPoints);
            }
        }

        private void KeystonePoint_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (sender is Border border && border.Tag != null)
            {
                _isDraggingKeystonePoint = true;
                _draggingKeystonePoint = border;
                var position = e.GetPosition(_keystoneOverlayCanvas);
                var pointIndex = Convert.ToInt32(border.Tag);
                var pointX = _keystonePoints[pointIndex].X * _zoom + _panOffset.X;
                var pointY = _keystonePoints[pointIndex].Y * _zoom + _panOffset.Y;
                _keystonePointOffset = new Point(position.X - pointX, position.Y - pointY);
                e.Pointer.Capture(border);
                e.Handled = true;
            }
        }

        private void KeystonePoint_PointerMoved(object? sender, PointerEventArgs e)
        {
            if (!_isDraggingKeystonePoint || _draggingKeystonePoint == null || _keystoneOverlayCanvas == null)
                return;

            var position = e.GetPosition(_keystoneOverlayCanvas);
            var pointIndex = Convert.ToInt32(_draggingKeystonePoint.Tag);

            var videoX = (position.X - _keystonePointOffset.X - _panOffset.X) / _zoom;
            var videoY = (position.Y - _keystonePointOffset.Y - _panOffset.Y) / _zoom;

            videoX = Math.Clamp(videoX, 0, _videoWidth);
            videoY = Math.Clamp(videoY, 0, _videoHeight);

            _keystonePoints[pointIndex] = new OpenCvSharp.Point2f((float)videoX, (float)videoY);

            UpdateKeystonePointsDisplay();
            UpdateKeystonePolygon();
            e.Handled = true;
        }

        private void KeystonePoint_PointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (_isDraggingKeystonePoint)
            {
                _isDraggingKeystonePoint = false;
                if (_draggingKeystonePoint != null)
                {
                    e.Pointer.Capture(null);
                }
                _draggingKeystonePoint = null;
                e.Handled = true;
            }
        }

        private void UpdateKeystonePointsDisplay()
        {
            if (_keystonePointTL == null || _keystonePointTR == null ||
                _keystonePointBR == null || _keystonePointBL == null ||
                _keystoneOverlayCanvas == null)
                return;

            var offsetX = -12;
            var offsetY = -12;

            Canvas.SetLeft(_keystonePointTL, _keystonePoints[0].X * _zoom + _panOffset.X + offsetX);
            Canvas.SetTop(_keystonePointTL, _keystonePoints[0].Y * _zoom + _panOffset.Y + offsetY);

            Canvas.SetLeft(_keystonePointTR, _keystonePoints[1].X * _zoom + _panOffset.X + offsetX);
            Canvas.SetTop(_keystonePointTR, _keystonePoints[1].Y * _zoom + _panOffset.Y + offsetY);

            Canvas.SetLeft(_keystonePointBR, _keystonePoints[2].X * _zoom + _panOffset.X + offsetX);
            Canvas.SetTop(_keystonePointBR, _keystonePoints[2].Y * _zoom + _panOffset.Y + offsetY);

            Canvas.SetLeft(_keystonePointBL, _keystonePoints[3].X * _zoom + _panOffset.X + offsetX);
            Canvas.SetTop(_keystonePointBL, _keystonePoints[3].Y * _zoom + _panOffset.Y + offsetY);
        }

        private void UpdateKeystonePolygon()
        {
            if (_keystonePolygon == null)
                return;

            var points = new List<Point>
            {
                new Point(_keystonePoints[0].X * _zoom + _panOffset.X, _keystonePoints[0].Y * _zoom + _panOffset.Y),
                new Point(_keystonePoints[1].X * _zoom + _panOffset.X, _keystonePoints[1].Y * _zoom + _panOffset.Y),
                new Point(_keystonePoints[2].X * _zoom + _panOffset.X, _keystonePoints[2].Y * _zoom + _panOffset.Y),
                new Point(_keystonePoints[3].X * _zoom + _panOffset.X, _keystonePoints[3].Y * _zoom + _panOffset.Y)
            };

            _keystonePolygon.Points = new Points(points);
        }

        #endregion

        private void OnThemeChanged(ThemeType theme)
        {
            UpdateButtonGroupStyles();
        }

        private void UpdateButtonGroupStyles()
        {
            var colors = ThemeManager.CurrentColors;

            UpdateButtonGroupShadow(_leftButtonGroup, colors.ShowButtonShadow);
            UpdateButtonGroupShadow(_keystoneButtonGroup, colors.ShowButtonShadow);
            UpdateButtonGroupShadow(_centerButtonGroup, colors.ShowButtonShadow);
            UpdateButtonGroupShadow(_rightButtonGroup, colors.ShowButtonShadow);

            UpdateButtonGroupBackground(_leftButtonGroup, colors.ShowButtonGroupBackground);
            UpdateButtonGroupBackground(_keystoneButtonGroup, colors.ShowButtonGroupBackground);
            UpdateButtonGroupBackground(_centerButtonGroup, colors.ShowButtonGroupBackground);
            UpdateButtonGroupBackground(_rightButtonGroup, colors.ShowButtonGroupBackground);

            UpdateButtonTextVisibility(_leftButtonGroup, colors.ShowButtonText);
            UpdateButtonTextVisibility(_keystoneButtonGroup, colors.ShowButtonText);
            UpdateButtonTextVisibility(_centerButtonGroup, colors.ShowButtonText);
            UpdateButtonTextVisibility(_rightButtonGroup, colors.ShowButtonText);

            UpdateButtonIconSizes(_leftButtonGroup, colors.IconSize, colors.ButtonSize);
            UpdateButtonIconSizes(_keystoneButtonGroup, colors.IconSize, colors.ButtonSize);
            UpdateButtonIconSizes(_centerButtonGroup, colors.IconSize, colors.ButtonSize);
            UpdateButtonIconSizes(_rightButtonGroup, colors.IconSize, colors.ButtonSize);

            if (_toolSliderBackground != null)
            {
                _toolSliderBackground.Width = colors.SliderIndicatorSize;
                _toolSliderBackground.Height = colors.SliderIndicatorSize;
            }

            UpdateButtonGroupPadding(_leftButtonGroup, colors.ButtonGroupPadding);
            UpdateButtonGroupPadding(_keystoneButtonGroup, colors.ButtonGroupPadding);
            UpdateButtonGroupPadding(_centerButtonGroup, colors.ButtonGroupPadding);
            UpdateButtonGroupPadding(_rightButtonGroup, colors.ButtonGroupPadding);
        }

        private void UpdateButtonGroupShadow(Border? border, bool showShadow)
        {
            if (border == null) return;

            if (showShadow)
            {
                var shadowColor = ThemeManager.CurrentTheme == ThemeType.NoBackground
                    ? Color.Parse("#66808080")
                    : Color.Parse("#33000000");

                var shadowBlur = ThemeManager.CurrentTheme == ThemeType.NoBackground ? 16 : 12;
                var shadowOffsetY = ThemeManager.CurrentTheme == ThemeType.NoBackground ? 6 : 4;

                border.BoxShadow = new BoxShadows(new BoxShadow
                {
                    Color = shadowColor,
                    OffsetX = 0,
                    OffsetY = shadowOffsetY,
                    Blur = shadowBlur,
                    Spread = 0
                });
            }
            else
            {
                border.BoxShadow = default;
            }
        }

        private void UpdateButtonGroupBackground(Border? border, bool showBackground)
        {
            if (border == null) return;

            if (showBackground)
            {
                border.Classes.Remove("no-background");

                var toggleButtons = border.GetLogicalDescendants().OfType<ToggleButton>();
                foreach (var btn in toggleButtons)
                {
                    btn.Classes.Remove("no-background");
                }
            }
            else
            {
                border.Classes.Add("no-background");

                var toggleButtons = border.GetLogicalDescendants().OfType<ToggleButton>();
                foreach (var btn in toggleButtons)
                {
                    btn.Classes.Add("no-background");
                }
            }
        }

        private void UpdateButtonTextVisibility(Border? border, bool showText)
        {
            if (border == null) return;
            var textBlocks = border.GetLogicalDescendants().OfType<TextBlock>()
                .Where(tb => tb.Classes.Contains("button-text-label") || tb.FontSize == 10 || tb.FontSize == 9);
            foreach (var tb in textBlocks)
            {
                tb.IsVisible = showText;
            }
        }

        private void UpdateIconAlignment(Border? border, bool showText)
        {
            if (border == null) return;

            var buttons = border.GetLogicalDescendants().OfType<Button>();
            var toggleButtons = border.GetLogicalDescendants().OfType<ToggleButton>();

            foreach (var btn in buttons.Concat(toggleButtons))
            {
                var grid = btn.GetLogicalDescendants().OfType<Grid>().FirstOrDefault();
                if (grid == null) continue;

                var viewbox = grid.GetLogicalDescendants().OfType<Viewbox>().FirstOrDefault();
                if (viewbox == null) continue;

                if (showText)
                {
                    viewbox.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top;
                    viewbox.Margin = new Thickness(0, 4, 0, 0);
                }
                else
                {
                    viewbox.VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center;
                    viewbox.Margin = new Thickness(0);
                }
            }
        }

        private void UpdateButtonIconSizes(Border? border, double iconSize, double buttonSize)
        {
            if (border == null) return;

            var buttons = border.GetLogicalDescendants().OfType<Button>();
            var toggleButtons = border.GetLogicalDescendants().OfType<ToggleButton>();

            foreach (var btn in buttons)
            {
                btn.Width = buttonSize;
                btn.Height = buttonSize;

                var viewbox = btn.GetLogicalDescendants().OfType<Viewbox>().FirstOrDefault();
                if (viewbox != null)
                {
                    viewbox.Width = iconSize;
                    viewbox.Height = iconSize;
                }
            }

            foreach (var btn in toggleButtons)
            {
                btn.Width = buttonSize;
                btn.Height = buttonSize;

                var viewbox = btn.GetLogicalDescendants().OfType<Viewbox>().FirstOrDefault();
                if (viewbox != null)
                {
                    viewbox.Width = iconSize;
                    viewbox.Height = iconSize;
                }
            }
        }

        private void UpdateButtonGroupPadding(Border? border, double padding)
        {
            if (border == null) return;

            border.Padding = new Thickness(padding, 4, padding, 4);
        }
    }

    public class PhotoItem : System.ComponentModel.INotifyPropertyChanged
    {
        private bool _isSelected;
        private bool _isDeleting;
        private bool _isChecked;

        public int Index { get; set; }
        public string? FilePath { get; set; }
        public DateTime Timestamp { get; set; }
        public Bitmap? Thumbnail { get; set; }
        public List<InkStroke> Strokes { get; set; } = new();

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(IsSelected)));
                }
            }
        }

        public bool IsDeleting
        {
            get => _isDeleting;
            set
            {
                if (_isDeleting != value)
                {
                    _isDeleting = value;
                    PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(IsDeleting)));
                }
            }
        }

        public bool IsChecked
        {
            get => _isChecked;
            set
            {
                if (_isChecked != value)
                {
                    _isChecked = value;
                    PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(IsChecked)));
                }
            }
        }

        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;
    }

    public class SelectedBackgroundConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool isSelected && isSelected)
                return new SolidColorBrush(Color.FromRgb(0, 120, 212));
            return new SolidColorBrush(Color.FromRgb(61, 61, 61));
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class SelectedForegroundConverter : IValueConverter
    {
        public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            if (value is bool isSelected && isSelected)
                return Brushes.White;
            return new SolidColorBrush(Color.FromRgb(170, 170, 170));
        }

        public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }

    public class WhiteboardPageThumbnail : INotifyPropertyChanged
    {
        private int _pageNumber;
        private Bitmap? _thumbnail;
        private bool _isSelected;

        public int PageNumber
        {
            get => _pageNumber;
            set
            {
                if (_pageNumber != value)
                {
                    _pageNumber = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PageNumber)));
                }
            }
        }

        public Bitmap? Thumbnail
        {
            get => _thumbnail;
            set
            {
                if (_thumbnail != value)
                {
                    _thumbnail = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Thumbnail)));
                }
            }
        }

        public bool IsSelected
        {
            get => _isSelected;
            set
            {
                if (_isSelected != value)
                {
                    _isSelected = value;
                    PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
