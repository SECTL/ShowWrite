using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using SkiaSharp;
using System;
using System.Collections.Generic;

namespace ShowWrite
{
    public class InkCanvas : Control, IDisposable
    {
        private SKSurface? _wetInkSurface;
        private SKCanvas? _wetInkCanvas;

        private int _videoWidth;
        private int _videoHeight;

        private int _wetInkWidth;
        private int _wetInkHeight;

        private bool _isPhotoMode;
        private double _photoWidth;
        private double _photoHeight;

        private bool _isWhiteboardMode;
        private string? _whiteboardBackgroundPath;
        private SKBitmap? _whiteboardBackgroundBitmap;

        private readonly List<InkStroke> _strokes = new();
        private List<SKPoint>? _currentVideoPoints;
        private List<float>? _currentPointWidths;
        private List<long>? _currentTimestamps;
        private bool _currentIsEraser;
        private float _currentSize;
        private SKColor _currentColor;
        private float _currentRatio = 1.0f;
        private float _prevRatio = 1.0f;

        private List<InkStroke>? _tempStrokes;
        private SKPoint _lastEraserPoint;
        private bool _hasLastEraserPoint;

        private WriteableBitmap? _displayBitmap;

        private bool _isDrawing;

        private Image? _videoImage;

        private double _currentZoom = 1.0;
        private Point _currentPan = new Point(0, 0);

        private bool _isPalmEraserActive = false;
        private bool _lastModeBeforePalmEraser = false;
        private double _palmEraserThreshold = 5000.0;
        private double _currentTouchArea = 0.0;
        private bool _enablePalmEraser = true;

        public int PenSize { get; set; } = 4;
        public SKColor PenColor { get; set; } = SKColors.Red;
        public int EraserSize { get; set; } = 20;
        public PenSettings PenSettings { get; set; } = new PenSettings();

        public double PalmEraserThreshold
        {
            get => _palmEraserThreshold;
            set => _palmEraserThreshold = Math.Max(1000, value);
        }

        public bool EnablePalmEraser
        {
            get => _enablePalmEraser;
            set => _enablePalmEraser = value;
        }

        public bool IsPalmEraserActive => _isPalmEraserActive;

        public bool IsPenMode { get; private set; }
        public bool IsEraserMode { get; private set; }

        public InkCanvas()
        {
            ClipToBounds = false;

            PointerPressed += OnPointerPressed;
            PointerMoved += OnPointerMoved;
            PointerReleased += OnPointerReleased;
            PointerCaptureLost += OnPointerCaptureLost;
        }

        public void SetVideoImage(Image videoImage)
        {
            _videoImage = videoImage;
        }

        public void SetVideoFrame(OpenCvSharp.Mat? frame)
        {
            if (frame != null && !frame.Empty())
            {
                SetVideoSize(frame.Width, frame.Height);
            }
        }

        public void SetTransform(double zoom, Point pan)
        {
            _currentZoom = zoom;
            _currentPan = pan;
        }

        private void SetVideoSize(int width, int height)
        {
            if (width <= 0 || height <= 0) return;
            if (_videoWidth == width && _videoHeight == height) return;

            _videoWidth = width;
            _videoHeight = height;
        }

        private void EnsureWetInkSurface(int width, int height)
        {
            if (width <= 0 || height <= 0) return;
            if (_wetInkSurface != null && _wetInkWidth == width && _wetInkHeight == height) return;

            _wetInkCanvas?.Dispose();
            _wetInkSurface?.Dispose();

            var info = new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul);
            _wetInkSurface = SKSurface.Create(info);
            _wetInkCanvas = _wetInkSurface?.Canvas;
            _wetInkCanvas?.Clear(SKColors.Transparent);
            _wetInkWidth = width;
            _wetInkHeight = height;
        }

        public void SetPenMode()
        {
            IsPenMode = true;
            IsEraserMode = false;
            if (_videoImage != null)
            {
                _videoImage.Cursor = new Cursor(StandardCursorType.Cross);
            }
            IsHitTestVisible = true;
        }

        public void SetEraserMode()
        {
            IsPenMode = false;
            IsEraserMode = true;
            if (_videoImage != null)
            {
                _videoImage.Cursor = new Cursor(StandardCursorType.Cross);
            }
            IsHitTestVisible = true;
        }

