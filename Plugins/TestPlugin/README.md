# ShowWrite 测试插件

这是一个演示 ShowWrite 插件系统的测试插件。

## 功能

该插件在底部菜单栏添加了两个按钮：

1. **测试按钮** - 显示一个勾选图标，点击时会在控制台输出点击次数
2. **拍照按钮** - 显示一个相机图标，点击时会在控制台输出拍照信息

## 安装方法

### 方法一：使用安装脚本（推荐）

1. 确保已编译 TestPlugin 项目
2. 运行 `install.bat` 脚本
3. 重启 ShowWrite 应用程序

### 方法二：手动安装

1. 编译 TestPlugin 项目：`dotnet build Plugins/TestPlugin/TestPlugin.csproj`
2. 将生成的 `TestPlugin.dll` 复制到 `%AppData%\ShowWrite\PKG` 文件夹
3. 重启 ShowWrite 应用程序

## 开发自己的插件

### 1. 创建插件类

创建一个实现 `IBottomToolbarPlugin` 接口的类：

```csharp
using System;
using System.Collections.Generic;
using ShowWrite;

namespace MyPlugin
{
    public class MyCustomPlugin : IBottomToolbarPlugin
    {
        public string Name => "我的插件";
        public string Version => "1.0.0";
        public string Description => "插件描述";
        public string Author => "作者名";

        public void Initialize()
        {
            // 插件初始化代码
        }

        public void OnLoad()
        {
            // 插件加载时调用
        }

        public void OnUnload()
        {
            // 插件卸载时调用
        }

        public List<PluginToolbarButton> GetToolbarButtons()
        {
            return new List<PluginToolbarButton>
            {
                new PluginToolbarButton
                {
                    IconPath = "M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm-2 15l-5-5 1.41-1.41L10 14.17l7.59-7.59L19 8l-9 9z",
                    Label = "按钮标签",
                    Order = 10,
                    IsEnabled = true,
                    OnClick = () => 
                    {
                        // 按钮点击事件处理
                        Console.WriteLine("按钮被点击了");
                    }
                }
            };
        }
    }
}
```

### 2. 创建项目文件

创建 `.csproj` 文件：

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net8.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
    <RootNamespace>MyPlugin</RootNamespace>
    <AssemblyName>MyPlugin</AssemblyName>
    <CopyLocalLockFileAssemblies>false</CopyLocalLockFileAssemblies>
  </PropertyGroup>

  <ItemGroup>
    <Reference Include="ShowWrite">
      <HintPath>..\..\bin\Debug\net8.0\ShowWrite.dll</HintPath>
      <Private>false</Private>
    </Reference>
  </ItemGroup>
</Project>
```

### 3. 编译并安装

1. 编译插件项目
2. 将生成的 DLL 复制到 `%AppData%\ShowWrite\PKG` 文件夹
3. 重启 ShowWrite

## 图标路径格式

`IconPath` 属性使用 SVG Path 数据格式。可以从以下资源获取图标路径：

- Material Design Icons
- FontAwesome
- 其他 SVG 图标库

示例图标路径：
- 勾选：`M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm-2 15l-5-5 1.41-1.41L10 14.17l7.59-7.59L19 8l-9 9z`
- 相机：`M19 6.5h-1.28l-.32-1a3 3 0 0 0-2.84-2H9.44A3 3 0 0 0 6.6 5.55l-.32 1H5a3 3 0 0 0-3 3v8a3 3 0 0 0 3 3h14a3 3 0 0 0 3-3v-8a3 3 0 0 0-3-3.05m1 11a1 1 0 0 1-1 1H5a1 1 0 0 1-1-1v-8a1 1 0 0 1 1-1h2a1 1 0 0 0 1-.68l.54-1.64a1 1 0 0 1 .95-.68h5.12a1 1 0 0 1 .95.68l.54 1.64a1 1 0 0 0 1 .68h2a1 1 0 0 1 1 1v8m-7-8c-2.21 0-4 1.79-4 4s1.79 4 4 4 4-1.79 4-4-1.79-4-4-4m0 6c-1.1 0-2-.9-2-2s.9-2 2-2 2 .9 2 2-.9 2-2 2`

## PluginToolbarButton 属性说明

| 属性 | 类型 | 说明 |
|------|------|------|
| IconPath | string | SVG Path 数据字符串 |
| Label | string | 按钮下方显示的文本 |
| OnClick | Action? | 按钮点击事件处理函数 |
| Order | int | 按钮排序，数值越小越靠前 |
| IsEnabled | bool | 按钮是否启用 |

## 注意事项

1. 插件 DLL 必须引用 ShowWrite 主程序
2. 插件类必须实现 `IBottomToolbarPlugin` 接口
3. 插件会被自动加载到 `%AppData%\ShowWrite\PKG` 文件夹
4. 按钮按 Order 属性排序显示
5. 确保插件代码的异常处理，避免影响主程序运行
