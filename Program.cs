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

        private static readonly string[] SupportedImageExtensions = { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp" };
        private static readonly string[] SupportedPdfExtensions = { ".pdf" };

        [STAThread]
        public static void Main(string[] args)
        {
            bool showConsole = false;

            foreach (var arg in args)
            {
                if (arg.Equals("--randomnote", StringComparison.OrdinalIgnoreCase) ||
                    arg.Equals("-rn", StringComparison.OrdinalIgnoreCase))
                {
                    RandomNoteMode = true;
                    continue;
                }

                if (arg.Equals("--debug", StringComparison.OrdinalIgnoreCase) ||
                    arg.Equals("-d", StringComparison.OrdinalIgnoreCase))
                {
                    showConsole = true;
                    continue;
                }

                var ext = Path.GetExtension(arg).ToLowerInvariant();
                if (IsSupportedExtension(ext) && File.Exists(arg))
                {
                    FilesToOpen.Add(Path.GetFullPath(arg));
                }
            }

            // 单实例：已有同模式实例时，把待打开文件转发过去并激活它的窗口，然后退出
            if (!SingleInstance.TryAcquire(RandomNoteMode, new List<string>(FilesToOpen)))
                return;

            // 控制台输出默认已关闭，仅在需要调试时通过参数启用
            if (showConsole)
            {
                AllocConsole();
                Console.Title = "ShowWrite - 控制台输出";
            }

            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }

        /// <summary>供已运行实例在启动阶段缓存转发过来的文件。</summary>
        public static void AddPendingFiles(IEnumerable<string> files)
        {
            lock (FilesToOpen)
            {
                foreach (var file in files)
                {
                    if (!string.IsNullOrEmpty(file) && !FilesToOpen.Contains(file))
                        FilesToOpen.Add(file);
                }
            }
        }

        /// <summary>取走待导入文件（主窗口创建后调用）。</summary>
        public static List<string> TakePendingFiles()
        {
            lock (FilesToOpen)
            {
                var files = new List<string>(FilesToOpen);
                FilesToOpen.Clear();
                return files;
            }
        }

        private static bool IsSupportedExtension(string ext)
        {
            foreach (var supported in SupportedImageExtensions)
                if (ext == supported) return true;
            foreach (var supported in SupportedPdfExtensions)
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
