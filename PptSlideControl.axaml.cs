using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using System.Collections.Generic;

namespace ShowWrite
{
    public partial class PptSlideControl : UserControl
    {
        public static readonly StyledProperty<Stretch> StretchProperty =
            AvaloniaProperty.Register<PptSlideControl, Stretch>(nameof(Stretch), Stretch.Uniform);

        public Stretch Stretch
        {
            get { return GetValue(StretchProperty); }
            set { SetValue(StretchProperty, value); }
        }

        public PptSlideControl()
        {
            InitializeComponent();
        }

        public void LoadSlide(PptService.PptSlide slide)
        {
            // 清空现有内容
            SlideGrid.Children.Clear();

            // 设置背景颜色
            if (!string.IsNullOrEmpty(slide.BackgroundColor))
            {
                if (Avalonia.Media.Color.TryParse(slide.BackgroundColor, out var color))
                {
                    SlideGrid.Background = new Avalonia.Media.SolidColorBrush(color);
                }
            }
            else
            {
                // 默认背景为白色
                SlideGrid.Background = Avalonia.Media.Brushes.White;
            }

            // 添加幻灯片元素
            foreach (var shape in slide.Shapes)
            {
                Control shapeControl = CreateShapeControl(shape);
                if (shapeControl != null)
                {
                    // 设置位置和大小
                    shapeControl.Margin = new Thickness(shape.X, shape.Y, 0, 0);
                    shapeControl.Width = shape.Width;
                    shapeControl.Height = shape.Height;

                    // 添加到网格
                    SlideGrid.Children.Add(shapeControl);
                }
            }
        }

        private Control CreateShapeControl(PptService.PptShape shape)
        {
            if (!string.IsNullOrEmpty(shape.Text))
            {
                // 创建文本框
                var textBlock = new TextBlock
                {
                    Text = shape.Text,
                    TextWrapping = Avalonia.Media.TextWrapping.Wrap,
                    VerticalAlignment = Avalonia.Layout.VerticalAlignment.Top,
                    HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Left
                };

                // 设置背景颜色
                if (!string.IsNullOrEmpty(shape.FillColor))
                {
                    textBlock.Background = new SolidColorBrush(Color.Parse(shape.FillColor));
                }

                return textBlock;
            }
            else if (!string.IsNullOrEmpty(shape.ImagePath))
            {
                // 创建图片控件
                var image = new Image
                {
                    Stretch = Stretch.Uniform
                };

                // 这里需要加载图片，暂时返回空
                return null;
            }
            else
            {
                // 创建矩形
                var rectangle = new Border
                {
                    CornerRadius = new CornerRadius(0)
                };

                // 设置填充颜色
                if (!string.IsNullOrEmpty(shape.FillColor))
                {
                    rectangle.Background = new SolidColorBrush(Color.Parse(shape.FillColor));
                }

                // 设置边框
                if (!string.IsNullOrEmpty(shape.BorderColor))
                {
                    rectangle.BorderBrush = new SolidColorBrush(Color.Parse(shape.BorderColor));
                    rectangle.BorderThickness = new Thickness(shape.BorderWidth);
                }

                return rectangle;
            }
        }
    }
}