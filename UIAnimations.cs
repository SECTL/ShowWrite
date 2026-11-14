using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Styling;
using System;
using System.Threading.Tasks;

namespace ShowWrite
{
    public static class UIAnimations
    {
        public static async Task SlideInFromRight(Border panel, double width)
        {
            if (panel == null) return;

            panel.IsVisible = true;

            var transform = new TranslateTransform { X = width };
            panel.RenderTransform = transform;

            // 先完成布局，确保面板以屏幕外的初始位置参与首帧渲染，
            // 避免首次展开时动画在布局完成前就结束（高分屏下尤为明显）
            panel.UpdateLayout();

            await AnimateTranslateX(panel, transform, width, 0, TimeSpan.FromMilliseconds(250), new CubicEaseOut());
            transform.X = 0;
        }

        public static async Task SlideOutToRight(Border panel, double width)
        {
            if (panel == null) return;

            var transform = panel.RenderTransform as TranslateTransform;
            if (transform == null)
            {
                transform = new TranslateTransform { X = 0 };
                panel.RenderTransform = transform;
            }

            await AnimateTranslateX(panel, transform, transform.X, width, TimeSpan.FromMilliseconds(250), new CubicEaseIn());
            transform.X = width;
        }

        public static async Task SlideToolBackground(Border slider, double targetX)
        {
            if (slider == null) return;

            var transform = slider.RenderTransform as TranslateTransform;
            if (transform == null)
            {
                transform = new TranslateTransform { X = 0 };
                slider.RenderTransform = transform;
            }

            await AnimateTranslateX(slider, transform, transform.X, targetX, TimeSpan.FromMilliseconds(200), new CubicEaseOut());
        }

        /// <summary>
        /// 使用 Avalonia 动画时钟对平移变换做插值。
        /// 不要用 Dispatcher 循环逐帧收值：Render 优先级的连续投递会饿死 Layout 优先级，
        /// 导致首次显示的面板在动画结束后才完成布局渲染，动画看起来“不显示”。
        /// </summary>
        private static async Task AnimateTranslateX(Visual target, TranslateTransform transform, double from, double to, TimeSpan duration, Easing easing)
        {
            // 动画目标必须是 Visual：TransformAnimator 会把目标强转为 Visual，
            // 再从 RenderTransform 中找到对应类型的变换对象来插值，直接传 Transform 会抛 InvalidCastException。
            transform.X = from;

            var animation = new Animation
            {
                Duration = duration,
                Easing = easing,
                FillMode = FillMode.Forward,
                Children =
                {
                    new KeyFrame
                    {
                        Cue = new Cue(0d),
                        Setters = { new Setter(TranslateTransform.XProperty, from) }
                    },
                    new KeyFrame
                    {
                        Cue = new Cue(1d),
                        Setters = { new Setter(TranslateTransform.XProperty, to) }
                    }
                }
            };

            await animation.RunAsync(target);
        }
    }
}
