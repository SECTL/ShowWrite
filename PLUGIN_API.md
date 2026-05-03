# ShowWrite 插件 API 文档

## 目录
- [概述](#概述)
- [插件接口](#插件接口)
- [开发指南](#开发指南)
- [示例代码](#示例代码)

---

## 概述

ShowWrite 支持通过插件系统扩展功能。插件可以实现底部工具栏按钮、窗口交互、相机帧处理等功能。

### 插件加载位置
插件 DLL 文件需要放置在以下目录：
```
C:\Users\{用户名}\AppData\Roaming\ShowWrite\PKG\
```

### 依赖项
插件项目需要引用 ShowWrite 主程序：
```xml
<ItemGroup>
  <Reference Include="ShowWrite">
    <HintPath>..\..\bin\Debug\net8.0\ShowWrite.dll</HintPath>
  </Reference>
</ItemGroup>
```

---

## 插件接口

### IPlugin - 基础插件接口

所有插件必须实现此接口。

#### 属性
| 属性 | 类型 | 说明 |
|------|------|------|
| Name | string | 插件名称 |
| Version | string | 插件版本 |
| Description | string | 插件描述 |
| Author | string | 插件作者 |

#### 方法
| 方法 | 说明 |
|------|------|
| void Initialize() | 初始化插件，在插件加载时调用一次 |
| void OnLoad() | 插件加载完成时调用 |
| void OnUnload() | 插件卸载时调用，用于清理资源 |

---

### IBottomToolbarPlugin - 底部工具栏插件

实现此接口的插件可以在底部工具栏添加按钮。

#### 方法
| 方法 | 说明 |
|------|------|
| List<PluginToolbarButton> GetToolbarButtons() | 获取工具栏按钮列表 |
| void UpdateToolbarButtons() | 更新工具栏按钮状态 |
| void SetRefreshToolbarCallback(Action callback) | 设置刷新工具栏的回调 |

#### PluginToolbarButton 类
| 属性 | 类型 | 说明 |
|------|------|------|
| IconPath | string | SVG 图标路径 |
| Label | string | 按钮标签 |
| OnClick | Action? | 点击事件处理 |
| Order | int | 排序顺序（越小越靠前） |
| IsEnabled | bool | 是否启用 |

---

### IPluginWindow - 窗口交互插件

实现此接口的插件可以与主窗口交互，包括显示覆盖层、控制工具栏可见性、处理相机帧等。

#### 方法
| 方法 | 说明 |
|------|------|
| void SetPluginOverlay(Border overlay) | 设置插件覆盖层控件 |
| void SetToolbarVisibilityCallback(Action<bool> callback) | 设置工具栏可见性回调 |
| void OnPluginActivated() | 插件激活时调用 |
| void OnPluginDeactivated() | 插件停用时调用 |
| void OnCameraFrame(Bitmap? bitmap) | 相机帧回调，用于实时处理 |
| void SetShowResultWindowCallback(Action<string, bool, List<(string Text, Action Callback)>>? callback) | 设置显示结果窗口的回调 |
| void SetCancelCallback(Action callback) | 设置取消按钮的回调 |
| void SetShowNotificationCallback(Action<string, int>? callback) | 设置显示通知的回调 |

---

## 开发指南

### 1. 创建插件项目

创建一个 .NET 8.0 类库项目：

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <Reference Include="ShowWrite">
      <HintPath>..\..\bin\Debug\net8.0\ShowWrite.dll</HintPath>
    </Reference>
  </ItemGroup>
  <ItemGroup>
    <PackageReference Include="ZXing.Net" Version="0.16.9" />
    <PackageReference Include="System.Drawing.Common" Version="6.0.0" />
  </ItemGroup>
  <Target Name="CopyPluginToPKG" AfterTargets="Build">
    <Copy SourceFiles="$(TargetPath)" DestinationFolder="$(AppData)\ShowWrite\PKG\" />
  </Target>
</Project>
```

### 2. 实现插件类

```csharp
using ShowWrite;
using Avalonia.Controls;
using Avalonia.Media;
using System.Collections.Generic;

public class MyPlugin : IPluginWindow
{
    public string Name => "我的插件";
    public string Version => "1.0.0";
    public string Description => "这是一个示例插件";
    public string Author => "Your Name";

    private Action<bool>? _setToolbarVisibilityCallback;
    private Action<string, int>? _showNotificationCallback;

    public void Initialize()
    {
        Console.WriteLine($"{Name} 初始化完成");
    }

    public void OnLoad()
    {
        Console.WriteLine($"{Name} 已加载");
    }

    public void OnUnload()
    {
        Console.WriteLine($"{Name} 已卸载");
    }

    public void SetPluginOverlay(Border overlay)
    {
    }

    public void SetToolbarVisibilityCallback(Action<bool> callback)
    {
        _setToolbarVisibilityCallback = callback;
    }

    public void OnPluginActivated()
    {
        _setToolbarVisibilityCallback?.Invoke(false);
    }

    public void OnPluginDeactivated()
    {
        _setToolbarVisibilityCallback?.Invoke(true);
    }

    public void OnCameraFrame(Bitmap? bitmap)
    {
    }

    public void SetShowResultWindowCallback(Action<string, bool, List<(string Text, Action Callback)>>? callback)
    {
    }

    public void SetCancelCallback(Action callback)
    {
    }

    public void SetShowNotificationCallback(Action<string, int>? callback)
    {
        _showNotificationCallback = callback;
    }

    public List<PluginToolbarButton> GetToolbarButtons()
    {
        return new List<PluginToolbarButton>
        {
            new PluginToolbarButton
            {
                IconPath = "M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm-1 17.93c-3.95-.49-7-3.85-7-7.93 0-.62.08-1.21.21-1.79L9 15v1c0 1.1.9 2 2 2v1.93zm6.9-2.54c-.26-.81-1-1.39-1.9-1.39h-1v-3c0-.55-.45-1-1-1H8v-2h2c.55 0 1-.45 1-1V7h2c1.1 0 2-.9 2-2v-.41c2.93 1.19 5 4.06 5 7.41 0 2.08-.8 3.97-2.1 5.39z",
                Label = "我的功能",
                Order = 10,
                IsEnabled = true,
                OnClick = () => MyFunction()
            }
        };
    }

    public void UpdateToolbarButtons()
    {
    }

    private void MyFunction()
    {
        _showNotificationCallback?.Invoke("功能已执行", 2000);
    }
}
```

---

## 示例代码

### 示例 1：简单通知插件

```csharp
public class NotificationPlugin : IBottomToolbarPlugin
{
    public string Name => "通知插件";
    public string Version => "1.0.0";
    public string Description => "显示简单通知";
    public string Author => "ShowWrite Team";

    private Action? _refreshToolbarCallback;

    public void Initialize() { }
    public void OnLoad() { }
    public void OnUnload() { }

    public List<PluginToolbarButton> GetToolbarButtons()
    {
        return new List<PluginToolbarButton>
        {
            new PluginToolbarButton
            {
                IconPath = "M12 22c1.1 0 2-.9 2-2h-4c0 1.1.9 2 2 2zm6-6v-5c0-3.07-1.63-5.64-4.5-6.32V4c0-.83-.67-1.5-1.5-1.5s-1.5.67-1.5 1.5v.68C7.64 5.36 6 7.92 6 11v5l-2 2v1h16v-1l-2-2zm-2 1H8v-6c0-2.48 1.51-4.5 4-4.5s4 2.02 4 4.5v6z",
                Label = "通知",
                Order = 10,
                IsEnabled = true,
                OnClick = () => Console.WriteLine("通知按钮被点击")
            }
        };
    }

    public void UpdateToolbarButtons()
    {
        _refreshToolbarCallback?.Invoke();
    }

    public void SetRefreshToolbarCallback(Action callback)
    {
        _refreshToolbarCallback = callback;
    }
}
```

### 示例 2：相机帧处理插件

```csharp
public class CameraPlugin : IPluginWindow
{
    public string Name => "相机插件";
    public string Version => "1.0.0";
    public string Description => "处理相机帧";
    public string Author => "ShowWrite Team";

    private bool _isProcessing = false;
    private Action<bool>? _setToolbarVisibilityCallback;
    private Action<string, int>? _showNotificationCallback;

    public void Initialize() { }
    public void OnLoad() { }
    public void OnUnload() { }

    public void SetPluginOverlay(Border overlay) { }

    public void SetToolbarVisibilityCallback(Action<bool> callback)
    {
        _setToolbarVisibilityCallback = callback;
    }

    public void OnPluginActivated()
    {
        _isProcessing = true;
        _setToolbarVisibilityCallback?.Invoke(false);
    }

    public void OnPluginDeactivated()
    {
        _isProcessing = false;
        _setToolbarVisibilityCallback?.Invoke(true);
    }

    public void OnCameraFrame(Bitmap? bitmap)
    {
        if (!_isProcessing || bitmap == null) return;

        Console.WriteLine($"收到相机帧: {bitmap.PixelSize.Width}x{bitmap.PixelSize.Height}");
    }

    public void SetShowResultWindowCallback(Action<string, bool, List<(string Text, Action Callback)>>? callback) { }
    public void SetCancelCallback(Action callback) { }
    public void SetShowNotificationCallback(Action<string, int>? callback)
    {
        _showNotificationCallback = callback;
    }

    public List<PluginToolbarButton> GetToolbarButtons()
    {
        return new List<PluginToolbarButton>
        {
            new PluginToolbarButton
            {
                IconPath = "M9.4 16.6L4.8 12l4.6-4.6L8 6l-6 6 6 6 1.4-1.4zm5.2 0l4.6-4.6-4.6-4.6L16 6l6 6-6 6-1.4-1.4z",
                Label = "处理",
                Order = 10,
                IsEnabled = true,
                OnClick = () => OnPluginActivated()
            }
        };
    }

    public void UpdateToolbarButtons() { }
}
```

### 示例 3：显示结果窗口

```csharp
public void ShowResult(string result, bool isUrl)
{
    var buttons = new List<(string Text, Action Callback)>
    {
        ("复制", () => CopyToClipboard(result))
    };

    if (isUrl)
    {
        buttons.Add(("打开链接", () => OpenUrl(result)));
    }

    buttons.Add(("取消", () => { }));

    _showResultWindowCallback?.Invoke(result, isUrl, buttons);
}

private void CopyToClipboard(string text)
{
    var clipboard = Avalonia.Application.Current?.GetClipboard();
    clipboard?.SetTextAsync(text);
    _showNotificationCallback?.Invoke("已复制到剪贴板", 2000);
}

private void OpenUrl(string url)
{
    try
    {
        Process.Start(new ProcessStartInfo
        {
            FileName = url,
            UseShellExecute = true
        });
    }
    catch (Exception ex)
    {
        Console.WriteLine($"打开链接失败: {ex.Message}");
    }
}
```

---

## 常用 SVG 图标路径

### 功能图标
- 扫描: `M3 5v4h2V5h4V3H5c-1.1 0-2 .9-2 2zm2 10H3v4c0 1.1.9 2 2 2h4v-2H5v-4zm14 4h-4v2h4c1.1 0 2-.9 2-2v-4h-2v4zm0-16h-4v2h4v4h2V5c0-1.1-.9-2-2-2zm-6 8h-2v2h2v-2zm-4 0H7v2h2v-2zm8 0h-2v2h2v-2zm-4-4H9v2h2V9zm0 8H9v2h2v-2z`
- 关闭: `M19 6.41L17.59 5 12 10.59 6.41 5 5 6.41 10.59 12 5 17.59 6.41 19 12 13.41 17.59 19 19 17.59 13.41 12z`
- 通知: `M12 22c1.1 0 2-.9 2-2h-4c0 1.1.9 2 2 2zm6-6v-5c0-3.07-1.63-5.64-4.5-6.32V4c0-.83-.67-1.5-1.5-1.5s-1.5.67-1.5 1.5v.68C7.64 5.36 6 7.92 6 11v5l-2 2v1h16v-1l-2-2zm-2 1H8v-6c0-2.48 1.51-4.5 4-4.5s4 2.02 4 4.5v6z`
- 设置: `M19.14 12.94c.04-.3.06-.61.06-.94 0-.32-.02-.64-.07-.94l2.03-1.58c.18-.14.23-.41.12-.61l-1.92-3.32c-.12-.22-.37-.29-.59-.22l-2.39.96c-.5-.38-1.03-.7-1.62-.94l-.36-2.54c-.04-.24-.24-.41-.48-.41h-3.84c-.24 0-.43.17-.47.41l-.36 2.54c-.59.24-1.13.57-1.62.94l-2.39-.96c-.22-.08-.47 0-.59.22L2.74 8.87c-.12.21-.08.47.12.61l2.03 1.58c-.05.3-.09.63-.09.94s.02.64.07.94l-2.03 1.58c-.18.14-.23.41-.12.61l1.92 3.32c.12.22.37.29.59.22l2.39-.96c.5.38 1.03.7 1.62.94l.36 2.54c.05.24.24.41.48.41h3.84c.24 0 .44-.17.47-.41l.36-2.54c.59-.24 1.13-.56 1.62-.94l2.39.96c.22.08.47 0 .59-.22l1.92-3.32c.12-.22.07-.47-.12-.61l-2.01-1.58zM12 15.6c-1.98 0-3.6-1.62-3.6-3.6s1.62-3.6 3.6-3.6 3.6 1.62 3.6 3.6-1.62 3.6-3.6 3.6z`

---

## 注意事项

1. **线程安全**：所有 UI 操作必须在 UI 线程上执行，使用 `Avalonia.Threading.Dispatcher.UIThread.Post()`
2. **资源清理**：在 `OnUnload()` 方法中释放所有资源
3. **异常处理**：插件中的异常不会影响主程序，但建议添加适当的异常处理
4. **版本兼容性**：确保插件与主程序版本兼容
5. **性能优化**：避免在 `OnCameraFrame` 中执行耗时操作

---

## 更新日志

### v1.0.0 (2025-02-27)
- 初始版本
- 支持底部工具栏插件
- 支持窗口交互插件
- 支持相机帧处理
- 支持结果窗口显示
- 支持通知显示
