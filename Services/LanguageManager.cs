using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace ShowWrite.Services
{
    /// <summary>
    /// 语言类型
    /// </summary>
    public enum LanguageType
    {
        [Description("简体中文")]
        SimplifiedChinese = 0,
        [Description("繁體中文")]
        TraditionalChinese = 1,
        [Description("文言")]
        ClassicalChinese = 2,
        [Description("English")]
        English = 3,
        [Description("བོད་ཡིག")]
        Tibetan = 4
    }

    /// <summary>
    /// 语言管理器
    /// </summary>
    public class LanguageManager : INotifyPropertyChanged
    {
        private static LanguageManager _instance;
        private LanguageType _currentLanguage = LanguageType.SimplifiedChinese;
        private readonly Dictionary<string, Dictionary<LanguageType, string>> _translations;
        private readonly Dictionary<string, string> _currentTranslations;

        public event Action LanguageChanged;
        public event PropertyChangedEventHandler PropertyChanged;

        /// <summary>
        /// 当前语言
        /// </summary>
        public LanguageType CurrentLanguage
        {
            get => _currentLanguage;
            set
            {
                if (_currentLanguage != value)
                {
                    _currentLanguage = value;
                    UpdateCurrentTranslations();
                    OnPropertyChanged(nameof(CurrentLanguage));
                    OnPropertyChanged(nameof(Translations));
                    LanguageChanged?.Invoke();
                }
            }
        }

        /// <summary>
        /// 当前语言的翻译字典（用于XAML绑定）
        /// </summary>
        public Dictionary<string, string> Translations
        {
            get
            {
                var result = new Dictionary<string, string>();
                foreach (var kvp in _currentTranslations)
                {
                    result[kvp.Key] = kvp.Value;
                }
                return result;
            }
        }

        /// <summary>
        /// 单例实例
        /// </summary>
        public static LanguageManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new LanguageManager();
                }
                return _instance;
            }
        }

        /// <summary>
        /// 私有构造函数
        /// </summary>
        private LanguageManager()
        {
            _translations = new Dictionary<string, Dictionary<LanguageType, string>>();
            _currentTranslations = new Dictionary<string, string>();
            InitializeTranslations();
            UpdateCurrentTranslations();
        }

        /// <summary>
        /// 更新当前翻译字典
        /// </summary>
        private void UpdateCurrentTranslations()
        {
            _currentTranslations.Clear();
            foreach (var kvp in _translations)
            {
                if (kvp.Value.ContainsKey(_currentLanguage))
                {
                    _currentTranslations[kvp.Key] = kvp.Value[_currentLanguage];
                }
            }
        }

        /// <summary>
        /// 获取翻译（通过索引器）
        /// </summary>
        public string this[string key]
        {
            get
            {
                if (_currentTranslations.ContainsKey(key))
                {
                    return _currentTranslations[key];
                }
                return key;
            }
        }

        /// <summary>
        /// 初始化翻译
        /// </summary>
        private void InitializeTranslations()
        {
            // 通用设置
            AddTranslation("Settings", "设置", "設置", "设制", "Settings", "སྒྲིག་འགོད");
            AddTranslation("GeneralSettings", "通用设置", "通用設置", "通用设制", "General Settings", "སྤྱི་སྤྱོད་སྒྲིག་འགོད");
            AddTranslation("AdvancedSettings", "高级设置", "高級設置", "高階设制", "Advanced Settings", "མཐོ་རིམ་སྒྲིག་འགོད");
            AddTranslation("StartupSettings", "启动图", "啟動圖", "启程图", "Startup Image", "སྒོ་འབྱེད་པར་རིས");
            AddTranslation("About", "关于", "關於", "关于", "About", "སྐོར་ལ");
            AddTranslation("LanguageSettings", "语言设置", "語言設置", "言语设制", "Language Settings", "སྐད་ཡིག་སྒྲིག་འགོད");
            AddTranslation("Language", "语言:", "語言:", "言语:", "Language:", "སྐད་ཡིག:");
            AddTranslation("LanguageChangeNote", "（更改语言后需要重启应用生效）", "（更改語言後需要重啟應用生效）", "（易言语後需重启方效）", "(Restart required to apply language changes)", "（སྐད་ཡིག་བསྒྱུར་རྗེས་མཉེན་ཆས་བསྐྱར་སྒོ་འབྱེད་དགོས）");

            // 界面主题
            AddTranslation("InterfaceTheme", "界面主题", "介面主題", "介面題旨", "Interface Theme", "མཐོང་ངོའི་བརྗོད་བྱ་");
            AddTranslation("UITheme", "UI主题:", "UI主題:", "UI題旨:", "UI Theme:", "UIབརྗོད་བྱ་:");
            AddTranslation("DarkTheme", "深色", "深色", "玄色", "Dark", "གསལ་མོ");
            AddTranslation("LightTheme", "浅色", "淺色", "素色", "Light", "སྨུག་མོ");
            AddTranslation("ThemeChangeNote", "（更改主题后需要重启应用生效）", "（更改主題後需要重啟應用生效）", "（易題旨後需重启方效）", "(Restart required to apply theme changes)", "（བརྗོད་བྱ་བསྒྱུར་རྗེས་མཉེན་ཆས་བསྐྱར་སྒོ་འབྱེད་དགོས）");

            // 启动设置
            AddTranslation("StartupSettings", "启动设置", "啟動設置", "启程设制", "Startup Settings", "སྒོ་འབྱེད་སྒྲིག་འགོད");
            AddTranslation("StartMaximized", "启动时最大化窗口", "啟動時最大化視窗", "启时极之牖", "Start maximized", "སྒོ་འབྱེད་སྐབས་སྒེའུ་ཁུང་ཆེ་ཤོས");
            AddTranslation("AutoStartCamera", "启动时自动打开摄像头", "啟動時自動開啟攝像頭", "启时自开鉴影匣", "Auto-start camera", "སྒོ་འབྱེད་སྐབས་པར་ཆས་རང་འགུལ་སྒོ་འབྱེད");
            AddTranslation("DefaultCamera", "默认摄像头:", "預設攝像頭:", "默鉴影匣:", "Default Camera:", "སྔོན་སྒྲིག་པར་ཆས:");

            // 默认工具设置
            AddTranslation("DefaultToolSettings", "默认工具设置", "預設工具設置", "默器设制", "Default Tool Settings", "སྔོན་སྒྲིག་ལག་ཆས་སྒྲིག་འགོད");
            AddTranslation("DefaultPenWidth", "默认画笔宽度:", "預設畫筆寬度:", "默画笔阔度:", "Default Pen Width:", "སྔོན་སྒྲིག་བྲིས་སྨྱུག་རྒྱ་ཁ:");
            AddTranslation("DefaultPenColor", "默认画笔颜色:", "預設畫筆顏色:", "默画笔色:", "Default Pen Color:", "སྔོན་སྒྲིག་བྲིས་སྨྱུག་ཚོན་མདོག:");
            AddTranslation("Red", "红色", "紅色", "朱色", "Red", "དམར་པོ");
            AddTranslation("Blue", "蓝色", "藍色", "碧色", "Blue", "སྔོ་པོ");
            AddTranslation("Green", "绿色", "綠色", "青色", "Green", "ལྗང་ཁུ");
            AddTranslation("Yellow", "黄色", "黃色", "黄色", "Yellow", "སེར་པོ");
            AddTranslation("White", "白色", "白色", "白色", "White", "དཀར་པོ");

            // 高级设置
            AddTranslation("VideoProcessing", "视频处理", "影片處理", "画作处置", "Video Processing", "བརྙན་འཕྲིན་ཐག་གཅོད");
            AddTranslation("EnableHardwareAccel", "启用硬件加速", "啟用硬體加速", "启用硬物辅速", "Enable Hardware Acceleration", "སྲ་ཆས་མྱུར་མགྱོགས་སྤྱོད");
            AddTranslation("EnableFrameProcessing", "启用实时画面处理", "啟用即時畫面處理", "启用实时画帧处置", "Enable Real-time Frame Processing", "དུས་ཐོག་བརྙན་རིས་ཐག་གཅོད་སྤྱོད");

            AddTranslation("PerformanceSettings", "性能设置", "效能設置", "效能设制", "Performance Settings", "ནུས་པ་སྒྲིག་འགོད");
            AddTranslation("FrameRateLimit", "帧率限制:", "幀率限制:", "帧率之限:", "Frame Rate Limit:", "བརྙན་ཚད་ཚད:");
            AddTranslation("NoLimit", "无限制", "無限制", "无制", "No Limit", "ཚད་མེད");

            AddTranslation("DeveloperMode", "开发者模式", "開發者模式", "匠作模式", "Developer Mode", "བཟོ་བསྐྲུན་པའི་རྣམ་པ");
            AddTranslation("EnableDeveloperMode", "启用开发者模式", "啟用開發者模式", "启用匠作模式", "Enable Developer Mode", "བཟོ་བསྐྲུན་པའི་རྣམ་པ་སྤྱོད");

            // 启动图设置
            AddTranslation("StartupImageSettings", "启动图设置", "啟動圖設置", "启程图设制", "Startup Image Settings", "སྒོ་འབྱེད་པར་རིས་སྒྲིག་འགོད");
            AddTranslation("StartupImageURL", "启动图片URL：", "啟動圖片URL：", "启程图之链：", "Startup Image URL:", "སྒོ་འབྱེད་པར་རིས་URL:");
            AddTranslation("StartupImageNote", "输入启动图片链接", "輸入啟動圖片連結", "输入启程图之链", "Enter startup image link", "སྒོ་འབྱེད་པར་རིས་ཀྱི་སྦྲེལ་ཐག་ནང་འཇུག");
            AddTranslation("StartupImageNote1", "请注意，你需要输入一个直链。", "請注意，你需要輸入一個直連。", "注意，需输直链。", "Note: You need to enter a direct link.", "དོ་ཁུར་བྱོས། ཁྱེད་ཀྱིས་ཐད་ཀར་སྦྲེལ་ཐག་ཅིག་ནང་འཇུག་བྱེད་དགོས།");
            AddTranslation("StartupImageNote2", "输入启动图片的直链URL（支持常见的图片格式如：.jpg, .png. .gif等）", "輸入啟動圖片的直連URL（支援常見的圖片格式如：.jpg, .png. .gif等）", "输入启程图直链（可纳诸式，若 .jpg, .png. .gif 等）", "Enter direct URL of startup image (supports common image formats like: .jpg, .png. .gif, etc.)", "སྒོ་འབྱེད་པར་རིས་ཀྱི་ཐད་ཀར་སྦྲེལ་ཐག་URLནང་འཇུག་བྱོས། （.jpg, .png. .gif སོགས་ཀྱི་རྒྱུན་སྤྱོད་པར་རིས་རྣམ་པ་རྒྱབ་སྐྱོར་བྱེད）");
            AddTranslation("StartupImageNote3", "提示：", "提示：", "示：", "Tips:", "དྲན་སྐུལ:");
            AddTranslation("StartupImageNote4", "1. 必须是可直接访问的图片直链（以.jpg、.png、.gif等图片格式结尾）", "1. 必須是可直接訪問的圖片直連（以.jpg、.png、.gif等圖片格式結尾）", "一、须为可直访之图链（尾缀须若 .jpg、.png、.gif 等图式）", "1. Must be a directly accessible image link (ending with .jpg, .png, .gif, etc.)", "༡. ཐད་ཀར་སྤྱོད་ཆོག་པའི་པར་རིས་ཀྱི་ཐད་ཀར་སྦྲེལ་ཐག་ཅིག་དགོས། （.jpg, .png, .gif སོགས་ཀྱི་པར་རིས་རྣམ་པ་མཇུག་རྫོགས་སུ་འགྱུར་བ）");
            AddTranslation("StartupImageNote5", "2. 支持常见的图片托管服务（如Imgur、GitHub Pages等）", "2. 支援常見的圖片託管服務（如Imgur、GitHub Pages等）", "二、可纳常见图库（若 Imgur、GitHub Pages 等）", "2. Supports common image hosting services (e.g., Imgur, GitHub Pages, etc.)", "༢. རྒྱུན་སྤྱོད་པར་རིས་བདག་གཉེར་ཞབས་ཞུ་རྒྱབ་སྐྱོར་བྱེད། （Imgur, GitHub Pages སོགས）");
            AddTranslation("StartupImageNote6", "3. 清空文本框将使用默认启动图", "3. 清空文本框將使用預設啟動圖", "三、若清空文匣，则用默启程图", "3. Clear text box to use default startup image", "༣. ཚིག་སྒྲོམ་སྟོང་པོར་གཏོང་ན་སྔོན་སྒྲིག་སྒོ་འབྱེད་པར་རིས་སྤྱོད་པ");

            // 关于
            AddTranslation("ShowWriteVideoPresenter", "ShowWrite 视频展台", "ShowWrite 影片展台", "ShowWrite 影画展台", "ShowWrite Video Presenter", "ShowWrite བརྙན་འཕྲིན་བཤམས་སྟེགས");
            AddTranslation("Version", "版本: ", "版本: ", "版本：", "Version: ", "པར་གཞི: ");
            AddTranslation("Copyright", "© 智教联盟", "© 智教聯盟", "© 智教盟", "© Smart Education Alliance", "© རིག་གནས་སློབ་གསོའི་མཐུན་ཚོགས");
            AddTranslation("BasedOnLibraries", "本软件基于AForge.NET和ZXing库开发", "本軟體基於AForge.NET和ZXing庫開發", "此器本于 AForge.NET 与 ZXing 库而作", "This software is developed based on AForge.NET and ZXing libraries", "མཉེན་ཆས་འདི་AForge.NETདང་ZXingགཞི་བཙུགས་ལ་བརྟེན་ནས་བཟོས་པ");

            AddTranslation("UpdateNote1", "本次更新的顺利发布，离不开", "本次更新的順利發布，離不開", "此番更易得以成布，赖于", "The successful release of this update would not be possible without", "ཐེངས་འདིའི་གསར་བཅོས་བདེ་བླག་ངང་སྤེལ་བར་");
            AddTranslation("UpdateNote2", "开发者（也就是我）", "開發者（也就是我）", "匠作者（即吾）", "the developer (that's me)", "བཟོ་བསྐྲུན་པ（ང་རང་རེད）");
            AddTranslation("UpdateNote3", "的持续投入，以及", "的持續投入，以及", "之恒心投注，并", "'s continuous dedication, and", "ཀྱི་རྒྱུན་མཐུད་མནོན་པ་དང་།");
            AddTranslation("UpdateNote4", "开发团队（还是我）", "開發團隊（還是我）", "匠作众（亦吾也）", "the development team (still me)", "བཟོ་བསྐྲུན་ཚོགས་པ（ང་རང་རེད）");
            AddTranslation("UpdateNote5", "的昼夜奋战。当然，也少不了各大 AI 大模型的助力。", "的晝夜奮戰。當然，也少不了各大 AI 大模型的助力。", "昼夜勤作。固然，亦赖诸 AI 大模之助也。", "'s day and night efforts. Of course, we also couldn't do it without help of various AI models.", "ཀྱི་ཉིན་མཚན་འབད་བརྩོན། དེ་བཞིན། AIམང་པོའི་རོགས་རམ་ཡང་མེད་དུ་མི་རུང");

            AddTranslation("ExperienceNote", "从 1.0 版本一路走来，我们经历了：", "從 1.0 版本一路走來，我們經歷了：", "自版次 1.0 行至今日，吾等所历：", "From version 1.0 to now, we have experienced:", "པར་གཞི་1.0ནས་ད་བར་ང་ཚོས་བརྒྱུད་པའི་ལོ་རྒྱུས་ནི།");

            AddTranslation("Achievement1", "• 很多次 Bug 修复", "• 很多次 Bug 修復", "• 虫蠹屡修", "• Many bug fixes", "• Bugམང་པོ་བཅོས་བསྒྲིགས");
            AddTranslation("Achievement2", "• 很多次 代码重构与优化", "• 很多次 代碼重構與優化", "• 码文数易其构而益精", "• Many code refactoring and optimizations", "• གཞི་བཟུང་གསར་བཅོས་དང་ལེགས་བཅོས་མང་པོ་བྱས");
            AddTranslation("Achievement3", "• 🙏 特别鸣谢：佛祖对本软件的加持与庇佑", "• 🙏 特別鳴謝：佛祖對本軟體的加持與庇佑", "• 🙏 特谢佛祖之加持与庇佑", "• 🙏 Special thanks: Buddha's blessings and protection for this software", "• 🙏 དམིགས་བསལ་གྱི་བཀའ་དྲིན་ཞུ་རྒྱུ། སངས་རྒྱས་ཀྱིས་མཉེན་ཆས་འདིར་སྲུང་སྐྱོབ་གནང་བ");
            AddTranslation("Achievement4", "（详情请参见 MainWindow.xaml.cs 中的神秘注释）", "（詳情請參見 MainWindow.xaml.cs 中的神秘註釋）", "（细末可见 MainWindow.xaml.cs 中玄注）", "(See mysterious comments in MainWindow.xaml.cs for details)", "（ཞིབ་ཕྲའི་གནས་ཚུལ་MainWindow.xaml.csནང་གི་ངོ་མཚར་བའི་མཆན་འགྲེལ་ལ་གཟིགས་རོགས）");

            AddTranslation("WishNote", "愿代码无 Bug，运行如飞。", "願代碼無 Bug，運行如飛。", "愿码文无蠹，行之如飞。", "May code be bug-free and run like wind.", "གཞི་བཟུང་ལBugམེད་པ་དང་། འཁོར་སྐྱོད་བྱེད་སྐབས་བྱ་རྒོད་བཞིན་མྱུར་བརྩོན་བྱུང");
            AddTranslation("DeveloperSignature", "—— 开发者 敬上", "—— 開發者 敬上", "—— 匠作者 谨呈", "—— Developer", "—— བཟོ་བསྐྲུན་པ་ནས་བཀའ་དྲིན་ཆེ་ཞུ");

            AddTranslation("CheckUpdate", "检查更新", "檢查更新", "检视更易", "Check Update", "གསར་བཅོས་བལྟ་བཤེར");
            AddTranslation("VisitWebsite", "访问官网", "訪問官網", "访官坊", "Visit Website", "དྲ་བ་གཙོ་བོར་བལྟ་བ");

            // 按钮
            AddTranslation("OK", "确定", "確定", "定", "OK", "གཏན་འཁེལ");
            AddTranslation("Cancel", "取消", "取消", "罢", "Cancel", "འདོར");

            // 连接设备
            AddTranslation("ConnectDevice", "连接设备", "連接設備", "连器具", "Connect Device", "སྒྲིག་ཆས་སྦྲེལ");
            AddTranslation("WaitingForConnection", "正在等待设备连接...", "正在等待設備連接...", "待器具连...", "Waiting for device connection...", "སྒྲིག་ཆས་སྦྲེལ་རྒྱུར་སྒུག་བཞིན་འདུག...");
            AddTranslation("DeviceConnected", "设备已连接", "設備已連接", "器具已连", "Device connected", "སྒྲིག་ཆས་སྦྲེལ་ཟིན");
            AddTranslation("HandshakeSent", "已发送握手信息 (SWEC_HELLO)", "已發送握手信息 (SWEC_HELLO)", "已发握手之讯 (SWEC_HELLO)", "Handshake sent (SWEC_HELLO)", "ལག་འཇུ་ཆ་འཕྲིན་སྐུར་ཟིན (SWEC_HELLO)");
            AddTranslation("HandshakeSuccess", "握手成功，等待照片传输...", "握手成功，等待照片傳輸...", "握手既成，待相片传输...", "Handshake successful, waiting for photo transfer...", "ལག་འཇུ་ལེགས་གྲུབ། པར་རིས་བརྒྱུད་སྐུར་ལ་སྒུག་བཞིན་འདུག...");
            AddTranslation("PhotoReceived", "收到照片", "收到照片", "得相片", "Photo received", "པར་རིས་ཐོབ་ཟིན");
            AddTranslation("IPAddress", "IP地址", "IP地址", "IP所在", "IP Address", "IPས་གནས");
            AddTranslation("PortNumber", "端口号", "端口號", "埠号", "Port Number", "སྒོ་ཁག་ཨང་རྟགས");
            AddTranslation("Refresh", "刷新", "刷新", "新之", "Refresh", "གསར་བསྒྱུར");
            AddTranslation("StoppedListening", "已停止监听", "已停止監聽", "已止监听", "Stopped listening", "ཉན་འཇོག་ཟིན");

            // 形状
            AddTranslation("Shape", "形状", "形狀", "形制", "Shape", "གཟུགས་རྣམ་པ");
            AddTranslation("Line", "直线", "直線", "直线", "Line", "ཐད་ཀའི་གྲུ་ཟུར");
            AddTranslation("Arrow", "箭头", "箭頭", "矢", "Arrow", "མདའ་མགོ");
            AddTranslation("Rectangle", "矩形", "矩形", "矩", "Rectangle", "གྲུ་བཞི་གཟུགས");
            AddTranslation("Ellipse", "椭圆", "橢圓", "椭圆", "Ellipse", "སྒོང་གི་གཟུགས");
            AddTranslation("Circle", "圆形", "圓形", "圆", "Circle", "ཟླུམ་གཟུགས");
            AddTranslation("DashedLine", "虚线", "虛線", "虚画", "Dashed Line", "སྟོང་གྲུ་ཟུར");
            AddTranslation("DotLine", "点线", "點線", "点画", "Dot Line", "ཚེག་གི་གྲུ་ཟུར");

            // 画笔
            AddTranslation("Pen", "画笔", "畫筆", "画笔", "Pen", "བྲིས་སྨྱུག");
            AddTranslation("Eraser", "橡皮擦", "橡皮擦", "拭", "Eraser", "བསུབ་ཆས");

            // 照片
            AddTranslation("Photos", "照片", "照片", "相片", "Photos", "པར་རིས");
            AddTranslation("PhotoRecords", "拍摄记录", "拍攝記錄", "摄记", "Photo Records", "པར་ལེན་ཟིན་ཐོ");
            AddTranslation("ClickAgainToReturn", "再次点击，返回直播", "再次點擊，返回直播", "复点之，返现观", "Click again to return to live mode", "ཡང་བསྐྱར་ནོན་ན་ཐད་གཏོང་ལ་ལོག");
            AddTranslation("Expand", "展开", "展開", "展", "Expand", "ཁྱབ་བརྡལ");
            AddTranslation("Collapse", "收起", "收起", "收", "Collapse", "བསྡུ");
            AddTranslation("Import", "导入", "導入", "入", "Import", "ནངེན་འདྲས");

            // 主界面
            AddTranslation("Camera", "摄像头", "攝像頭", "鉴影匣", "Camera", "པར་ཆས");
            AddTranslation("Capture", "拍摄", "拍攝", "摄", "Capture", "པར་ལེན");
            AddTranslation("Scan", "扫描", "掃描", "扫", "Scan", "བཤུས་བཟོ");
            AddTranslation("Clear", "清除", "清除", "清", "Clear", "བསལ");
            AddTranslation("ClearScreenConfirm", "确认清屏", "確認清屏", "确清屏", "Clear Screen Confirm", "བསལ་གཏན");
            AddTranslation("SlideToConfirm", "滑动滑块确认清除所有笔迹", "滑動滑塊確認清除所有筆跡", "滑块以确清迹", "Slide to confirm clear all strokes", "བཤུས་བཟོ་བསལ་གཏན་བྱེད");
            AddTranslation("Undo", "撤销", "撤銷", "撤", "Undo", "ཕྱིར་འཐེན");
            AddTranslation("Redo", "重做", "重做", "复", "Redo", "བསྐྱར་བཟོ");
            AddTranslation("ZoomIn", "放大", "放大", "展", "Zoom In", "ཆེར་བསྐྱེད");
            AddTranslation("ZoomOut", "缩小", "縮小", "缩", "Zoom Out", "ཆུང་དུ་གཏོང");
            AddTranslation("ResetZoom", "重置缩放", "重置縮放", "复位", "Reset Zoom", "སླར་གསོ");
            AddTranslation("FitToScreen", "适应屏幕", "適應屏幕", "适屏", "Fit to Screen", "བརྙན་ཤེལ་ལ་སྒྲིབ");
            AddTranslation("RotateLeft", "向左旋转", "向左旋轉", "左旋", "Rotate Left", "གཡོན་ལ་བསྐོར");
            AddTranslation("RotateRight", "向右旋转", "向右旋轉", "右旋", "Rotate Right", "གཡས་ལ་བསྐོར");
            AddTranslation("FlipHorizontal", "水平翻转", "水平翻轉", "水平转", "Flip Horizontal", "ཆུ་ཚད་བརྗེ");
            AddTranslation("FlipVertical", "垂直翻转", "垂直翻轉", "垂直转", "Flip Vertical", "ཀྲང་ཚད་བརྗེ");
            AddTranslation("Brightness", "亮度", "亮度", "明", "Brightness", "སྣང་ཚད");
            AddTranslation("Contrast", "对比度", "對比度", "对比", "Contrast", "གསལ་ཚད");
            AddTranslation("PerspectiveCorrection", "梯形校正", "梯形校正", "梯正", "Perspective Correction", "རི་མོ་སྒྱུར");
            AddTranslation("Fullscreen", "全屏", "全屏", "满屏", "Fullscreen", "བརྙན་ཤེལ་ཆ་ཚང");
            AddTranslation("ExitFullscreen", "退出全屏", "退出全屏", "退满屏", "Exit Fullscreen", "བརྙན་ཤེལ་ལས་ཐོན");
            AddTranslation("Minimize", "最小化", "最小化", "极小", "Minimize", "ཆུང་ཤོས་སུ་གཏོང");
            AddTranslation("Close", "关闭", "關閉", "闭", "Close", "སྒོ་རྒྱག");
            AddTranslation("ConfirmClose", "确认关闭", "確認關閉", "确闭", "Confirm Close", "སྒོ་རྒྱག་གཏན་འཁེལ");
            AddTranslation("ConfirmCloseMessage", "确定要关闭应用吗？", "確定要關閉應用嗎？", "确欲闭此器乎？", "Are you sure you want to close application?", "གོ་རིམ་སྒོ་རྒྱག་དགོས་སམ");

            // 画笔设置
            AddTranslation("PenSettings", "画笔设置", "畫筆設置", "画笔设制", "Pen Settings", "བྲིས་སྨྱུག་སྒྲིག་འགོད");
            AddTranslation("PenWidth", "画笔宽度", "畫筆寬度", "画笔阔度", "Pen Width", "བྲིས་སྨྱུག་རྒྱ་ཁ");
            AddTranslation("PenColor", "画笔颜色", "畫筆顏色", "画笔色", "Pen Color", "བྲིས་སྨྱུག་ཚོན་མདོག");
            AddTranslation("ColorPicker", "颜色选择器", "顏色選擇器", "选色器", "Color Picker", "ཚོན་མདོག་འདེམས་ཆས");
            AddTranslation("CustomColor", "自定义颜色", "自定義顏色", "自定色", "Custom Color", "རང་སྒྲིག་ཚོན་མདོག");
            AddTranslation("MoreColors", "更多颜色...", "更多顏色...", "多色...", "More Colors...", "ཚོན་མདོག་མང་པོ...");
            AddTranslation("CurrentColor", "当前颜色", "當前顏色", "当前色", "Current Color", "ད་ལྟའི་ཚོན་མདོག");
            AddTranslation("CurrentPenWidth", "当前画笔宽度", "當前畫筆寬度", "当前画笔阔度", "Current Pen Width", "ད་ལྟའི་བྲིས་སྨྱུག་རྒྱ་ཁ");

            // 颜色名称
            AddTranslation("Black", "黑色", "黑色", "黑色", "Black", "སྨུག་པོ");
            AddTranslation("Red", "红色", "紅色", "朱色", "Red", "དམར་པོ");
            AddTranslation("Green", "绿色", "綠色", "青色", "Green", "ལྗང་ཁུ");
            AddTranslation("Blue", "蓝色", "藍色", "碧色", "Blue", "སྔོ་པོ");
            AddTranslation("Yellow", "黄色", "黃色", "黄色", "Yellow", "སེར་པོ");
            AddTranslation("White", "白色", "白色", "白色", "White", "དཀར་པོ");
            AddTranslation("Orange", "橙色", "橙色", "橙色", "Orange", "ཟིང་ཁ");
            AddTranslation("Purple", "紫色", "紫色", "紫色", "Purple", "སྨུག་སྨིག");
            AddTranslation("Cyan", "青色", "青色", "青色", "Cyan", "སྔོ་སྨུག");
            AddTranslation("Magenta", "洋红色", "洋紅色", "洋红", "Magenta", "དམར་སྨུག");
            AddTranslation("Brown", "棕色", "棕色", "褐色", "Brown", "སྨུག་སྨིག་སྨུག");
            AddTranslation("Pink", "粉色", "粉色", "粉", "Pink", "དམར་སྨུག་སྨུག");
            AddTranslation("Gray", "灰色", "灰色", "灰色", "Gray", "སྨུག་པོ");
            AddTranslation("DarkRed", "深红色", "深紅色", "深朱", "Dark Red", "དམར་པོ་གཏིང");
            AddTranslation("DarkGreen", "深绿色", "深綠色", "深青", "Dark Green", "ལྗང་ཁུ་གཏིང");
            AddTranslation("DarkBlue", "深蓝色", "深藍色", "深碧", "Dark Blue", "སྔོ་པོ་གཏིང");
            AddTranslation("Gold", "金色", "金色", "金", "Gold", "གསེར་མདོག");
            AddTranslation("Silver", "银色", "銀色", "银", "Silver", "དངུལ་མདོག");
            AddTranslation("Lime", "柠檬色", "檸檬色", "柠檬黄", "Lime", "ལིམ་མདོག");
            AddTranslation("Teal", "蓝绿色", "藍綠色", "蓝绿", "Teal", "སྔོ་ལྗང");

            // 画面调节窗口
            AddTranslation("AdjustVideo", "画面调节", "畫面調節", "画调", "Adjust Video", "བརྙན་རིས་སྒྲིག་འགོད");
            AddTranslation("Brightness", "亮度", "亮度", "明", "Brightness", "སྣང་ཚད");
            AddTranslation("Contrast", "对比度", "對比度", "对比", "Contrast", "གསལ་ཚད");
            AddTranslation("RotationDirection", "旋转方向", "旋轉方向", "旋向", "Rotation Direction", "བསྐོར་ཁ");
            AddTranslation("HorizontalMirror", "水平镜像", "水平鏡像", "水平镜", "Horizontal Mirror", "ཆུ་ཚད་མེ་ལོང");
            AddTranslation("VerticalMirror", "垂直镜像", "垂直鏡像", "垂直镜", "Vertical Mirror", "ཀྲང་ཚད་མེ་ལོང");
            AddTranslation("Degrees", "°", "°", "度", "°", "°");

            // 启动窗口
            AddTranslation("ShowWrite", "ShowWrite", "ShowWrite", "ShowWrite", "ShowWrite", "ShowWrite");
            AddTranslation("VideoPresenter", "视频展台", "影片展台", "画作展台", "Video Presenter", "བརྙན་འཕྲིན་བཤམས་སྟེགས");
            AddTranslation("OpenSourceVideoPresenter", "开 源 视 频 展 台 行 业 跟 跑 者", "開 源 影 片 展 台 行 業 跟 跑 者", "开源影画展台行业从行者", "Open Source Video Presenter Industry Follower", "གསར་བཏོད་བརྙན་འཕྲིན་བཤམས་སྟེགས་ལས་རིགས་རྗེས་སྙེག");
            AddTranslation("SoftwareVersion", "软件版本", "軟體版本", "器版本", "Software Version", "མཉེན་ཆས་པར་གཞི");
            AddTranslation("StartingUp", "正在启！动！", "正在啟！動！", "正启！动！", "Starting Up!", "སྒོ་འབྱེད་བཞིན་འདུག");
            AddTranslation("LoadingImage", "加载图片中...", "加載圖片中...", "载图中...", "Loading Image...", "པར་རིས་སྒྲིལ་བཞིན་འདུག...");

            // 选色悬浮窗
            AddTranslation("Close", "关闭", "關閉", "闭", "Close", "སྒོ་རྒྱག");
            AddTranslation("PenWidthLabel", "笔宽:", "筆寬:", "笔阔:", "Pen Width:", "བྲིས་སྨྱུག་རྒྱ་ཁ:");
            AddTranslation("ColorLabel", "颜色:", "顏色:", "色:", "Color:", "ཚོན་མདོག:");

            // 橡皮擦设置
            AddTranslation("EraserSettings", "橡皮擦设置", "橡皮擦設置", "拭设制", "Eraser Settings", "བསུབ་ཆས་སྒྲིག་འགོད");
            AddTranslation("EraserSize", "橡皮擦大小", "橡皮擦大小", "拭大小", "Eraser Size", "བསུབ་ཆས་ཆེ་ཆུང");
            AddTranslation("EraserSizeMethod", "橡皮擦大小计算方法", "橡皮擦大小計算方法", "拭大小计法", "Eraser Size Calculation Method", "བསུབ་ཆས་ཆེ་ཆུང་རྩིས་རྒྱག་ཐབས");
            AddTranslation("ManualEraserSize", "手动设置橡皮擦大小", "手動設置橡皮擦大小", "手设拭大小", "Manual Eraser Size", "ལག་བཟུང་བསུབ་ཆས་ཆེ་ཆུང་སྒྲིག");
            AddTranslation("AreaThreshold", "面积阈值", "面積閾值", "积阈", "Area Threshold", "རྒྱ་ཁྱོན་ཚད");
            AddTranslation("SpeedThreshold", "速度阈值", "速度閾值", "速阈", "Speed Threshold", "མྱུར་ཚད་ཚད");
            AddTranslation("PalmEraser", "手掌橡皮擦", "手掌橡皮擦", "掌拭", "Palm Eraser", "ཐལ་ལྕགས་བསུབ་ཆས");
            AddTranslation("PalmEraserThreshold", "手掌橡皮擦阈值", "手掌橡皮擦閾值", "掌拭阈", "Palm Eraser Threshold", "ཐལ་ལྕགས་བསུབ་ཆས་ཚད");

            // 状态消息
            AddTranslation("Ready", "就绪", "就緒", "备", "Ready", "གྲ་སྒྲིག");
            AddTranslation("Recording", "录制中", "錄製中", "录中", "Recording", "བརྙན་ལེན་བཞིན་འདུག");
            AddTranslation("Paused", "已暂停", "已暫停", "已止", "Paused", "བར་བསྣུབས");
            AddTranslation("Error", "错误", "錯誤", "误", "Error", "ནོར་འཁྲུལ");
            AddTranslation("Success", "成功", "成功", "成", "Success", "ལེགས་འགྲུབ");
            AddTranslation("Failed", "失败", "失敗", "败", "Failed", "ཕམ་ཉེས");
            AddTranslation("Loading", "加载中", "加載中", "载中", "Loading", "སྒྲིལ་བཞིན་འདུག");
            AddTranslation("Saving", "保存中", "保存中", "存中", "Saving", "ཉར་བཞིན་འདུག");
            AddTranslation("Saved", "已保存", "已保存", "已存", "Saved", "ཉར་ཟིན");
            AddTranslation("Captured", "已拍摄", "已拍攝", "已摄", "Captured", "པར་ལེན་ཟིན");
            AddTranslation("NoCameraDetected", "未检测到摄像头\n批注功能仍可使用", "未檢測到攝像頭\n批註功能仍可使用", "未检测到鉴影匣\n批注功能仍可使用", "No camera detected\nAnnotation function still available", "པར་ཆས་ཚོར་མི་ཐུབ\nཟུར་བརྒྱན་ནུས་པ་སྤྱོད་ཆོག");

            // 其他
            AddTranslation("More", "更多", "更多", "多", "More", "མང་པོ");
            AddTranslation("ScanQR", "扫一扫", "掃一掃", "扫", "Scan QR", "བཤུས་བཟོ");
            AddTranslation("Move", "移动", "移動", "移", "Move", "སྤོ");
            AddTranslation("Clear", "清除", "清除", "清", "Clear", "བསལ");
            AddTranslation("Capture", "拍摄", "拍攝", "摄", "Capture", "པར་ལེན");
            AddTranslation("ReturnLive", "返回实时", "返回實時", "返现观", "Return to Live", "ཐད་གཏོང་ལ་ལོག");
            AddTranslation("SaveImage", "保存图片", "保存圖片", "存图", "Save Image", "པར་རིས་ཉར");

            // 更多菜单
            AddTranslation("SwitchCamera", "切换摄像头", "切換攝像頭", "切换鉴影匣", "Switch Camera", "པར་ཆས་བརྗེ");
            AddTranslation("PerspectiveCorrection", "梯形校正", "梯形校正", "梯正", "Perspective Correction", "རི་མོ་སྒྱུར");
            AddTranslation("ClearCorrection", "清除校正", "清除校正", "清校正", "Clear Correction", "སྒྱུར་བཟོ་བསལ");
            AddTranslation("Exit", "退出", "退出", "退", "Exit", "ཐོན");

            // 梯形校正
            AddTranslation("Point1", "点1", "點1", "点1", "Point 1", "ས་ཚིགས་༡");
            AddTranslation("Point2", "点2", "點2", "点2", "Point 2", "ས་ཚིགས་༢");
            AddTranslation("Point3", "点3", "點3", "点3", "Point 3", "ས་ཚིགས་༣");
            AddTranslation("Point4", "点4", "點4", "点4", "Point 4", "ས་ཚིགས་༤");
            AddTranslation("Complete", "完成", "完成", "成", "Complete", "ལེགས་གྲུབ");
            AddTranslation("Reset", "重置", "重置", "复", "Reset", "སླར་གསོ");
            AddTranslation("Cancel", "取消", "取消", "罢", "Cancel", "འདོར");
            AddTranslation("CorrectionHint", "拖动红点调整校正区域，完成后点击'完成'按钮", "拖動紅點調整校正區域，完成後點擊'完成'按鈕", "拖红点调校正区，成后点'成'钮", "Drag red points to adjust correction area, then click 'Complete' button", "དམར་ས་ཚིགས་འདྲུད་ནས་སྒྱུར་བཟོ་ས་ཁོངས་སྒྲིག་པ། ལེགས་གྲུབ་རྗེས་'ལེགས་གྲུབ'མནན");

            // 触控信息
            AddTranslation("TouchInfo", "触控信息", "觸控信息", "触控信息", "Touch Info", "མཐེ་འཁོར་ཆ་འཕྲིན");
            AddTranslation("TouchPoints", "触控点数", "觸控點數", "触控点数", "Touch Points", "མཐེ་འཁོར་ས་ཚིགས་གྲངས");
            AddTranslation("Area", "面积", "面積", "面积", "Area", "རྒྱ་ཁྱོན");
            AddTranslation("SDKArea", "SDK识别面积", "SDK識別面積", "SDK识面积", "SDK Area", "SDKངོས་འཛིན་རྒྱ་ཁྱོན");
            AddTranslation("Center", "中心", "中心", "中心", "Center", "ལྟེ་བ");
        }

        /// <summary>
        /// 添加翻译
        /// </summary>
        private void AddTranslation(string key, string simplified, string traditional, string classical, string english, string tibetan = "")
        {
            if (!_translations.ContainsKey(key))
            {
                _translations[key] = new Dictionary<LanguageType, string>
                {
                    { LanguageType.SimplifiedChinese, simplified },
                    { LanguageType.TraditionalChinese, traditional },
                    { LanguageType.ClassicalChinese, classical },
                    { LanguageType.English, english },
                    { LanguageType.Tibetan, string.IsNullOrEmpty(tibetan) ? simplified : tibetan }
                };
            }
        }

        /// <summary>
        /// 获取翻译
        /// </summary>
        public string GetTranslation(string key)
        {
            if (_translations.ContainsKey(key))
            {
                return _translations[key][_currentLanguage];
            }
            return key;
        }

        /// <summary>
        /// 获取语言描述
        /// </summary>
        public string GetLanguageDescription(LanguageType language)
        {
            var field = language.GetType().GetField(language.ToString());
            var attribute = (DescriptionAttribute)field.GetCustomAttributes(typeof(DescriptionAttribute), false)[0];
            return attribute.Description;
        }

        /// <summary>
        /// 属性更改通知
        /// </summary>
        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
