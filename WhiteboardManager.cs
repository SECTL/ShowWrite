using Aspose.Slides;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using DocumentFormat.OpenXml.Packaging;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using D = DocumentFormat.OpenXml.Drawing;
using IOPath = System.IO.Path;
using P = DocumentFormat.OpenXml.Presentation;

namespace ShowWrite
{
    public class WhiteboardManager : INotifyPropertyChanged, IDisposable
    {
        private bool _isWhiteboardMode = false;
        private Border? _whiteboardBackground;
        private StackPanel? _whiteboardModeButtons;
        private StackPanel? _whiteboardPageButtons;
        private Border? _pagePanel;
        private TextBlock? _pageInfoText;
        private int _whiteboardCurrentPage = 1;
        private int _whiteboardTotalPages = 1;
        private List<List<InkStroke>> _whiteboardPages = new List<List<InkStroke>> { new List<InkStroke>() };
        private List<string?> _pageBackgrounds = new List<string?> { null };
        private Avalonia.Controls.Shapes.Path? _nextPagePath;
        private TextBlock? _nextPageText;
        private bool _pagePanelOpen = false;
        private ObservableCollection<WhiteboardPageThumbnail> _whiteboardPageThumbnails = new();
        private InkCanvas _inkCanvas;
        private CameraService? _cameraService;
        private Image _videoImage;
        private Border? _importingOverlay;
        private List<InkStroke> _videoStrokesBackup = new();
        private bool _hasWhiteboardData = false;

        // AsposeSlidesService fields
        private Aspose.Slides.Presentation? _asposePresentation;
        private int _asposeCurrentIndex;

        // PowerPointService fields
        private PowerPointService? _powerPointService;
        private bool _isPowerPointMode = false;

        public bool IsWhiteboardMode => _isWhiteboardMode;
        public int CurrentPage => _whiteboardCurrentPage;
        public int TotalPages => _whiteboardTotalPages;
        public ObservableCollection<WhiteboardPageThumbnail> WhiteboardPageThumbnails => _whiteboardPageThumbnails;

        // AsposeSlidesService properties
        public bool IsAsposePresentationOpen => _asposePresentation != null;
        public int AsposeSlideCount => _asposePresentation?.Slides.Count ?? 0;
        public int AsposeCurrentIndex => _asposeCurrentIndex;

        // PowerPointService properties
        public bool IsPowerPointMode => _isPowerPointMode;
        public bool IsPowerPointOpen => _powerPointService?.IsOpen ?? false;
        public int PowerPointSlideCount => _powerPointService?.SlideCount ?? 0;
        public int PowerPointCurrentSlide => _powerPointService?.CurrentSlideIndex ?? 1;

        public event PropertyChangedEventHandler? PropertyChanged;
        public event Action? WhiteboardModeEntered;
        public event Action? WhiteboardModeExited;
        public event Action? ImportPptRequested;

        public WhiteboardManager(
            InkCanvas inkCanvas,
            Image videoImage,
            CameraService? cameraService)
        {
            _inkCanvas = inkCanvas;
            _videoImage = videoImage;
            _cameraService = cameraService;
        }

        public void Initialize(
            Border whiteboardBackground,
            StackPanel whiteboardModeButtons,
            StackPanel whiteboardPageButtons,
            Border pagePanel,
            TextBlock pageInfoText,
            Avalonia.Controls.Shapes.Path nextPagePath,
            TextBlock nextPageText,
            Border importingOverlay)
        {
            _whiteboardBackground = whiteboardBackground;
            _whiteboardModeButtons = whiteboardModeButtons;
            _whiteboardPageButtons = whiteboardPageButtons;
            _pagePanel = pagePanel;
            _pageInfoText = pageInfoText;
            _nextPagePath = nextPagePath;
            _nextPageText = nextPageText;
            _importingOverlay = importingOverlay;
        }

