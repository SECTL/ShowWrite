using System;
using System.IO;

namespace ShowWrite
{
    public static class PluginDebugger
    {
        public static void PrintPluginStatus()
        {
            Console.WriteLine("========== 插件加载状态 ==========");
            Console.WriteLine();

            var pluginsPath = Config.GetPluginsPath();
            Console.WriteLine($"插件路径: {pluginsPath}");
            Console.WriteLine($"路径存在: {Directory.Exists(pluginsPath)}");
            Console.WriteLine();

            if (Directory.Exists(pluginsPath))
            {
                var dllFiles = Directory.GetFiles(pluginsPath, "*.dll", SearchOption.AllDirectories);
                Console.WriteLine($"找到的 DLL 文件数量: {dllFiles.Length}");

                foreach (var dllFile in dllFiles)
                {
                    Console.WriteLine($"  - {Path.GetFileName(dllFile)}");
                }
                Console.WriteLine();
            }

            var plugins = PluginManager.Instance.Plugins;
            Console.WriteLine($"已加载的插件数量: {plugins.Count}");
            Console.WriteLine();

            foreach (var plugin in plugins)
            {
                Console.WriteLine($"插件名称: {plugin.Name}");
                Console.WriteLine($"  版本: {plugin.Version}");
                Console.WriteLine($"  作者: {plugin.Author}");
                Console.WriteLine($"  描述: {plugin.Description}");
                Console.WriteLine($"  文件: {Path.GetFileName(plugin.FilePath)}");
                Console.WriteLine($"  已启用: {plugin.IsEnabled}");
                Console.WriteLine($"  已加载: {plugin.IsLoaded}");

                if (plugin.PluginInstance is IBottomToolbarPlugin bottomToolbarPlugin)
                {
                    try
                    {
                        var buttons = bottomToolbarPlugin.GetToolbarButtons();
                        Console.WriteLine($"  工具栏按钮数量: {buttons.Count}");
                        foreach (var btn in buttons)
                        {
                            Console.WriteLine($"    - {btn.Label} (Order: {btn.Order})");
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"  获取工具栏按钮失败: {ex.Message}");
                    }
                }
                else
                {
                    Console.WriteLine($"  不支持底部工具栏");
                }

                Console.WriteLine();
            }

            var toolbarButtons = PluginManager.Instance.GetBottomToolbarButtons();
            Console.WriteLine($"底部工具栏按钮总数: {toolbarButtons.Count}");
            foreach (var btn in toolbarButtons)
            {
                Console.WriteLine($"  - {btn.Label} (Order: {btn.Order}, Enabled: {btn.IsEnabled})");
            }

            Console.WriteLine();
            Console.WriteLine("====================================");
        }
    }
}
