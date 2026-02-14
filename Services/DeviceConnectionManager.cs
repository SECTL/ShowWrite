using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media.Imaging;
using QRCoder;
using System.Drawing;
using System.IO;


namespace ShowWrite.Services
{
    /// <summary>
    /// 设备连接管理器
    /// 负责管理设备连接、端口开放和二维码生成
    /// </summary>
    public class DeviceConnectionManager
    {
        private TcpListener _tcpListener;
        private int _port = 8888;
        private bool _isListening = false;
        private string _connectionCode;
        private BitmapImage _qrCodeImage;
        private readonly LanguageManager _languageManager;

        public event Action<string> ConnectionStatusChanged;
        public event Action<string> ClientConnected;
        public event Action<byte[]> PhotoReceived;

        /// <summary>
        /// 连接状态
        /// </summary>
        public bool IsListening => _isListening;

        /// <summary>
        /// 端口号
        /// </summary>
        public int Port
        {
            get => _port;
            set => _port = value;
        }

        /// <summary>
        /// 连接码
        /// </summary>
        public string ConnectionCode => _connectionCode;

        /// <summary>
        /// 二维码图像
        /// </summary>
        public BitmapImage QrCodeImage => _qrCodeImage;

        /// <summary>
        /// 构造函数
        /// </summary>
        public DeviceConnectionManager()
        {
            _languageManager = LanguageManager.Instance;
        }