        public void EnterWhiteboardMode(
            StackPanel normalBottomButtons,
            Button captureBtn,
            Button scanBtn,
            Button connectDeviceBtn,
            Button pipBtn,
            StackPanel normalRightButtons)
        {
            if (_isWhiteboardMode) return;

            _isWhiteboardMode = true;

            _cameraService?.CancelConnecting();

            _videoImage.IsVisible = false;

            if (_whiteboardBackground != null)
            {
                _whiteboardBackground.IsVisible = true;
            }

            if (normalBottomButtons != null)
            {
                normalBottomButtons.IsVisible = false;
            }

            if (_whiteboardModeButtons != null)
            {
                _whiteboardModeButtons.IsVisible = true;
            }

            if (captureBtn != null) captureBtn.IsVisible = false;
            if (scanBtn != null) scanBtn.IsVisible = false;
            if (connectDeviceBtn != null) connectDeviceBtn.IsVisible = false;
            if (pipBtn != null) pipBtn.IsVisible = false;
            if (normalRightButtons != null) normalRightButtons.IsVisible = false;

            if (_whiteboardPageButtons != null)
            {
                _whiteboardPageButtons.IsVisible = true;
            }

            _videoStrokesBackup = _inkCanvas.GetStrokes();

            if (!_hasWhiteboardData)
            {
                _whiteboardPages = new List<List<InkStroke>> { new List<InkStroke>() };
                _pageBackgrounds = new List<string?> { null };
                _whiteboardCurrentPage = 1;
                _whiteboardTotalPages = 1;
                _inkCanvas.ClearStrokes();
                _inkCanvas.SetWhiteboardBackground(null);
            }
            else
            {
                if (_whiteboardCurrentPage - 1 < _whiteboardPages.Count)
                {
                    _inkCanvas.SetStrokes(_whiteboardPages[_whiteboardCurrentPage - 1]);
                }
                if (_whiteboardCurrentPage - 1 < _pageBackgrounds.Count)
                {
                    _inkCanvas.SetWhiteboardBackground(_pageBackgrounds[_whiteboardCurrentPage - 1]);
                }
            }

            UpdatePageInfo();

            Grid.SetRowSpan(_inkCanvas, 2);
            _inkCanvas.SetWhiteboardMode();
            _inkCanvas.IsHitTestVisible = true;

            WhiteboardModeEntered?.Invoke();
        }

        public void ExitWhiteboardMode(
            StackPanel normalBottomButtons,
            Button captureBtn,
            Button scanBtn,
            Button connectDeviceBtn,
            Button pipBtn,
            StackPanel normalRightButtons,
            ToggleButton moveBtn,
            ToggleButton penBtn,
            ToggleButton eraserBtn,
            Border toolSliderBackground,
            StackPanel loadingContainer,
            StackPanel loadingPanel,
            Action startLoadingAnimation)
        {
            if (!_isWhiteboardMode) return;

            if (_whiteboardCurrentPage - 1 < _whiteboardPages.Count)
            {
                _whiteboardPages[_whiteboardCurrentPage - 1] = _inkCanvas.GetStrokes();
            }
            _hasWhiteboardData = _whiteboardTotalPages > 1 || _pageBackgrounds.Any(p => p != null) || _whiteboardPages.Any(p => p.Count > 0);

            _isWhiteboardMode = false;

            _videoImage.IsVisible = true;

            if (_whiteboardBackground != null)
            {
                _whiteboardBackground.IsVisible = false;
            }

            if (normalBottomButtons != null)
            {
                normalBottomButtons.IsVisible = true;
            }

            if (_whiteboardModeButtons != null)
            {
                _whiteboardModeButtons.IsVisible = false;
            }

            if (captureBtn != null) captureBtn.IsVisible = true;
            if (scanBtn != null) scanBtn.IsVisible = true;
            if (connectDeviceBtn != null) connectDeviceBtn.IsVisible = true;
            if (pipBtn != null) pipBtn.IsVisible = true;
            if (normalRightButtons != null) normalRightButtons.IsVisible = true;

            if (_whiteboardPageButtons != null)
            {
                _whiteboardPageButtons.IsVisible = false;
            }

            _inkCanvas.ExitWhiteboardMode();
            _inkCanvas.ClearStrokes();
            _inkCanvas.SetWhiteboardBackground(null);
            _inkCanvas.IsHitTestVisible = false;
            Grid.SetRowSpan(_inkCanvas, 1);
            _inkCanvas.SetStrokes(_videoStrokesBackup);

            _videoStrokesBackup = new List<InkStroke>();

            if (moveBtn != null) moveBtn.IsChecked = true;
            if (penBtn != null) penBtn.IsChecked = false;
            if (eraserBtn != null) eraserBtn.IsChecked = false;

            if (toolSliderBackground != null)
            {
                toolSliderBackground.Margin = new Thickness(0, 0, 0, 0);
            }

            if (loadingContainer != null)
            {
                loadingContainer.IsVisible = !_cameraService?.IsConnected ?? true;
            }
            if (loadingPanel != null)
            {
                loadingPanel.IsVisible = !_cameraService?.IsConnected ?? true;
            }
            if (_cameraService?.IsConnected != true)
            {
                startLoadingAnimation?.Invoke();
            }

            _cameraService?.DetectAndConnectCamera();

            WhiteboardModeExited?.Invoke();
        }

