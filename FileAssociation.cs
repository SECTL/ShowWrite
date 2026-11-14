using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace ShowWrite
{
    /// <summary>
    /// 把当前程序注册到 Windows 的“打开方式”列表（仅当前用户，无需管理员权限）。
    /// </summary>
    internal static class FileAssociation
    {
        public const string ProgIdImage = "ShowWrite.Image";
        public const string ProgIdPdf = "ShowWrite.Pdf";

        private const string ApplicationKeyNameFallback = "ShowWrite.exe";

        public static readonly string[] ImageExtensions = { ".jpg", ".jpeg", ".png", ".bmp", ".gif", ".webp" };
        public static readonly string[] PdfExtensions = { ".pdf" };

        [DllImport("shell32.dll")]
        private static extern void SHChangeNotify(int eventId, uint flags, IntPtr item1, IntPtr item2);

        private const int ShcneAssocChanged = 0x08000000;

        private static string ApplicationKeyName
        {
            get
            {
                var name = Path.GetFileName(ExePath);
                return string.IsNullOrEmpty(name) ? ApplicationKeyNameFallback : name;
            }
        }

        private static string ExePath
        {
            get
            {
                var path = Environment.ProcessPath;
                if (string.IsNullOrEmpty(path))
                    path = Process.GetCurrentProcess().MainModule?.FileName;
                return path ?? string.Empty;
            }
        }

        /// <summary>注册为图片文件的可用打开方式。</summary>
        public static void RegisterImage()
            => Register(ProgIdImage, "图片", ImageExtensions);

        /// <summary>注册为 PDF 文件的可用打开方式。</summary>
        public static void RegisterPdf()
            => Register(ProgIdPdf, "PDF 文档", PdfExtensions);

        /// <summary>取消图片文件关联注册。</summary>
        public static void UnregisterImage()
            => Unregister(ProgIdImage, ImageExtensions);

        /// <summary>取消 PDF 文件关联注册。</summary>
        public static void UnregisterPdf()
            => Unregister(ProgIdPdf, PdfExtensions);

        /// <summary>判断指定 ProgID 是否已注册。</summary>
        public static bool IsRegistered(string progId)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey($@"Software\Classes\{progId}");
                return key != null;
            }
            catch
            {
                return false;
            }
        }

        private static void Register(string progId, string description, string[] extensions)
        {
            var exe = ExePath;
            if (string.IsNullOrEmpty(exe) || !File.Exists(exe))
                throw new InvalidOperationException("无法定位当前程序路径");

            // 1) ProgID：供“打开方式”列表调用 "exe" "%1"
            using (var progKey = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{progId}"))
            {
                progKey.SetValue(null, description);
                using (var iconKey = progKey.CreateSubKey("DefaultIcon"))
                    iconKey.SetValue(null, $"\"{exe}\",0");
                using (var cmdKey = progKey.CreateSubKey(@"shell\open\command"))
                    cmdKey.SetValue(null, $"\"{exe}\" \"%1\"");
            }

            // 2) 注册到各扩展名的“打开方式”候选列表
            foreach (var ext in extensions)
            {
                using var extKey = Registry.CurrentUser.CreateSubKey($@"Software\Classes\{ext}\OpenWithProgids");
                extKey.SetValue(progId, Array.Empty<byte>(), RegistryValueKind.None);
            }

            // 3) Applications 注册，保证出现在“打开方式 > 更多应用”中
            using (var appKey = Registry.CurrentUser.CreateSubKey($@"Software\Classes\Applications\{ApplicationKeyName}"))
            {
                appKey.SetValue(null, "ShowWrite");
                appKey.SetValue("FriendlyAppName", "ShowWrite");
                using (var iconKey = appKey.CreateSubKey("DefaultIcon"))
                    iconKey.SetValue(null, $"\"{exe}\",0");
                using (var cmdKey = appKey.CreateSubKey(@"shell\open\command"))
                    cmdKey.SetValue(null, $"\"{exe}\" \"%1\"");
                using (var typesKey = appKey.CreateSubKey("SupportedTypes"))
                {
                    foreach (var ext in extensions)
                        typesKey.SetValue(ext, string.Empty);
                }
            }

            SHChangeNotify(ShcneAssocChanged, 0, IntPtr.Zero, IntPtr.Zero);
        }

        private static void Unregister(string progId, string[] extensions)
        {
            try
            {
                Registry.CurrentUser.DeleteSubKeyTree($@"Software\Classes\{progId}", false);
            }
            catch { }

            foreach (var ext in extensions)
            {
                try
                {
                    using var extKey = Registry.CurrentUser.OpenSubKey(
                        $@"Software\Classes\{ext}\OpenWithProgids", writable: true);
                    extKey?.DeleteValue(progId, false);
                }
                catch { }
            }

            try
            {
                var typesPath = $@"Software\Classes\Applications\{ApplicationKeyName}\SupportedTypes";
                using (var typesKey = Registry.CurrentUser.OpenSubKey(typesPath, writable: true))
                {
                    if (typesKey != null)
                    {
                        foreach (var ext in extensions)
                            typesKey.DeleteValue(ext, false);
                    }
                }

                // 已没有支持的扩展名时，移除整个 Applications 注册
                using (var checkKey = Registry.CurrentUser.OpenSubKey(typesPath))
                {
                    if (checkKey == null || checkKey.ValueCount == 0 && checkKey.SubKeyCount == 0)
                        Registry.CurrentUser.DeleteSubKeyTree($@"Software\Classes\Applications\{ApplicationKeyName}", false);
                }
            }
            catch { }

            SHChangeNotify(ShcneAssocChanged, 0, IntPtr.Zero, IntPtr.Zero);
        }
    }
}
