using Avalonia;
using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

namespace ShowWrite
{
    internal class Program
    {
        [DllImport("kernel32.dll")]
        private static extern bool AllocConsole();

        [DllImport("kernel32.dll")]
        private static extern bool FreeConsole();

        public static bool RandomNoteMode { get; private set; }
        public static List<string> FilesToOpen { get; private set; } = new List<string>();

        private static readonly string[] SupportedImageExtensions = { ".jpg", ".jpeg", ".png", ".bmp", ".gif" };
        private static readonly string[] SupportedPdfExtensions = { ".pdf" };
        private static readonly string[] SupportedPptExtensions = { ".pptx", ".ppt" };

        [STAThread]
        public static void Main(string[] args)
        {
            foreach (var arg in args)
            {
                if (arg.Equals("--randomnote", StringComparison.OrdinalIgnoreCase) ||
                    arg.Equals("-rn", StringComparison.OrdinalIgnoreCase))
                {
                    RandomNoteMode = true;
                    break;
                }

                var ext = Path.GetExtension(arg).ToLowerInvariant();
                if (IsSupportedExtension(ext) && File.Exists(arg))
                {
                    FilesToOpen.Add(arg);
                }
            }

            if (!RandomNoteMode)
            {
                AllocConsole();
                Console.Title = "ShowWrite - 控制台输出";
            }

            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }

        private static bool IsSupportedExtension(string ext)
        {
            foreach (var supported in SupportedImageExtensions)
                if (ext == supported) return true;
            foreach (var supported in SupportedPdfExtensions)
                if (ext == supported) return true;
            foreach (var supported in SupportedPptExtensions)
                if (ext == supported) return true;
            return false;
        }

        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .WithInterFont()
                .LogToTrace();
    }
}