        public bool HandlePointerWheel()
        {
            return _isWhiteboardMode;
        }

        public bool HandleCanvasPointerPressed()
        {
            return _isWhiteboardMode;
        }

        public void PrevPage_Click(object? sender, RoutedEventArgs e)
        {
            if (_whiteboardCurrentPage > 1)
            {
                _whiteboardPages[_whiteboardCurrentPage - 1] = _inkCanvas.GetStrokes();
                _whiteboardCurrentPage--;
                _inkCanvas.SetStrokes(_whiteboardPages[_whiteboardCurrentPage - 1]);
                if (_whiteboardCurrentPage - 1 < _pageBackgrounds.Count)
                {
                    _inkCanvas.SetWhiteboardBackground(_pageBackgrounds[_whiteboardCurrentPage - 1]);
                }
                UpdatePageInfo();
            }
        }

        public void NextPage_Click(object? sender, RoutedEventArgs e)
        {
            if (_whiteboardCurrentPage == _whiteboardTotalPages)
            {
                _whiteboardPages[_whiteboardCurrentPage - 1] = _inkCanvas.GetStrokes();
                _whiteboardPages.Add(new List<InkStroke>());
                _pageBackgrounds.Add(null);
                _whiteboardTotalPages++;
                _whiteboardCurrentPage++;
                _inkCanvas.SetStrokes(new List<InkStroke>());
                _inkCanvas.SetWhiteboardBackground(null);
                UpdatePageInfo();
            }
            else
            {
                _whiteboardPages[_whiteboardCurrentPage - 1] = _inkCanvas.GetStrokes();
                _whiteboardCurrentPage++;
                _inkCanvas.SetStrokes(_whiteboardPages[_whiteboardCurrentPage - 1]);
                if (_whiteboardCurrentPage - 1 < _pageBackgrounds.Count)
                {
                    _inkCanvas.SetWhiteboardBackground(_pageBackgrounds[_whiteboardCurrentPage - 1]);
                }
                UpdatePageInfo();
            }
        }

