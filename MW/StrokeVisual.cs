using StylusPointCollection = System.Windows.Input.StylusPointCollection;
using StylusPoint = System.Windows.Input.StylusPoint;
using Stroke = System.Windows.Ink.Stroke;
using DrawingAttributes = System.Windows.Ink.DrawingAttributes;

namespace ShowWrite
{
    /// <summary>
    /// 手写笔预览视觉元素
    /// 用于实时预览手写笔迹，提高性能
    /// </summary>
    public class StrokeVisual
    {
        private readonly DrawingAttributes _drawingAttributes;
        private readonly StylusPointCollection _stylusPoints;

        public StrokeVisual(DrawingAttributes drawingAttributes)
        {
            _drawingAttributes = drawingAttributes ?? new DrawingAttributes();
            _stylusPoints = new StylusPointCollection();
        }

        /// <summary>
        /// 添加笔触点
        /// </summary>
        public void Add(StylusPoint point)
        {
            _stylusPoints.Add(point);
        }

        /// <summary>
        /// 重绘预览
        /// </summary>
        public void Redraw()
        {
            if (_visualCanvas != null)
            {
                var stroke = new Stroke(_stylusPoints)
                {
                    DrawingAttributes = _drawingAttributes.Clone()
                };
                _visualCanvas.RenderStroke(stroke);
            }
        }

        /// <summary>
        /// 设置视觉画布
        /// </summary>
        public void SetVisualCanvas(VisualCanvas visualCanvas)
        {
            _visualCanvas = visualCanvas;
        }

        private VisualCanvas _visualCanvas;
    }
}
