using Avalonia;
using System;
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
            }

            if (!RandomNoteMode)
            {
                AllocConsole();
                Console.Title = "ShowWrite - 控制台输出";
            }

            BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
        }

        public static AppBuilder BuildAvaloniaApp()
            => AppBuilder.Configure<App>()
                .UsePlatformDetect()
                .WithInterFont()
                .LogToTrace();
    }
}
