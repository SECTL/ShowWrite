using ShowWrite.Models;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Ink;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Image = System.Windows.Controls.Image;
using ListBox = System.Windows.Controls.ListBox;
using Orientation = System.Windows.Controls.Orientation;

namespace ShowWrite.Services
{
    /// <summary>
    /// 照片悬浮窗管理器
    /// 负责管理照片悬浮窗的显示、定位、数据绑定等所有逻辑
    /// </summary>
    public class PhotoPopupManager : System.ComponentModel.INotifyPropertyChanged
    {
        private readonly Popup _photoPopup;
        private readonly System.Windows.Controls.ListBox _photoList;
        private readonly Window _mainWindow;
        private readonly ObservableCollection<PhotoWithStrokes> _photos;
        private readonly DrawingManager _drawingManager;
        private readonly CameraManager _cameraManager;
        private readonly MemoryManager _memoryManager;
        private readonly FrameProcessor _frameProcessor;
        private readonly PanZoomManager _panZoomManager;
        private readonly LogManager _logManager;

        // 常量定义
        private const double BottomToolbarHeight = 70; // 底部工具栏高度
        private const double PopupMargin = 10; // 悬浮窗边距
        private const double PopupWidth = 400; // 悬浮窗宽度
        private const double PopupHeight = 500; // 悬浮窗高度

        // 事件
        public event Action<PhotoWithStrokes> PhotoSelected;
        public event Action BackToLiveRequested;
        public event Action SaveImageRequested;
        public event Action PopupOpened;
        public event Action PopupClosed;

        // 状态
        private PhotoWithStrokes _currentPhoto;
        private StrokeCollection _liveStrokes;
        private bool _isLiveMode = true;

        /// <summary>
        /// 当前选中的照片
        /// </summary>
        public PhotoWithStrokes CurrentPhoto
        {
            get => _currentPhoto;
            set
            {
                if (_currentPhoto != value)
                {
                    _currentPhoto = value;
                    OnPropertyChanged(nameof(CurrentPhoto));
                }
            }
        }

        /// <summary>
        /// 是否处于实时模式
        /// </summary>
        public bool IsLiveMode
        {
            get => _isLiveMode;
            set => _isLiveMode = value;
        }

        // 9个参数的构造函数
        public PhotoPopupManager(
            Popup photoPopup,
            ListBox photoList,
            Window mainWindow,
            ObservableCollection<PhotoWithStrokes> photos,
            DrawingManager drawingManager,
            CameraManager cameraManager,
            MemoryManager memoryManager,
            FrameProcessor frameProcessor,
            PanZoomManager panZoomManager)
            : this(photoPopup, photoList, mainWindow, photos, drawingManager, cameraManager, memoryManager, frameProcessor, panZoomManager, null)
        {
        }

        // 10个参数的构造函数
        public PhotoPopupManager(
            Popup photoPopup,
            ListBox photoList,
            Window mainWindow,
            ObservableCollection<PhotoWithStrokes> photos,
            DrawingManager drawingManager,
            CameraManager cameraManager,
            MemoryManager memoryManager,
            FrameProcessor frameProcessor,
            PanZoomManager panZoomManager,
            LogManager logManager)
        {
            _photoPopup = photoPopup;
            _photoList = photoList ?? throw new ArgumentNullException(nameof(photoList));
            _mainWindow = mainWindow ?? throw new ArgumentNullException(nameof(mainWindow));
            _photos = photos ?? throw new ArgumentNullException(nameof(photos));
            _drawingManager = drawingManager ?? throw new ArgumentNullException(nameof(drawingManager));
            _cameraManager = cameraManager ?? throw new ArgumentNullException(nameof(cameraManager));
            _memoryManager = memoryManager ?? throw new ArgumentNullException(nameof(memoryManager));
            _frameProcessor = frameProcessor ?? throw new ArgumentNullException(nameof(frameProcessor));
            _panZoomManager = panZoomManager ?? throw new ArgumentNullException(nameof(panZoomManager));
            _logManager = logManager;

            Initialize();
        }

