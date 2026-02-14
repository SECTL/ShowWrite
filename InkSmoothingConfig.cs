using System;

namespace Ink_Canvas.Helpers
{
    public class InkSmoothingConfig
    {
        public double SmoothingStrength { get; set; } = 0.4;
        public double ResampleInterval { get; set; } = 2.5;
        public int InterpolationSteps { get; set; } = 12;
        public double CurveTension { get; set; } = 0.3;
        public bool UseAdaptiveInterpolation { get; set; } = true;
        public int MaxPointsPerStroke { get; set; } = 1000;
        public double MinDistance { get; set; } = 0.3;
        public double OutlierAngleThreshold { get; set; } = Math.PI / 6;
        public bool RemoveNoisePoints { get; set; } = true;
        public bool UseHardwareAcceleration { get; set; } = true;
        public int MaxConcurrentTasks { get; set; } = Environment.ProcessorCount;
    }
}
