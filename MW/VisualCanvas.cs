using System.Windows;
using System.Windows.Ink;
using System.Windows.Media;

namespace ShowWrite
{
    /// <summary>
    /// 视觉画布，用于渲染手写笔预览
    /// </summary>
    public class VisualCanvas : FrameworkElement
    {
        private readonly DrawingVisual _drawingVisual;

        public VisualCanvas()
        {
            _drawingVisual = new DrawingVisual();
        }

        /// <summary>
        /// 渲染笔迹
        /// </summary>
        public void RenderStroke(Stroke stroke)
        {
            if (stroke == null) return;

            using (var drawingContext = _drawingVisual.RenderOpen())
            {
                var geometry = stroke.GetGeometry();
                if (geometry != null)
                {
                    drawingContext.DrawGeometry(
                        new SolidColorBrush(stroke.DrawingAttributes.Color),
                        new System.Windows.Media.Pen(new SolidColorBrush(stroke.DrawingAttributes.Color), stroke.DrawingAttributes.Width),
                        geometry);
                }
            }

            InvalidateVisual();
        }

        /// <summary>
        /// 清除画布
        /// </summary>
        public void Clear()
        {
            using (var drawingContext = _drawingVisual.RenderOpen())
            {
                drawingContext.Close();
            }
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext drawingContext)
        {
            base.OnRender(drawingContext);
            drawingContext.DrawDrawing(_drawingVisual.Drawing);
        }
    }
}