        /// <summary>
        /// 开始监听连接
        /// </summary>
        public async Task StartListeningAsync()
        {
            try
            {
                if (_isListening)
                    return;

                _tcpListener = new TcpListener(IPAddress.Any, _port);
                _tcpListener.Start();
                _isListening = true;

                var message = _languageManager.GetTranslation("WaitingForConnection").Replace("...", "");
                ConnectionStatusChanged?.Invoke($"{message} {_port}...");

                Logger.Info("DeviceConnectionManager", $"开始监听端口 {_port}");

                while (_isListening)
                {
                    TcpClient client = await _tcpListener.AcceptTcpClientAsync();
                    await HandleClientAsync(client);
                }
            }
            catch (Exception ex)
            {
                _isListening = false;
                Logger.Error("DeviceConnectionManager", $"监听失败: {ex.Message}", ex);
                ConnectionStatusChanged?.Invoke($"连接失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 停止监听
        /// </summary>
        public void StopListening()
        {
            try
            {
                _isListening = false;
                _tcpListener?.Stop();
                _tcpListener = null;

                ConnectionStatusChanged?.Invoke(_languageManager.GetTranslation("StoppedListening"));
                Logger.Info("DeviceConnectionManager", "已停止监听");
            }
            catch (Exception ex)
            {
                Logger.Error("DeviceConnectionManager", $"停止监听失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 处理客户端连接
        /// </summary>
        private async Task HandleClientAsync(TcpClient client)
        {
            try
            {
                Logger.Info("DeviceConnectionManager", "客户端已连接");
                ClientConnected?.Invoke(_languageManager.GetTranslation("DeviceConnected"));

                using (var stream = client.GetStream())
                {
                    var reader = new System.IO.StreamReader(stream, System.Text.Encoding.UTF8);
                    var writer = new System.IO.StreamWriter(stream, System.Text.Encoding.UTF8) { AutoFlush = true };

                    // 握手流程：等待客户端发送 SWEC_HELLO
                    var hello = await reader.ReadLineAsync();
                    Logger.Info("DeviceConnectionManager", $"收到: {hello}");

                    if (hello == "SWEC_HELLO")
                    {
                        // 第一步：发送 SWEC_ACK
                        await writer.WriteLineAsync("SWEC_ACK");
                        ConnectionStatusChanged?.Invoke("握手中...");
                        Logger.Info("DeviceConnectionManager", "发送: SWEC_ACK");

                        // 第二步：发送 SWEC_READY
                        await writer.WriteLineAsync("SWEC_READY");
                        ConnectionStatusChanged?.Invoke(_languageManager.GetTranslation("HandshakeSuccess"));
                        Logger.Info("DeviceConnectionManager", "发送: SWEC_READY");
                    }
                    else
                    {
                        Logger.Error("DeviceConnectionManager", $"握手失败，收到: {hello}");
                        ConnectionStatusChanged?.Invoke($"握手失败: 收到无效响应");
                        return;
                    }

                    // 握手完成后，开始接收照片
                    string message;
                    while ((message = await reader.ReadLineAsync()) != null)
                    {
                        Logger.Info("DeviceConnectionManager", $"收到消息: {message}");

                        if (message.StartsWith("PHOTO:"))
                        {
                            var base64Data = message.Substring(6);
                            var photoData = Convert.FromBase64String(base64Data);
                            PhotoReceived?.Invoke(photoData);
                            ConnectionStatusChanged?.Invoke(_languageManager.GetTranslation("PhotoReceived"));
                            
                            await writer.WriteLineAsync("PHOTO_OK");
                        }
                        else if (message == "PING")
                        {
                            await writer.WriteLineAsync("PONG");
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Logger.Error("DeviceConnectionManager", $"处理客户端连接失败: {ex.Message}", ex);
                ConnectionStatusChanged?.Invoke("连接断开，等待新设备连接...");
            }
            finally
            {
                client.Close();
            }
        }

        /// <summary>
        /// 生成连接码
        /// </summary>
        public string GenerateConnectionCode()
        {
            _connectionCode = Guid.NewGuid().ToString("N").Substring(0, 8).ToUpper();
            Logger.Info("DeviceConnectionManager", $"生成连接码: {_connectionCode}");
            return _connectionCode;
        }

        /// <summary>
        /// 生成二维码
        /// </summary>
        public void GenerateQrCode()
        {
            try
            {
                var ipAddress = GetLocalIPAddress();
                var qrContent = $"{ipAddress}:{_port}";
                
                var generator = new QRCodeGenerator();
                var data = generator.CreateQrCode(qrContent, QRCodeGenerator.ECCLevel.Q);
                var qrCode = new QRCode(data);
                var barcodeBitmap = qrCode.GetGraphic(10, Color.Black, Color.White, true);

                _qrCodeImage = new BitmapImage();
                using (var stream = new System.IO.MemoryStream())
                {
                    barcodeBitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Png);
                    stream.Position = 0;
                    _qrCodeImage.BeginInit();
                    _qrCodeImage.CacheOption = BitmapCacheOption.OnLoad;
                    _qrCodeImage.StreamSource = stream;
                    _qrCodeImage.EndInit();
                    _qrCodeImage.Freeze();
                }

                Logger.Info("DeviceConnectionManager", $"二维码生成成功: {qrContent}");
            }
            catch (Exception ex)
            {
                Logger.Error("DeviceConnectionManager", $"生成二维码失败: {ex.Message}", ex);
            }
        }

        /// <summary>
        /// 获取本机IP地址
        /// </summary>
        public string GetLocalIPAddress()
        {
            try
            {
                // 获取所有网络接口
                var interfaces = System.Net.NetworkInformation.NetworkInterface.GetAllNetworkInterfaces()
                    .Where(ni => ni.OperationalStatus == System.Net.NetworkInformation.OperationalStatus.Up &&
                              ni.NetworkInterfaceType != System.Net.NetworkInformation.NetworkInterfaceType.Loopback &&
                              !ni.Description.Contains("Virtual") &&
                              !ni.Description.Contains("Hyper-V") &&
                              !ni.Description.Contains("VMware") &&
                              !ni.Description.Contains("VirtualBox") &&
                              !ni.Description.Contains("Tunnel"))
                    .ToList();

                // 如果没有找到网络接口，返回默认值
                if (!interfaces.Any())
                {
                    return "127.0.0.1";
                }

                // 优先获取有网关的网卡（当前正在使用的）
                foreach (var netInterface in interfaces)
                {
                    var ipProps = netInterface.GetIPProperties();
                    var gatewayAddresses = ipProps.GatewayAddresses;

                    if (gatewayAddresses != null && gatewayAddresses.Any())
                    {
                        foreach (var addrInfo in ipProps.UnicastAddresses)
                        {
                            if (addrInfo.Address.AddressFamily == AddressFamily.InterNetwork)
                            {
                                return addrInfo.Address.ToString();
                            }
                        }
                    }
                }

                // 如果没有找到有网关的网卡，返回第一个可用的IP
                foreach (var netInterface in interfaces)
                {
                    var ipProps = netInterface.GetIPProperties();
                    foreach (var addrInfo in ipProps.UnicastAddresses)
                    {
                        if (addrInfo.Address.AddressFamily == AddressFamily.InterNetwork)
                        {
                            return addrInfo.Address.ToString();
                        }
                    }
                }

                return "127.0.0.1";
            }
            catch (Exception ex)
            {
                Logger.Error("DeviceConnectionManager", $"获取IP地址失败: {ex.Message}", ex);
                return "127.0.0.1";
            }
        }
    }
}
