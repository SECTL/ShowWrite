using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace ShowWrite
{
    public class LicenseManager
    {
        private static readonly string ConfigFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "ShowWrite",
            "license.config");

        private static LicenseManager? _instance;
        private static readonly object _lock = new();

        public string? CurrentUuid { get; private set; }

        public static LicenseManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    lock (_lock)
                    {
                        _instance ??= new LicenseManager();
                    }
                }
                return _instance;
            }
        }

        private LicenseManager()
        {
        }

        public Task<string> GetOrCreateLicenseAsync()
        {
            Debug.WriteLine("[LicenseManager] 开始获取或创建许可证");

            string localUuid = ReadLocalLicense();
            if (IsUuidV7(localUuid))
            {
                Debug.WriteLine($"[LicenseManager] 找到本地缓存的 UUIDv7: {localUuid}");
                CurrentUuid = localUuid;
                return Task.FromResult(localUuid);
            }

            // 本地无缓存或旧规则生成的 UUID：直接使用 UUIDv7 生成
            string uuid = CreateUuidV7().ToString();
            CurrentUuid = uuid;

            Debug.WriteLine($"[LicenseManager] 已生成 UUIDv7: {uuid}");

            SaveLocalLicense(uuid);
            Debug.WriteLine("[LicenseManager] UUID 已保存到本地");

            return Task.FromResult(uuid);
        }

        /// <summary>
        /// 生成 UUIDv7：48 位毫秒时间戳 + 版本号 7 + 74 位随机数。
        /// </summary>
        private static Guid CreateUuidV7()
        {
            Span<byte> bytes = stackalloc byte[16];

            long unixMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            bytes[0] = (byte)(unixMs >> 40);
            bytes[1] = (byte)(unixMs >> 32);
            bytes[2] = (byte)(unixMs >> 24);
            bytes[3] = (byte)(unixMs >> 16);
            bytes[4] = (byte)(unixMs >> 8);
            bytes[5] = (byte)unixMs;

            RandomNumberGenerator.Fill(bytes.Slice(6, 10));

            // 版本号 7
            bytes[6] = (byte)((bytes[6] & 0x0F) | 0x70);
            // 变体 10xxxxxx
            bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);

            return new Guid(bytes, bigEndian: true);
        }

        /// <summary>
        /// 判断字符串是否为 UUIDv7（第 7 字节高 4 位为版本号 7）。
        /// </summary>
        private static bool IsUuidV7(string value)
        {
            if (!Guid.TryParse(value, out var guid))
            {
                return false;
            }

            byte[] bytes = guid.ToByteArray(bigEndian: true);
            return (bytes[6] >> 4) == 7;
        }

        private string ReadLocalLicense()
        {
            try
            {
                if (File.Exists(ConfigFilePath))
                {
                    string content = File.ReadAllText(ConfigFilePath);
                    return Decrypt(content);
                }
            }
            catch
            {
            }
            return string.Empty;
        }

        private void SaveLocalLicense(string uuid)
        {
            try
            {
                string? directory = Path.GetDirectoryName(ConfigFilePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                string encrypted = Encrypt(uuid);
                File.WriteAllText(ConfigFilePath, encrypted);

                File.SetAttributes(ConfigFilePath, FileAttributes.Hidden);
            }
            catch
            {
            }
        }

        private static string Encrypt(string plainText)
        {
            var plainBytes = Encoding.UTF8.GetBytes(plainText);
            return Convert.ToBase64String(plainBytes);
        }

        private static string Decrypt(string cipherText)
        {
            var cipherBytes = Convert.FromBase64String(cipherText);
            return Encoding.UTF8.GetString(cipherBytes);
        }
    }
}