        /// <summary>
        /// 初始化照片悬浮窗
        /// </summary>
        private void Initialize()
        {
            try
            {
                // 绑定数据源
                _photoList.ItemsSource = _photos;

                // 设置 AlternationCount 以支持编号显示
                _photoList.AlternationCount = int.MaxValue;

                // 初始化照片索引
                UpdatePhotoIndexes();

                // 订阅事件
                _photoList.SelectionChanged += PhotoList_SelectionChanged;
                
                if (_photoPopup != null)
                {
                    _photoPopup.Opened += PhotoPopup_Opened;
                    _photoPopup.Closed += PhotoPopup_Closed;
                }

                // 初始化实时模式笔迹
                _liveStrokes = new StrokeCollection(_drawingManager.GetStrokes());

                Logger.Info("PhotoPopupManager", "照片悬浮窗初始化完成");
            }
            catch (Exception ex)
            {
                Logger.Error("PhotoPopupManager", $"初始化失败: {ex.Message}", ex);
                throw;
            }
        }

        /// <summary>
        /// 显示照片悬浮窗
        /// </summary>
        public void ShowPhotoPopup()
        {
            try
            {
                // 重新定位悬浮窗
                RepositionPhotoPopup();

                // 打开悬浮窗
                _photoPopup.IsOpen = true;

                // 更新列表显示
                UpdatePhotoListDisplay();

                // 监听窗口大小变化，以便在窗口大小改变时重新定位
                _mainWindow.SizeChanged += MainWindow_SizeChanged;

                Logger.Debug("PhotoPopupManager", "显示照片悬浮窗");
            }
            catch (Exception ex)
            {
                Logger.Error("PhotoPopupManager", $"显示悬浮窗失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 隐藏照片悬浮窗
        /// </summary>
        public void HidePhotoPopup()
        {
            try
            {
                _photoPopup.IsOpen = false;

                // 移除窗口大小变化监听
                _mainWindow.SizeChanged -= MainWindow_SizeChanged;

                Logger.Debug("PhotoPopupManager", "隐藏照片悬浮窗");
            }
            catch (Exception ex)
            {
                Logger.Error("PhotoPopupManager", $"隐藏悬浮窗失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 切换照片悬浮窗显示状态
        /// </summary>
        public void TogglePhotoPopup()
        {
            if (_photoPopup.IsOpen)
            {
                HidePhotoPopup();
            }
            else
            {
                ShowPhotoPopup();
            }
        }

        /// <summary>
        /// 重新定位照片悬浮窗（贴右侧放置）
        /// </summary>
        public void RepositionPhotoPopup()
        {
            if (_photoPopup == null) return;

            try
            {
                // 获取主窗口的尺寸和位置
                double mainWindowWidth = _mainWindow.ActualWidth;
                double mainWindowHeight = _mainWindow.ActualHeight;

                // 获取 Popup 的实际高度（Border 的高度）
                double popupHeight = PopupHeight; // 默认值
                if (_photoPopup.Child is System.Windows.Controls.Border border)
                {
                    popupHeight = border.Height;
                }

                // 计算位置：贴右侧放置
                double left = mainWindowWidth - PopupWidth - PopupMargin;

                // 减去底部工具栏高度，确保悬浮窗不会覆盖工具栏
                double top = mainWindowHeight - popupHeight - BottomToolbarHeight - PopupMargin;

                // 确保位置在屏幕范围内
                left = Math.Max(PopupMargin, left);
                top = Math.Max(PopupMargin, top);

                // 设置悬浮窗位置
                _photoPopup.HorizontalOffset = left;
                _photoPopup.VerticalOffset = top;

                // 调试信息
                Logger.Debug("PhotoPopupManager",
                    $"照片悬浮窗定位到: ({left:F0}, {top:F0}), " +
                    $"主窗口尺寸: ({mainWindowWidth:F0}x{mainWindowHeight:F0}), " +
                    $"悬浮窗高度: {popupHeight:F0}, " +
                    $"避开了底部工具栏高度: {BottomToolbarHeight}");
            }
            catch (Exception ex)
            {
                Logger.Error("PhotoPopupManager", $"重新定位照片悬浮窗失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 添加新照片（修改版：创建缩略图）
        /// </summary>
        /// <param name="image">照片图像</param>
        /// <param name="strokes">笔迹</param>
        public void AddPhoto(BitmapSource image, StrokeCollection strokes)
        {
            try
            {
                if (image == null)
                {
                    Logger.Warning("PhotoPopupManager", "添加照片失败：image 为 null");
                    return;
                }

                // 确保在 UI 线程上创建图像
                if (!System.Windows.Application.Current.Dispatcher.CheckAccess())
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                        AddPhoto(image, strokes));
                    return;
                }

                // 冻结图像以确保线程安全
                if (!image.IsFrozen && image.CanFreeze)
                {
                    image.Freeze();
                }

                // 创建缩略图
                BitmapSource thumbnail = CreateThumbnail(image, 120, 90);

                var capturedImage = new CapturedImage(image);
                var photo = new PhotoWithStrokes(capturedImage);

                // 检查是否成功创建了 photo
                if (photo == null)
                {
                    Logger.Error("PhotoPopupManager", "创建 PhotoWithStrokes 失败");
                    return;
                }

                // 设置图像和缩略图
                photo.Image = image;
                photo.Thumbnail = thumbnail;
                photo.Strokes = strokes != null ? new StrokeCollection(strokes) : new StrokeCollection();

                // 添加到列表开头
                _photos.Insert(0, photo);
                CurrentPhoto = photo;

                // 更新所有照片的索引
                UpdatePhotoIndexes();

                // 更新列表显示
                UpdatePhotoListDisplay();

                // 清理内存
                _memoryManager?.TriggerMemoryCleanup();

                Logger.Info("PhotoPopupManager", "添加新照片成功");
            }
            catch (Exception ex)
            {
                Logger.Error("PhotoPopupManager", $"添加照片失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 创建缩略图
        /// </summary>
        private BitmapSource CreateThumbnail(BitmapSource source, int width, int height)
        {
            try
            {
                // 增加空值检查
                if (source == null)
                {
                    Logger.Warning("PhotoPopupManager", "无法创建缩略图：source 为 null");
                    // 返回一个默认的空白图像
                    return CreateDefaultThumbnail(width, height);
                }

                var scaleX = (double)width / source.PixelWidth;
                var scaleY = (double)height / source.PixelHeight;
                var scale = Math.Min(scaleX, scaleY);
                var scaledWidth = (int)(source.PixelWidth * scale);
                var scaledHeight = (int)(source.PixelHeight * scale);
                var thumbnail = new TransformedBitmap(source,
                    new ScaleTransform(scale, scale));
                var result = new CroppedBitmap(thumbnail,
                    new Int32Rect((thumbnail.PixelWidth - scaledWidth) / 2,
                                 (thumbnail.PixelHeight - scaledHeight) / 2,
                                 scaledWidth, scaledHeight));
                result.Freeze();
                return result;
            }
            catch (Exception ex)
            {
                Logger.Error("PhotoPopupManager", $"创建缩略图失败: {ex.Message}", ex);
                return CreateDefaultThumbnail(width, height);
            }
        }

        /// <summary>
        /// 创建默认缩略图（用于错误情况）
        /// </summary>
        private BitmapSource CreateDefaultThumbnail(int width, int height)
        {
            try
            {
                var drawingVisual = new DrawingVisual();
                using (var drawingContext = drawingVisual.RenderOpen())
                {
                    drawingContext.DrawRectangle(
                        System.Windows.Media.Brushes.LightGray,
                        null,
                        new Rect(0, 0, width, height));
                }
                var renderBitmap = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
                renderBitmap.Render(drawingVisual);
                renderBitmap.Freeze();
                return renderBitmap;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 更新所有照片的索引
        /// </summary>
        public void UpdatePhotoIndexes()
        {
            for (int i = 0; i < _photos.Count; i++)
            {
                _photos[i].Index = i + 1;
            }
        }

        /// <summary>
        /// 选择照片查看
        /// </summary>
        /// <param name="photoWithStrokes">要查看的照片</param>
        public void SelectPhotoForViewing(PhotoWithStrokes photoWithStrokes)
        {
            try
            {
                if (photoWithStrokes == null)
                {
                    Logger.Warning("PhotoPopupManager", "尝试选择 null 的照片");
                    return;
                }

                // 检查照片是否有效
                if (photoWithStrokes.Image == null || photoWithStrokes.Image == null)
                {
                    Logger.Warning("PhotoPopupManager", "选择的照片图像为空");
                    return;
                }

                // 检查是否点击了已选中的照片
                if (_currentPhoto != null && _currentPhoto == photoWithStrokes && !_isLiveMode)
                {
                    // 再次点击已选中的照片，返回实时模式
                    BackToLive();
                    return;
                }

                Logger.Info("PhotoPopupManager", "选择照片查看模式");

                // 保存当前实时模式的笔迹
                _liveStrokes = new StrokeCollection(_drawingManager.GetStrokes());
                _isLiveMode = false;
                CurrentPhoto = photoWithStrokes;
                
                // 设置列表选中状态
                if (_photoList != null)
                {
                    _photoList.SelectedItem = photoWithStrokes;
                }

                // 触发照片选择事件 - 传递完整的 photoWithStrokes 对象
                PhotoSelected?.Invoke(photoWithStrokes);

                // 切换绘制管理器的StrokeCollection到照片的笔迹
                _drawingManager.SwitchToPhotoStrokes(photoWithStrokes.Strokes);

                // 重置缩放状态
                _panZoomManager.ResetZoom();

                // 停止摄像头并释放资源
                _cameraManager.PauseCamera();
                _cameraManager.ReleaseCameraResources();

                // 触发GC释放旧资源
                _memoryManager.TriggerMemoryCleanup();

                // 更新列表显示
                UpdatePhotoListDisplay();
            }
            catch (Exception ex)
            {
                Logger.Error("PhotoPopupManager", $"选择照片查看失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 返回实时模式
        /// </summary>
        public void BackToLive()
        {
            try
            {
                Logger.Info("PhotoPopupManager", "返回实时模式");

                // 保存当前照片的笔迹
                if (_currentPhoto != null)
                {
                    _currentPhoto.Strokes = new StrokeCollection(_drawingManager.GetStrokes());
                }

                _isLiveMode = true;
                CurrentPhoto = null;

                // 清空照片列表选中项
                _photoList.SelectedItem = null;

                // 触发返回实时模式事件
                BackToLiveRequested?.Invoke();

                // 切换回实时模式的笔迹
                _drawingManager.SwitchToPhotoStrokes(_liveStrokes);

                // 重置缩放
                _panZoomManager.ResetZoom();

                // 重新启动摄像头
                _cameraManager.RestartCamera();

                // 内存清理
                _memoryManager.TriggerMemoryCleanup();

                // 更新照片列表显示
                UpdatePhotoListDisplay();

                Logger.Info("PhotoPopupManager", "已返回实时模式");
            }
            catch (Exception ex)
            {
                Logger.Error("PhotoPopupManager", $"返回实时模式失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 保存当前照片
        /// </summary>
        public void SaveCurrentPhoto()
        {
            try
            {
                if (_currentPhoto == null)
                {
                    Logger.Warning("PhotoPopupManager", "没有当前照片可保存");
                    return;
                }

                Logger.Info("PhotoPopupManager", "开始保存图片");

                // 触发保存图片事件
                SaveImageRequested?.Invoke();
            }
            catch (Exception ex)
            {
                Logger.Error("PhotoPopupManager", $"保存当前照片失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 获取所有照片
        /// </summary>
        public ObservableCollection<PhotoWithStrokes> GetPhotos()
        {
            return _photos;
        }

        /// <summary>
        /// 更新照片列表显示
        /// </summary>
        public void UpdatePhotoListDisplay()
        {
            try
            {
                if (_photoList != null)
                {
                    _photoList.Items.Refresh();
                }
            }
            catch (Exception ex)
            {
                Logger.Error("PhotoPopupManager", $"更新照片列表显示失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 创建照片列表项模板（修改版：移除"当前查看中"提示）
        /// </summary>
        private DataTemplate CreatePhotoListItemTemplate()
        {
            var dataTemplate = new DataTemplate();

            // 创建主框架
            var mainStackFactory = new FrameworkElementFactory(typeof(StackPanel));
            mainStackFactory.SetValue(StackPanel.OrientationProperty, Orientation.Horizontal);
            mainStackFactory.SetValue(StackPanel.MarginProperty, new Thickness(6));
            mainStackFactory.AddHandler(StackPanel.MouseLeftButtonDownEvent, new MouseButtonEventHandler(PhotoItem_MouseLeftButtonDown));

            // 创建编号
            var numberFactory = new FrameworkElementFactory(typeof(TextBlock));
            numberFactory.SetValue(TextBlock.WidthProperty, 30.0);
            numberFactory.SetValue(TextBlock.TextAlignmentProperty, TextAlignment.Center);
            numberFactory.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
            numberFactory.SetValue(TextBlock.ForegroundProperty, System.Windows.Media.Brushes.White);
            numberFactory.SetValue(TextBlock.FontSizeProperty, 14.0);
            numberFactory.SetValue(TextBlock.FontWeightProperty, FontWeights.Bold);
            numberFactory.SetBinding(TextBlock.TextProperty, new System.Windows.Data.Binding("Index"));

            // 创建缩略图容器（Grid）
            var gridFactory = new FrameworkElementFactory(typeof(Grid));
            gridFactory.SetValue(Grid.WidthProperty, 120.0);
            gridFactory.SetValue(Grid.HeightProperty, 90.0);

            // 创建缩略图
            var imageFactory = new FrameworkElementFactory(typeof(Image));
            imageFactory.SetBinding(Image.SourceProperty, new System.Windows.Data.Binding("Thumbnail"));
            imageFactory.SetValue(Image.StretchProperty, System.Windows.Media.Stretch.UniformToFill);

            // 创建遮罩层
            var overlayFactory = new FrameworkElementFactory(typeof(Border));
            overlayFactory.SetValue(Border.BackgroundProperty, new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromArgb(180, 0, 0, 0)));
            overlayFactory.SetValue(Border.VisibilityProperty, Visibility.Collapsed);
            overlayFactory.SetBinding(Border.VisibilityProperty, new System.Windows.Data.Binding("IsSelected")
            {
                RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.FindAncestor, typeof(ListBoxItem), 1),
                Converter = new BooleanToVisibilityConverter()
            });

            // 创建遮罩层文字
            var overlayTextFactory = new FrameworkElementFactory(typeof(TextBlock));
            overlayTextFactory.SetValue(TextBlock.TextProperty, "再次点击\n返回直播");
            overlayTextFactory.SetValue(TextBlock.ForegroundProperty, System.Windows.Media.Brushes.White);
            overlayTextFactory.SetValue(TextBlock.FontSizeProperty, 12.0);
            overlayTextFactory.SetValue(TextBlock.FontWeightProperty, FontWeights.Bold);
            overlayTextFactory.SetValue(TextBlock.TextAlignmentProperty, TextAlignment.Center);
            overlayTextFactory.SetValue(TextBlock.VerticalAlignmentProperty, VerticalAlignment.Center);
            overlayTextFactory.SetValue(TextBlock.HorizontalAlignmentProperty, System.Windows.HorizontalAlignment.Center);

            // 组合遮罩层
            overlayFactory.AppendChild(overlayTextFactory);

            // 组合 Grid（缩略图 + 遮罩层）
            gridFactory.AppendChild(imageFactory);
            gridFactory.AppendChild(overlayFactory);

            // 创建复选框
            var checkBoxFactory = new FrameworkElementFactory(typeof(System.Windows.Controls.CheckBox));
            checkBoxFactory.SetValue(FrameworkElement.WidthProperty, 24.0);
            checkBoxFactory.SetValue(FrameworkElement.HeightProperty, 24.0);
            checkBoxFactory.SetValue(FrameworkElement.MarginProperty, new Thickness(8, 0, 8, 0));
            checkBoxFactory.SetValue(FrameworkElement.VerticalAlignmentProperty, VerticalAlignment.Center);
            checkBoxFactory.SetBinding(ToggleButton.IsCheckedProperty, new System.Windows.Data.Binding("IsSelected")
            {
                Mode = System.Windows.Data.BindingMode.TwoWay
            });
            checkBoxFactory.SetBinding(UIElement.VisibilityProperty, new System.Windows.Data.Binding("Tag")
            {
                RelativeSource = new System.Windows.Data.RelativeSource(System.Windows.Data.RelativeSourceMode.FindAncestor, typeof(ListBox), 1),
                Converter = new StringToVisibilityConverter()
            });

            // 组合主面板（编号 + 缩略图容器 + 复选框）
            mainStackFactory.AppendChild(numberFactory);
            mainStackFactory.AppendChild(gridFactory);
            mainStackFactory.AppendChild(checkBoxFactory);

            dataTemplate.VisualTree = mainStackFactory;
            return dataTemplate;
        }

        #region 事件处理方法

        /// <summary>
        /// 主窗口大小变化事件处理
        /// </summary>
        private void MainWindow_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_photoPopup != null && _photoPopup.IsOpen)
            {
                // 当主窗口大小改变时，重新定位悬浮窗
                RepositionPhotoPopup();
            }
        }

        /// <summary>
        /// 照片悬浮窗打开事件
        /// </summary>
        private void PhotoPopup_Opened(object sender, EventArgs e)
        {
            try
            {
                // 延迟重新定位，确保高度绑定已经生效
                System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(new Action(() =>
                {
                    RepositionPhotoPopup();
                }), System.Windows.Threading.DispatcherPriority.Loaded);

                // 更新列表显示
                UpdatePhotoListDisplay();

                // 触发打开事件
                PopupOpened?.Invoke();

                Logger.Debug("PhotoPopupManager", "照片悬浮窗已打开");
            }
            catch (Exception ex)
            {
                Logger.Error("PhotoPopupManager", $"照片悬浮窗打开事件失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 照片悬浮窗关闭事件
        /// </summary>
        private void PhotoPopup_Closed(object sender, EventArgs e)
        {
            try
            {
                // 不清空选中项，保持选中状态以便下次打开时恢复
                // _photoList.SelectedItem = null;

                // 触发关闭事件
                PopupClosed?.Invoke();

                Logger.Debug("PhotoPopupManager", "照片悬浮窗已关闭");
            }
            catch (Exception ex)
            {
                Logger.Error("PhotoPopupManager", $"照片悬浮窗关闭事件失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 照片列表选择变更事件
        /// </summary>
        private void PhotoList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                // 如果没有选中任何项，直接返回
                if (_photoList.SelectedItem == null)
                    return;

                if (_photoList.SelectedItem is PhotoWithStrokes photoWithStrokes)
                {
                    // 选择照片查看
                    SelectPhotoForViewing(photoWithStrokes);
                }
            }
            catch (Exception ex)
            {
                Logger.Error("PhotoPopupManager", $"照片列表选择变更事件失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 照片项鼠标左键按下事件
        /// </summary>
        private void PhotoItem_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            try
            {
                if (sender is StackPanel stackPanel && stackPanel.DataContext is PhotoWithStrokes photoWithStrokes)
                {
                    // 如果点击的是当前选中的照片，返回实时模式
                    if (_currentPhoto != null && _currentPhoto == photoWithStrokes && !_isLiveMode)
                    {
                        BackToLive();
                        e.Handled = true;
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("PhotoPopupManager", $"照片项鼠标点击事件失败: {ex.Message}", ex);
            }
        }

        #endregion

        #region INotifyPropertyChanged 实现

        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
        }

        #endregion

        #region 清理资源

        /// <summary>
        /// 清理资源
        /// </summary>
        public void Dispose()
        {
            try
            {
                // 取消事件订阅（使用空值条件运算符确保安全）
                _photoList?.SelectionChanged -= PhotoList_SelectionChanged;
                _photoPopup?.Opened -= PhotoPopup_Opened;
                _photoPopup?.Closed -= PhotoPopup_Closed;
                _mainWindow?.SizeChanged -= MainWindow_SizeChanged;

                // 关闭悬浮窗
                _photoPopup?.Dispatcher.Invoke(() =>
                {
                    _photoPopup.IsOpen = false;
                });

                // 清空数据
                _photos?.Clear();

                // 释放引用
                _liveStrokes = null;
                CurrentPhoto = null;

                Logger.Info("PhotoPopupManager", "照片悬浮窗管理器已清理");
            }
            catch (Exception ex)
            {
                Logger.Error("PhotoPopupManager", $"清理资源失败: {ex.Message}", ex);
            }
        }

        #endregion
    }

    /// <summary>
    /// 布尔值到可见性转换器
    /// </summary>
    public class BooleanToVisibilityConverter : System.Windows.Data.IValueConverter
    {
        public object Convert(object value, System.Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is bool isSelected)
            {
                return isSelected ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, System.Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is Visibility visibility)
            {
                return visibility == Visibility.Visible;
            }
            return false;
        }
    }

    /// <summary>
    /// 替代索引转换器 - 将索引转换为从1开始的编号
    /// </summary>
    public class AlternationIndexConverter : System.Windows.Data.IValueConverter
    {
        public object Convert(object value, System.Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is int index)
            {
                return (index + 1).ToString();
            }
            return "0";
        }

        public object ConvertBack(object value, System.Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new System.NotImplementedException();
        }
    }

    /// <summary>
    /// 减法转换器
    /// 用于从屏幕高度中减去指定值（如底部菜单栏高度）
    /// </summary>
    public class SubtractConverter : System.Windows.Data.IValueConverter
    {
        public object Convert(object value, System.Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            try
            {
                if (value is double doubleValue)
                {
                    double doubleParam = 0;
                    if (parameter is double d)
                    {
                        doubleParam = d;
                    }
                    else if (parameter is string s && double.TryParse(s, out double result))
                    {
                        doubleParam = result;
                    }
                    return doubleValue - doubleParam;
                }
                return value;
            }
            catch
            {
                return value;
            }
        }

        public object ConvertBack(object value, System.Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new System.NotImplementedException();
        }
    }

    public class StringToVisibilityConverter : System.Windows.Data.IValueConverter
    {
        public object Convert(object value, System.Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            if (value is string str)
            {
                return str == "Visible" ? Visibility.Visible : Visibility.Collapsed;
            }
            if (value is Visibility vis)
            {
                return vis;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, System.Type targetType, object parameter, System.Globalization.CultureInfo culture)
        {
            throw new System.NotImplementedException();
        }
    }
}