        public void SetMoveMode()
        {
            IsPenMode = false;
            IsEraserMode = false;
            if (_videoImage != null)
            {
                _videoImage.Cursor = Cursor.Default;
            }
            IsHitTestVisible = false;
        }

        public void SetPenColor(SKColor color)
        {
            PenColor = color;
        }

        public void SetPenSize(int size)
        {
            PenSize = size;
        }

        public void SetPhotoMode(double photoWidth, double photoHeight)
        {
            _photoWidth = photoWidth;
            _photoHeight = photoHeight;
            _isPhotoMode = true;
            IsHitTestVisible = IsPenMode || IsEraserMode;
        }

        public void ExitPhotoMode()
        {
            _isPhotoMode = false;
            _photoWidth = 0;
            _photoHeight = 0;
        }

        public void SetWhiteboardMode()
        {
            _isWhiteboardMode = true;
        }

        public void ExitWhiteboardMode()
        {
            _isWhiteboardMode = false;
            _whiteboardBackgroundPath = null;
            _whiteboardBackgroundBitmap?.Dispose();
            _whiteboardBackgroundBitmap = null;
        }

        public void SetWhiteboardBackground(string? imagePath)
        {
            _whiteboardBackgroundPath = imagePath;
            _whiteboardBackgroundBitmap?.Dispose();
            _whiteboardBackgroundBitmap = null;

            if (!string.IsNullOrEmpty(imagePath) && System.IO.File.Exists(imagePath))
            {
                try
                {
                    using var stream = System.IO.File.OpenRead(imagePath);
                    _whiteboardBackgroundBitmap = SKBitmap.Decode(stream);
                }
                catch
                {
                    _whiteboardBackgroundBitmap = null;
                }
            }

            InvalidateVisual();
        }

        public List<InkStroke> GetStrokes()
        {
            return new List<InkStroke>(_strokes);
        }

        public void SetStrokes(List<InkStroke> strokes)
        {
            _strokes.Clear();
            _strokes.AddRange(strokes);
            InvalidateVisual();
        }

        public void ClearStrokes()
        {
            _strokes.Clear();
            InvalidateVisual();
        }

        private SKPoint ScreenToVideo(Point screenPoint)
        {
            if (_isWhiteboardMode || _isPhotoMode)
            {
                return new SKPoint((float)screenPoint.X, (float)screenPoint.Y);
            }

            var videoX = (float)((screenPoint.X - _currentPan.X) / _currentZoom);
            var videoY = (float)((screenPoint.Y - _currentPan.Y) / _currentZoom);
            return new SKPoint(videoX, videoY);
        }

        private SKPoint VideoToScreen(SKPoint videoPoint)
        {
            if (_isWhiteboardMode || _isPhotoMode)
            {
                return videoPoint;
            }

            var screenX = videoPoint.X * (float)_currentZoom + (float)_currentPan.X;
            var screenY = videoPoint.Y * (float)_currentZoom + (float)_currentPan.Y;
            return new SKPoint(screenX, screenY);
        }

        private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
        {
            if (!IsPenMode && !IsEraserMode) return;

            var point = e.GetPosition(this);
            var pointerPoint = e.GetCurrentPoint(this);

            if (EnablePalmEraser && IsPenMode)
            {
                var touchArea = CalculateTouchArea(e);
                _currentTouchArea = touchArea;

                if (touchArea > PalmEraserThreshold)
                {
                    ActivatePalmEraser(point);
                    return;
                }
            }

            _isDrawing = true;
            e.Pointer.Capture(this);

            var videoPoint = ScreenToVideo(point);
            _currentVideoPoints = new List<SKPoint> { videoPoint };
            _currentIsEraser = IsEraserMode;
            _currentSize = IsEraserMode ? EraserSize : PenSize;
            _currentColor = PenColor;

            float zoomFactor = (_isWhiteboardMode || _isPhotoMode) ? 1.0f : (float)_currentZoom;

            if (_currentIsEraser)
            {
                _lastEraserPoint = videoPoint;
                _hasLastEraserPoint = true;
                _tempStrokes = CloneStrokes(_strokes);
                ApplyEraserToPoint(videoPoint, _currentSize / zoomFactor);
            }
            else
            {
                _currentRatio = 0.5f;
                _prevRatio = 0.5f;
                float baseWidth = _currentSize / zoomFactor;
                _currentPointWidths = new List<float> { baseWidth * _currentRatio };
                _currentTimestamps = new List<long> { DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() };
            }
        }