        private void UpdatePageInfo()
        {
            if (_pageInfoText != null)
            {
                _pageInfoText.Text = $"{_whiteboardCurrentPage}/{_whiteboardTotalPages}";
            }

            if (_nextPagePath != null && _nextPageText != null)
            {
                if (_whiteboardCurrentPage == _whiteboardTotalPages)
                {
                    _nextPagePath.Data = Geometry.Parse("M19 13h-6v6h-2v-6H5v-2h6V5h2v6h6v2z");
                    _nextPageText.Text = "加页";
                }
                else
                {
                    _nextPagePath.Data = Geometry.Parse("M10 6L8.59 7.41 13.17 12l-4.58 4.59L10 18l6-6z");
                    _nextPageText.Text = "下一页";
                }
            }

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentPage)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(TotalPages)));
        }

        public void PageInfoBorder_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            TogglePagePanel();
        }

        private void TogglePagePanel()
        {
            if (_pagePanel == null) return;

            if (_pagePanelOpen)
            {
                ClosePagePanel();
            }
            else
            {
                OpenPagePanel();
            }
        }

        private async void OpenPagePanel()
        {
            if (_pagePanel == null || _pagePanelOpen) return;

            _pagePanelOpen = true;
            _pagePanel.IsVisible = true;
            UpdatePageThumbnails();

            await UIAnimations.SlideInFromRight(_pagePanel, 280);
        }

        private async void ClosePagePanel()
        {
            if (_pagePanel == null || !_pagePanelOpen) return;

            _pagePanelOpen = false;
            await UIAnimations.SlideOutToRight(_pagePanel, 280);
            _pagePanel.IsVisible = false;
        }

        private void UpdatePageThumbnails()
        {
            _whiteboardPageThumbnails.Clear();
            for (int i = 0; i < _whiteboardTotalPages; i++)
            {
                _whiteboardPageThumbnails.Add(new WhiteboardPageThumbnail
                {
                    PageNumber = i + 1,
                    Thumbnail = GeneratePageThumbnail(i),
                    IsSelected = (i + 1) == _whiteboardCurrentPage
                });
            }
        }

        private Bitmap? GeneratePageThumbnail(int pageIndex)
        {
            if (pageIndex < 0 || pageIndex >= _pageBackgrounds.Count)
                return null;

            var backgroundPath = _pageBackgrounds[pageIndex];
            if (string.IsNullOrEmpty(backgroundPath) || !File.Exists(backgroundPath))
                return null;

            try
            {
                using var stream = File.OpenRead(backgroundPath);
                using var originalBitmap = SKBitmap.Decode(stream);
                if (originalBitmap == null) return null;

                int thumbWidth = 260;
                int thumbHeight = (int)(originalBitmap.Height * ((double)thumbWidth / originalBitmap.Width));

                using var resizedBitmap = originalBitmap.Resize(new SKImageInfo(thumbWidth, thumbHeight), SKFilterQuality.Medium);
                if (resizedBitmap == null) return null;

                using var image = SKImage.FromBitmap(resizedBitmap);
                using var data = image.Encode(SKEncodedImageFormat.Png, 80);

                var ms = new MemoryStream(data.ToArray());
                return new Bitmap(ms);
            }
            catch
            {
                return null;
            }
        }

        public void PageItem_PointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (sender is Border border && border.Tag is int pageNumber)
            {
                if (pageNumber < 1 || pageNumber > _whiteboardTotalPages)
                    return;

                _whiteboardPages[_whiteboardCurrentPage - 1] = _inkCanvas.GetStrokes();
                _whiteboardCurrentPage = pageNumber;
                _inkCanvas.SetStrokes(_whiteboardPages[_whiteboardCurrentPage - 1]);
                if (_whiteboardCurrentPage - 1 < _pageBackgrounds.Count)
                {
                    _inkCanvas.SetWhiteboardBackground(_pageBackgrounds[_whiteboardCurrentPage - 1]);
                }
                UpdatePageInfo();
                UpdatePageThumbnails();
                ClosePagePanel();
            }
        }

        public void AddPageBtn_Click(object? sender, RoutedEventArgs e)
        {
            _whiteboardPages[_whiteboardCurrentPage - 1] = _inkCanvas.GetStrokes();
            _whiteboardPages.Add(new List<InkStroke>());
            _pageBackgrounds.Add(null);
            _whiteboardTotalPages++;
            _whiteboardCurrentPage = _whiteboardTotalPages;
            _inkCanvas.SetStrokes(new List<InkStroke>());
            _inkCanvas.SetWhiteboardBackground(null);
            UpdatePageInfo();
            UpdatePageThumbnails();
        }

        public void ImportPptBtn_Click(object? sender, RoutedEventArgs e)
        {
            ImportPptRequested?.Invoke();
        }

        public async void ImportPptSlides(List<string> slideImagePaths)
        {
            if (slideImagePaths == null || slideImagePaths.Count == 0)
                return;

            if (_importingOverlay != null)
            {
                _importingOverlay.IsVisible = true;
            }

            try
            {
                await Task.Run(async () =>
                {
                    foreach (var imagePath in slideImagePaths)
                    {
                        if (!File.Exists(imagePath)) continue;

                        await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                        {
                            if (_whiteboardCurrentPage - 1 < _whiteboardPages.Count)
                            {
                                _whiteboardPages[_whiteboardCurrentPage - 1] = _inkCanvas.GetStrokes();
                            }
                            _whiteboardPages.Add(new List<InkStroke>());
                            _pageBackgrounds.Add(imagePath);
                            _whiteboardTotalPages++;
                            _whiteboardCurrentPage = _whiteboardTotalPages;
                            _inkCanvas.SetStrokes(new List<InkStroke>());
                            _inkCanvas.SetWhiteboardBackground(imagePath);
                            UpdatePageInfo();
                            UpdatePageThumbnails();
                        });
                    }
                });
            }
            finally
            {
                if (_importingOverlay != null)
                {
                    _importingOverlay.IsVisible = false;
                }
            }
        }

        #region PptService Methods (Spire.Presentation)

        public static List<string> ConvertPptToImages(string pptPath, string? outputDirectory = null)
        {
            var imagePaths = new List<string>();

            if (!File.Exists(pptPath))
            {
                return imagePaths;
            }

            outputDirectory ??= IOPath.Combine(IOPath.GetTempPath(), "ShowWrite_PptImages", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(outputDirectory);

            try
            {
                using var presentation = new Spire.Presentation.Presentation();
                presentation.LoadFromFile(pptPath);

                for (int i = 0; i < presentation.Slides.Count; i++)
                {
                    var slide = presentation.Slides[i];
                    var fileName = $"Slide_{i + 1}.png";
                    var outputPath = IOPath.Combine(outputDirectory, fileName);

                    using var image = slide.SaveAsImage();
                    if (image != null)
                    {
                        using var ms = new MemoryStream();
                        image.Save(ms, System.Drawing.Imaging.ImageFormat.Png);
                        ms.Position = 0;

                        using var skBitmap = SKBitmap.Decode(ms);
                        if (skBitmap != null)
                        {
                            using var fileStream = File.OpenWrite(outputPath);
                            skBitmap.Encode(fileStream, SKEncodedImageFormat.Png, 100);
                            imagePaths.Add(outputPath);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"PPT 转换错误: {ex.Message}");
            }

            return imagePaths;
        }

        public static int GetSlideCount(string pptPath)
        {
            if (!File.Exists(pptPath))
            {
                return 0;
            }

            try
            {
                using var presentation = new Spire.Presentation.Presentation();
                presentation.LoadFromFile(pptPath);
                return presentation.Slides.Count;
            }
            catch
            {
                return 0;
            }
        }

        #endregion

        #region AsposeSlidesService Methods

        public void OpenAsposePresentation(string filePath)
        {
            CloseAsposePresentation();
            _asposePresentation = new Aspose.Slides.Presentation(filePath);
            _asposeCurrentIndex = 0;
        }

        public void CloseAsposePresentation()
        {
            _asposePresentation?.Dispose();
            _asposePresentation = null;
            _asposeCurrentIndex = 0;
        }

        public void AsposeNextSlide()
        {
            if (_asposePresentation == null) return;
            if (_asposeCurrentIndex < _asposePresentation.Slides.Count - 1)
            {
                _asposeCurrentIndex++;
            }
        }

        public void AsposePreviousSlide()
        {
            if (_asposePresentation == null) return;
            if (_asposeCurrentIndex > 0)
            {
                _asposeCurrentIndex--;
            }
        }

        public Bitmap? RenderAsposeCurrentSlide(int targetWidth = 1280, int targetHeight = 720)
        {
            if (_asposePresentation == null) return null;
            var slide = _asposePresentation.Slides[_asposeCurrentIndex];

            var slideSize = _asposePresentation.SlideSize.Size;
            var scaleX = (float)targetWidth / slideSize.Width;
            var scaleY = (float)targetHeight / slideSize.Height;
            var scale = Math.Min(scaleX, scaleY);
            if (scale <= 0) scale = 1f;

            using Aspose.Slides.IImage image = slide.GetImage(scale, scale);
            using var ms = new MemoryStream();
            image.Save(ms, ImageFormat.Png);
            ms.Position = 0;
            return new Bitmap(ms);
        }

        #endregion

        #region OpenXmlPptService Methods

        public static List<string> ReadAllTextFromPpt(string filePath)
        {
            var result = new List<string>();
            using var ppt = PresentationDocument.Open(filePath, false);
            var presentationPart = ppt.PresentationPart;
            if (presentationPart?.Presentation?.SlideIdList == null)
                return result;

            foreach (var slideId in presentationPart.Presentation.SlideIdList.Elements<P.SlideId>())
            {
                var slidePart = (SlidePart)presentationPart.GetPartById(slideId.RelationshipId);
                var texts = slidePart.Slide.Descendants<D.Text>();
                foreach (var t in texts)
                {
                    if (!string.IsNullOrWhiteSpace(t.Text))
                    {
                        result.Add(t.Text);
                    }
                }
            }
            return result;
        }

        #endregion

        #region PowerPointService Methods

        /// <summary>
        /// 打开PowerPoint文件并准备放映
        /// </summary>
        public bool OpenPowerPointPresentation(string filePath)
        {
            try
            {
                ClosePowerPointPresentation();

                _powerPointService = new PowerPointService();
                _powerPointService.SlideChanged += OnPowerPointSlideChanged;
                _powerPointService.PresentationEnded += OnPowerPointPresentationEnded;

                return _powerPointService.OpenPresentation(filePath);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"打开PowerPoint失败: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// 开始PowerPoint放映
        /// </summary>
        public bool StartPowerPointSlideShow(IntPtr hostWindowHandle, int width, int height)
        {
            if (_powerPointService == null) return false;

            try
            {
                _isPowerPointMode = true;
                return _powerPointService.StartSlideShow(hostWindowHandle, width, height);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"开始放映失败: {ex.Message}");
                _isPowerPointMode = false;
                return false;
            }
        }

        /// <summary>
        /// PowerPoint下一页
        /// </summary>
        public void PowerPointNextSlide()
        {
            _powerPointService?.NextSlide();
        }

        /// <summary>
        /// PowerPoint上一页
        /// </summary>
        public void PowerPointPreviousSlide()
        {
            _powerPointService?.PreviousSlide();
        }

        /// <summary>
        /// 跳转到PowerPoint指定页
        /// </summary>
        public void PowerPointGoToSlide(int slideIndex)
        {
            _powerPointService?.GoToSlide(slideIndex);
        }

        /// <summary>
        /// 停止PowerPoint放映
        /// </summary>
        public void StopPowerPointSlideShow()
        {
            _powerPointService?.StopSlideShow();
            _isPowerPointMode = false;
        }

        /// <summary>
        /// 关闭PowerPoint演示文稿
        /// </summary>
        public void ClosePowerPointPresentation()
        {
            if (_powerPointService != null)
            {
                _powerPointService.SlideChanged -= OnPowerPointSlideChanged;
                _powerPointService.PresentationEnded -= OnPowerPointPresentationEnded;
                _powerPointService.Dispose();
                _powerPointService = null;
            }
            _isPowerPointMode = false;
        }

        private void OnPowerPointSlideChanged(int currentSlide, int totalSlides)
        {
            // 触发属性变更通知
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PowerPointCurrentSlide)));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(PowerPointSlideCount)));
        }

        private void OnPowerPointPresentationEnded()
        {
            _isPowerPointMode = false;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsPowerPointMode)));
        }

        #endregion

        public void Dispose()
        {
            CloseAsposePresentation();
            ClosePowerPointPresentation();
            GC.SuppressFinalize(this);
        }
    }
}
