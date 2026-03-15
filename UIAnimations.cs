using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Media;
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

            await AnimateTransform(transform, width, 0, TimeSpan.FromMilliseconds(250), new CubicEaseOut());
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

            await AnimateTransform(transform, 0, width, TimeSpan.FromMilliseconds(250), new CubicEaseIn());
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

            await AnimateTransform(transform, transform.X, targetX, TimeSpan.FromMilliseconds(200), new CubicEaseOut());
        }

        private static Task AnimateTransform(TranslateTransform transform, double from, double to, TimeSpan duration, Easing easing)
        {
            var tcs = new TaskCompletionSource<bool>();

            var startTime = DateTime.Now;
            var durationMs = duration.TotalMilliseconds;

            void Tick()
            {
                var elapsed = (DateTime.Now - startTime).TotalMilliseconds;
                var progress = Math.Min(elapsed / durationMs, 1.0);
                var easedProgress = easing.Ease(progress);

                transform.X = from + (to - from) * easedProgress;

                if (progress >= 1.0)
                {
                    tcs.SetResult(true);
                }
                else
                {
                    Avalonia.Threading.Dispatcher.UIThread.Post(Tick, Avalonia.Threading.DispatcherPriority.Render);
                }
            }

            Avalonia.Threading.Dispatcher.UIThread.Post(Tick, Avalonia.Threading.DispatcherPriority.Render);
            return tcs.Task;
        }
    }
}