        private void OnPointerMoved(object? sender, PointerEventArgs e)
        {
            if (!IsPenMode && !IsEraserMode) return;

            var currentPoint = e.GetPosition(this);
            var videoPoint = ScreenToVideo(currentPoint);

            if (EnablePalmEraser && IsPenMode)
            {
                var touchArea = CalculateTouchArea(e);
                _currentTouchArea = touchArea;

                if (touchArea > PalmEraserThreshold)
                {
                    if (!_isPalmEraserActive)
                    {
                        ActivatePalmEraser(currentPoint);
                    }
                    ApplyEraserAtPoint(videoPoint, CalculatePalmEraserSize(touchArea));
                    InvalidateVisual();
                    return;
                }
            }

            if (_isPalmEraserActive)
            {
                DeactivatePalmEraser();
            }

            if (!_isDrawing || _currentVideoPoints == null || _wetInkCanvas == null) return;

            var lastVideoPoint = _currentVideoPoints[^1];
            _currentVideoPoints.Add(videoPoint);

            float zoomFactor = (_isWhiteboardMode || _isPhotoMode) ? 1.0f : (float)_currentZoom;

            if (_currentIsEraser)
            {
                if (_hasLastEraserPoint)
                {
                    ApplyEraserToSegment(_lastEraserPoint, videoPoint, _currentSize / zoomFactor);
                }
                _lastEraserPoint = videoPoint;
                _hasLastEraserPoint = true;
            }
            else
            {
                var currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                _currentTimestamps!.Add(currentTime);

                float videoDistance = CalculateDistance(lastVideoPoint, videoPoint);
                float screenDistance = (_isWhiteboardMode || _isPhotoMode) ? videoDistance : videoDistance * (float)_currentZoom;

                UpdateRatioBySpeed(screenDistance);

                float baseWidth = _currentSize / zoomFactor;
                float width = baseWidth * _currentRatio;
                _currentPointWidths!.Add(width);

                var screenFrom = VideoToScreen(lastVideoPoint);
                var screenTo = VideoToScreen(videoPoint);

                float screenFromWidth = (_isWhiteboardMode || _isPhotoMode) ? baseWidth * _prevRatio : baseWidth * _prevRatio * (float)_currentZoom;
                float screenToWidth = (_isWhiteboardMode || _isPhotoMode) ? width : width * (float)_currentZoom;

                DrawSmoothSegment(_wetInkCanvas, screenFrom, screenTo,
                    screenFromWidth, screenToWidth, _currentColor);

                _prevRatio = _currentRatio;
            }

            InvalidateVisual();
        }

        private float CalculateDistance(SKPoint from, SKPoint to)
        {
            float dx = to.X - from.X;
            float dy = to.Y - from.Y;
            return (float)Math.Sqrt(dx * dx + dy * dy);
        }

        private void UpdateRatioBySpeed(float screenDistance)
        {
            var settings = PenSettings;

            if (screenDistance > settings.SpeedThresholdFast)
            {
                if (_currentRatio > settings.RatioMin)
                    _currentRatio *= settings.RatioChangeCoefficient;
            }
            else if (screenDistance < settings.SpeedThresholdSlow)
            {
                if (_currentRatio < settings.RatioMax)
                    _currentRatio *= (1 + (1 - settings.RatioChangeCoefficient));
            }
            else
            {
                if (_currentRatio > 1f)
                    _currentRatio *= settings.RatioChangeCoefficient;
                else
                    _currentRatio *= (1 + (1 - settings.RatioChangeCoefficient) / 2);
            }

            _currentRatio = Math.Clamp(_currentRatio, settings.RatioMin, settings.RatioMax);
        }

        private void DrawSmoothSegment(SKCanvas canvas, SKPoint from, SKPoint to,
            float fromWidth, float toWidth, SKColor color)
        {
            float distance = CalculateDistance(from, to);

            using var paint = new SKPaint
            {
                Style = SKPaintStyle.Fill,
                IsAntialias = true,
                Color = color
            };

            if (distance < 1f)
            {
                canvas.DrawCircle(from, fromWidth / 2, paint);
                return;
            }

            int subdivisions = Math.Max(3, (int)(distance / (float)PenSettings.Denominator * 10));

            for (int i = 0; i <= subdivisions; i++)
            {
                float t = i / (float)subdivisions;
                float x = from.X + (to.X - from.X) * t;
                float y = from.Y + (to.Y - from.Y) * t;
                float w = fromWidth + (toWidth - fromWidth) * t;

                canvas.DrawCircle(new SKPoint(x, y), w / 2, paint);
            }
        }

        private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
        {
            if (_isPalmEraserActive)
            {
                if (_tempStrokes != null)
                {
                    _strokes.Clear();
                    _strokes.AddRange(_tempStrokes);
                }
                DeactivatePalmEraser();
            }

            if (!_isDrawing) return;

            if (_currentVideoPoints != null && _currentVideoPoints.Count > 1)
            {
                if (_currentIsEraser && _tempStrokes != null)
                {
                    _strokes.Clear();
                    _strokes.AddRange(_tempStrokes);
                }
                else if (_currentPointWidths != null)
                {
                    ApplyInkStyle(_currentPointWidths);

                    var stroke = new InkStroke
                    {
                        VideoPoints = new List<SKPoint>(_currentVideoPoints),
                        PointWidths = new List<float>(_currentPointWidths),
                        IsEraser = false,
                        Size = _currentSize / (float)_currentZoom,
                        Color = _currentColor
                    };
                    _strokes.Add(stroke);
                }
            }

            _currentVideoPoints = null;
            _currentPointWidths = null;
            _currentTimestamps = null;
            _tempStrokes = null;
            _hasLastEraserPoint = false;
            _isDrawing = false;

            _wetInkCanvas?.Clear(SKColors.Transparent);
            InvalidateVisual();
            e.Pointer.Capture(null);
        }

        private void ApplyInkStyle(List<float> widths)
        {
            if (widths.Count < 2) return;

            int n = widths.Count - 1;
            float baseWidth = _currentSize / (float)_currentZoom;
            float minPressure = 0.2f;
            int taperLength = Math.Min(widths.Count / 5, 12);

            if (n >= taperLength * 2)
            {
                for (int i = 0; i < taperLength; i++)
                {
                    float factor = (float)i / taperLength;
                    float pressure = minPressure + (1.0f - minPressure) * factor;
                    float targetWidth = baseWidth * pressure;
                    widths[i] = widths[i] * factor + targetWidth * (1 - factor);
                }

                for (int i = 0; i < taperLength; i++)
                {
                    float factor = (float)i / taperLength;
                    float pressure = minPressure + (1.0f - minPressure) * factor;
                    float targetWidth = baseWidth * pressure;
                    int idx = n - i;
                    widths[idx] = widths[idx] * factor + targetWidth * (1 - factor);
                }
            }
            else
            {
                for (int i = 0; i <= n; i++)
                {
                    float startFactor = (float)i / n;
                    float endFactor = (float)(n - i) / n;
                    float positionFactor = Math.Min(startFactor, endFactor) * 2.0f;
                    float pressure = minPressure + (1.0f - minPressure) * Math.Min(positionFactor, 1.0f);
                    float targetWidth = baseWidth * pressure;
                    float blendFactor = 1.0f - Math.Min(positionFactor, 1.0f);
                    widths[i] = widths[i] * (1 - blendFactor) + targetWidth * blendFactor;
                }
            }
        }

        private void OnPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
        {
            if (_isDrawing && _currentVideoPoints != null && _currentVideoPoints.Count > 1)
            {
                if (_currentIsEraser && _tempStrokes != null)
                {
                    _strokes.Clear();
                    _strokes.AddRange(_tempStrokes);
                }
                else if (_currentPointWidths != null)
                {
                    ApplyInkStyle(_currentPointWidths);

                    var stroke = new InkStroke
                    {
                        VideoPoints = new List<SKPoint>(_currentVideoPoints),
                        PointWidths = new List<float>(_currentPointWidths),
                        IsEraser = false,
                        Size = _currentSize / (float)_currentZoom,
                        Color = _currentColor
                    };
                    _strokes.Add(stroke);
                }
            }

            _currentVideoPoints = null;
            _currentPointWidths = null;
            _currentTimestamps = null;
            _tempStrokes = null;
            _hasLastEraserPoint = false;
            _isDrawing = false;
            _wetInkCanvas?.Clear(SKColors.Transparent);
        }

        private List<InkStroke> CloneStrokes(List<InkStroke> source)
        {
            var result = new List<InkStroke>();
            foreach (var stroke in source)
            {
                result.Add(new InkStroke
                {
                    VideoPoints = new List<SKPoint>(stroke.VideoPoints ?? new List<SKPoint>()),
                    PointWidths = stroke.PointWidths != null ? new List<float>(stroke.PointWidths) : null,
                    IsEraser = stroke.IsEraser,
                    Size = stroke.Size,
                    Color = stroke.Color
                });
            }
            return result;
        }

        private void ApplyEraserToPoint(SKPoint eraserCenter, float eraserRadius)
        {
            if (_tempStrokes == null) return;

            var newStrokes = new List<InkStroke>();

            foreach (var stroke in _tempStrokes)
            {
                if (stroke.VideoPoints == null || stroke.VideoPoints.Count < 2)
                {
                    newStrokes.Add(stroke);
                    continue;
                }

                var segments = EraseStrokeWithCircle(stroke, eraserCenter, eraserRadius);
                newStrokes.AddRange(segments);
            }

            _tempStrokes = newStrokes;
        }

        private void ApplyEraserToSegment(SKPoint from, SKPoint to, float eraserRadius)
        {
            if (_tempStrokes == null) return;

            var newStrokes = new List<InkStroke>();
            float step = 1.0f;

            float dx = to.X - from.X;
            float dy = to.Y - from.Y;
            float length = (float)Math.Sqrt(dx * dx + dy * dy);

            if (length < step)
            {
                ApplyEraserToPoint(to, eraserRadius);
                return;
            }

            int steps = (int)Math.Ceiling(length / step);
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                SKPoint point = new SKPoint(
                    from.X + dx * t,
                    from.Y + dy * t
                );
                ApplyEraserToPoint(point, eraserRadius);
            }
        }

        private List<InkStroke> EraseStrokeWithCircle(InkStroke stroke, SKPoint circleCenter, float circleRadius)
        {
            var result = new List<InkStroke>();
            var currentSegment = new List<SKPoint>();
            var currentWidths = new List<float>();
            var hasWidths = stroke.PointWidths != null && stroke.PointWidths.Count == stroke.VideoPoints.Count;

            for (int i = 0; i < stroke.VideoPoints.Count; i++)
            {
                var point = stroke.VideoPoints[i];
                var width = hasWidths ? stroke.PointWidths![i] : stroke.Size;

                if (i == 0)
                {
                    if (!IsPointInCircle(point, circleCenter, circleRadius))
                    {
                        currentSegment.Add(point);
                        currentWidths.Add(width);
                    }
                    continue;
                }

                var prevPoint = stroke.VideoPoints[i - 1];
                var prevWidth = hasWidths ? stroke.PointWidths![i - 1] : stroke.Size;
                var intersections = LineCircleIntersection(prevPoint, point, circleCenter, circleRadius);

                if (intersections.Count == 0)
                {
                    if (!IsPointInCircle(point, circleCenter, circleRadius))
                    {
                        currentSegment.Add(point);
                        currentWidths.Add(width);
                    }
                    else
                    {
                        if (currentSegment.Count >= 2)
                        {
                            result.Add(CreateStrokeSegment(currentSegment, currentWidths, stroke));
                        }
                        currentSegment.Clear();
                        currentWidths.Clear();
                    }
                }
                else if (intersections.Count == 1)
                {
                    var inter = intersections[0];
                    var interWidth = InterpolateWidth(prevWidth, width, prevPoint, point, inter);

                    if (IsPointInCircle(prevPoint, circleCenter, circleRadius))
                    {
                        if (currentSegment.Count >= 2)
                        {
                            result.Add(CreateStrokeSegment(currentSegment, currentWidths, stroke));
                        }
                        currentSegment.Clear();
                        currentWidths.Clear();
                        currentSegment.Add(inter);
                        currentWidths.Add(interWidth);
                        currentSegment.Add(point);
                        currentWidths.Add(width);
                    }
                    else
                    {
                        currentSegment.Add(inter);
                        currentWidths.Add(interWidth);
                        if (currentSegment.Count >= 2)
                        {
                            result.Add(CreateStrokeSegment(currentSegment, currentWidths, stroke));
                        }
                        currentSegment.Clear();
                        currentWidths.Clear();
                    }
                }
                else
                {
                    var inter1 = intersections[0];
                    var inter2 = intersections[1];

                    float t1 = GetParameterOnLine(prevPoint, point, inter1);
                    float t2 = GetParameterOnLine(prevPoint, point, inter2);

                    SKPoint first = t1 < t2 ? inter1 : inter2;
                    SKPoint second = t1 < t2 ? inter2 : inter1;
                    float firstT = Math.Min(t1, t2);
                    float secondT = Math.Max(t1, t2);

                    var firstWidth = InterpolateWidth(prevWidth, width, firstT);
                    var secondWidth = InterpolateWidth(prevWidth, width, secondT);

                    currentSegment.Add(first);
                    currentWidths.Add(firstWidth);
                    if (currentSegment.Count >= 2)
                    {
                        result.Add(CreateStrokeSegment(currentSegment, currentWidths, stroke));
                    }
                    currentSegment.Clear();
                    currentWidths.Clear();
                    currentSegment.Add(second);
                    currentWidths.Add(secondWidth);
                    currentSegment.Add(point);
                    currentWidths.Add(width);
                }
            }

            if (currentSegment.Count >= 2)
            {
                result.Add(CreateStrokeSegment(currentSegment, currentWidths, stroke));
            }

            return result;
        }

        private float InterpolateWidth(float w1, float w2, float t)
        {
            return w1 + (w2 - w1) * t;
        }

        private float InterpolateWidth(float w1, float w2, SKPoint p1, SKPoint p2, SKPoint inter)
        {
            float dx = p2.X - p1.X;
            float dy = p2.Y - p1.Y;
            float length = (float)Math.Sqrt(dx * dx + dy * dy);
            if (length < 0.001f) return w1;

            float t = ((inter.X - p1.X) * dx + (inter.Y - p1.Y) * dy) / (length * length);
            return InterpolateWidth(w1, w2, t);
        }

        private InkStroke CreateStrokeSegment(List<SKPoint> points, List<float> widths, InkStroke source)
        {
            return new InkStroke
            {
                VideoPoints = new List<SKPoint>(points),
                PointWidths = new List<float>(widths),
                IsEraser = source.IsEraser,
                Size = source.Size,
                Color = source.Color
            };
        }

        private bool IsPointInCircle(SKPoint point, SKPoint center, float radius)
        {
            float dx = point.X - center.X;
            float dy = point.Y - center.Y;
            return dx * dx + dy * dy < radius * radius;
        }

        private List<SKPoint> LineCircleIntersection(SKPoint lineStart, SKPoint lineEnd, SKPoint circleCenter, float circleRadius)
        {
            var result = new List<SKPoint>();

            float dx = lineEnd.X - lineStart.X;
            float dy = lineEnd.Y - lineStart.Y;
            float fx = lineStart.X - circleCenter.X;
            float fy = lineStart.Y - circleCenter.Y;

            float a = dx * dx + dy * dy;
            float b = 2 * (fx * dx + fy * dy);
            float c = fx * fx + fy * fy - circleRadius * circleRadius;

            float discriminant = b * b - 4 * a * c;

            if (discriminant < 0 || a == 0)
            {
                return result;
            }

            discriminant = (float)Math.Sqrt(discriminant);

            float t1 = (-b - discriminant) / (2 * a);
            float t2 = (-b + discriminant) / (2 * a);

            if (t1 >= 0 && t1 <= 1)
            {
                result.Add(new SKPoint(
                    lineStart.X + t1 * dx,
                    lineStart.Y + t1 * dy
                ));
            }

            if (t2 >= 0 && t2 <= 1 && Math.Abs(t1 - t2) > 0.0001f)
            {
                result.Add(new SKPoint(
                    lineStart.X + t2 * dx,
                    lineStart.Y + t2 * dy
                ));
            }

            return result;
        }

        private float GetParameterOnLine(SKPoint lineStart, SKPoint lineEnd, SKPoint point)
        {
            float dx = lineEnd.X - lineStart.X;
            float dy = lineEnd.Y - lineStart.Y;

            if (Math.Abs(dx) > Math.Abs(dy))
            {
                return (point.X - lineStart.X) / dx;
            }
            else if (Math.Abs(dy) > 0.0001f)
            {
                return (point.Y - lineStart.Y) / dy;
            }
            return 0;
        }

        private SKPaint CreatePenPaint(SKColor color, float size)
        {
            return new SKPaint
            {
                Style = SKPaintStyle.Stroke,
                StrokeCap = SKStrokeCap.Round,
                StrokeJoin = SKStrokeJoin.Round,
                IsAntialias = true,
                Color = color,
                StrokeWidth = size
            };
        }

        private SKPaint CreateEraserPaint(float size)
        {
            return new SKPaint
            {
                Style = SKPaintStyle.Stroke,
                StrokeCap = SKStrokeCap.Round,
                StrokeJoin = SKStrokeJoin.Round,
                IsAntialias = true,
                BlendMode = SKBlendMode.Clear,
                StrokeWidth = size
            };
        }

        public override void Render(DrawingContext context)
        {
            base.Render(context);

            var displayWidth = (int)Bounds.Width;
            var displayHeight = (int)Bounds.Height;

            if (displayWidth <= 0 || displayHeight <= 0) return;

            EnsureWetInkSurface(displayWidth, displayHeight);

            if (_displayBitmap == null || _displayBitmap.PixelSize.Width != displayWidth || _displayBitmap.PixelSize.Height != displayHeight)
            {
                _displayBitmap?.Dispose();
                _displayBitmap = new WriteableBitmap(new PixelSize(displayWidth, displayHeight), new Vector(96, 96), PixelFormat.Bgra8888, AlphaFormat.Premul);
            }

            using (var fb = _displayBitmap.Lock())
            {
                var info = new SKImageInfo(fb.Size.Width, fb.Size.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
                using var surface = SKSurface.Create(info, fb.Address, fb.RowBytes);
                surface.Canvas.Clear(SKColors.Transparent);

                if (_isWhiteboardMode && _whiteboardBackgroundBitmap != null)
                {
                    var canvas = surface.Canvas;
                    var bounds = Bounds;

                    var imgWidth = _whiteboardBackgroundBitmap.Width;
                    var imgHeight = _whiteboardBackgroundBitmap.Height;

                    var scaleX = (float)bounds.Width / imgWidth;
                    var scaleY = (float)bounds.Height / imgHeight;
                    var scale = Math.Min(scaleX, scaleY);

                    var destWidth = imgWidth * scale;
                    var destHeight = imgHeight * scale;
                    var destX = ((float)bounds.Width - destWidth) / 2;
                    var destY = ((float)bounds.Height - destHeight) / 2;

                    var destRect = new SKRect(destX, destY, destX + destWidth, destY + destHeight);
                    canvas.DrawBitmap(_whiteboardBackgroundBitmap, destRect);
                }

                if (_isDrawing && _currentIsEraser && _tempStrokes != null)
                {
                    RenderStrokes(surface.Canvas, _tempStrokes);
                }
                else
                {
                    RenderStrokes(surface.Canvas, _strokes);
                }

                if (_wetInkSurface != null)
                {
                    using var wetSnapshot = _wetInkSurface.Snapshot();
                    surface.Canvas.DrawImage(wetSnapshot, 0, 0);
                }
            }

            context.DrawImage(_displayBitmap, new Rect(0, 0, displayWidth, displayHeight), Bounds);
        }

        private void RenderStrokes(SKCanvas canvas, List<InkStroke> strokes)
        {
            foreach (var stroke in strokes)
            {
                RenderStroke(canvas, stroke);
            }
        }

        private void RenderStroke(SKCanvas canvas, InkStroke stroke)
        {
            if (stroke.VideoPoints == null || stroke.VideoPoints.Count < 2) return;

            if (stroke.PointWidths != null && stroke.PointWidths.Count == stroke.VideoPoints.Count)
            {
                RenderVariableWidthStroke(canvas, stroke);
            }
            else
            {
                var displaySize = stroke.Size * (float)_currentZoom;
                var paint = CreatePenPaint(stroke.Color, displaySize);

                for (int i = 1; i < stroke.VideoPoints.Count; i++)
                {
                    var screenFrom = VideoToScreen(stroke.VideoPoints[i - 1]);
                    var screenTo = VideoToScreen(stroke.VideoPoints[i]);
                    canvas.DrawLine(screenFrom, screenTo, paint);
                }
            }
        }

        private void RenderVariableWidthStroke(SKCanvas canvas, InkStroke stroke)
        {
            if (stroke.VideoPoints == null || stroke.PointWidths == null) return;

            using var paint = new SKPaint
            {
                Style = SKPaintStyle.Fill,
                IsAntialias = true,
                Color = stroke.Color
            };

            for (int i = 0; i < stroke.VideoPoints.Count - 1; i++)
            {
                var p1 = VideoToScreen(stroke.VideoPoints[i]);
                var p2 = VideoToScreen(stroke.VideoPoints[i + 1]);
                var w1 = stroke.PointWidths[i] * (float)_currentZoom;
                var w2 = stroke.PointWidths[i + 1] * (float)_currentZoom;

                RenderSmoothSegment(canvas, p1, p2, w1, w2, paint);
            }
        }

        private void RenderSmoothSegment(SKCanvas canvas, SKPoint p1, SKPoint p2, float w1, float w2, SKPaint paint)
        {
            float dx = p2.X - p1.X;
            float dy = p2.Y - p1.Y;
            float length = (float)Math.Sqrt(dx * dx + dy * dy);

            if (length < 0.5f)
            {
                canvas.DrawCircle(p1, w1 / 2, paint);
                return;
            }

            int subdivisions = Math.Max(2, (int)(length / 2f));

            for (int i = 0; i <= subdivisions; i++)
            {
                float t = i / (float)subdivisions;
                float x = p1.X + dx * t;
                float y = p1.Y + dy * t;
                float w = w1 + (w2 - w1) * t;

                canvas.DrawCircle(new SKPoint(x, y), w / 2, paint);
            }
        }

        protected override Size MeasureOverride(Size availableSize)
        {
            return availableSize;
        }

        public void ClearAll()
        {
            _strokes.Clear();
            _currentVideoPoints = null;
            _currentPointWidths = null;
            _currentTimestamps = null;
            _tempStrokes = null;
            _hasLastEraserPoint = false;
            _wetInkCanvas?.Clear(SKColors.Transparent);
            InvalidateVisual();
        }

        public void Undo()
        {
            if (_strokes.Count == 0) return;

            _strokes.RemoveAt(_strokes.Count - 1);
            InvalidateVisual();
        }

        public int StrokeCount => _strokes.Count;

        private double CalculateTouchArea(PointerEventArgs e)
        {
            try
            {
                var pointerPoint = e.GetCurrentPoint(this);
                var bounds = pointerPoint.Properties.ContactRect;
                return bounds.Width * bounds.Height;
            }
            catch
            {
                return 0;
            }
        }

        private void ActivatePalmEraser(Point screenPoint)
        {
            if (_isPalmEraserActive) return;

            _lastModeBeforePalmEraser = IsPenMode;
            _isPalmEraserActive = true;
            _isDrawing = false;

            if (_wetInkCanvas != null)
            {
                _wetInkCanvas.Clear(SKColors.Transparent);
            }
        }

        private void DeactivatePalmEraser()
        {
            if (!_isPalmEraserActive) return;

            _isPalmEraserActive = false;
            InvalidateVisual();
        }

        private float CalculatePalmEraserSize(double touchArea)
        {
            double baseSize = Math.Sqrt(touchArea) * 0.1;
            return Math.Max(20, (float)baseSize);
        }

        private void ApplyEraserAtPoint(SKPoint videoPoint, float eraserSize)
        {
            if (_strokes.Count == 0) return;

            if (_tempStrokes == null)
            {
                _tempStrokes = CloneStrokes(_strokes);
            }

            var newStrokes = new List<InkStroke>();
            float eraserRadius = eraserSize / 2f;

            foreach (var stroke in _tempStrokes)
            {
                if (stroke.VideoPoints == null || stroke.VideoPoints.Count < 2)
                {
                    newStrokes.Add(stroke);
                    continue;
                }

                var segments = EraseStrokeWithCircle(stroke, videoPoint, eraserRadius);
                newStrokes.AddRange(segments);
            }

            _tempStrokes = newStrokes;
        }

        public void Dispose()
        {
            _wetInkCanvas?.Dispose();
            _wetInkSurface?.Dispose();
            _displayBitmap?.Dispose();
        }
    }

    public class InkStroke
    {
        public List<SKPoint>? VideoPoints { get; set; }
        public List<float>? PointWidths { get; set; }
        public bool IsEraser { get; set; }
        public float Size { get; set; }
        public SKColor Color { get; set; }
    }
}
