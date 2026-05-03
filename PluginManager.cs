using Avalonia.Controls;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;

namespace ShowWrite
{
    public interface IPlugin
    {
        string Name { get; }
        string Version { get; }
        string Description { get; }
        string Author { get; }
        void Initialize();
        void OnLoad();
        void OnUnload();
    }

    public interface IBottomToolbarPlugin : IPlugin
    {
        List<PluginToolbarButton> GetToolbarButtons();
        void UpdateToolbarButtons();
        void SetRefreshToolbarCallback(Action callback);
    }

    public interface IPluginWindow : IPlugin
    {
        void SetPluginOverlay(Border overlay);
        void SetToolbarVisibilityCallback(Action<bool> callback);
        void OnPluginActivated();
        void OnPluginDeactivated();
        void OnCameraFrame(Avalonia.Media.Imaging.Bitmap? bitmap);
        void SetShowResultWindowCallback(Action<string, bool, List<(string Text, Action Callback)>>? callback);
        void SetCancelCallback(Action callback);
        void SetShowNotificationCallback(Action<string, int>? callback);
    }

    public class PluginToolbarButton
    {
        public string IconPath { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public Action? OnClick { get; set; }
        public int Order { get; set; } = 100;
        public bool IsEnabled { get; set; } = true;
    }

    public class PluginInfo
    {
        public string Name { get; set; } = string.Empty;
        public string Version { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Author { get; set; } = string.Empty;
        public string FilePath { get; set; } = string.Empty;
        public bool IsLoaded { get; set; }
        public bool IsEnabled { get; set; }
        public IPlugin? PluginInstance { get; set; }
        public Assembly? Assembly { get; set; }
    }

    public class PluginManager
    {
        private static PluginManager? _instance;
        public static PluginManager Instance => _instance ??= new PluginManager();

        private readonly List<PluginInfo> _plugins = new();
        private readonly string _pluginsPath;

        public IReadOnlyList<PluginInfo> Plugins => _plugins.AsReadOnly();

        private PluginManager()
        {
            _pluginsPath = Config.GetPluginsPath();
        }

        public void LoadPlugins()
        {
            if (!Directory.Exists(_pluginsPath))
            {
                Directory.CreateDirectory(_pluginsPath);
                return;
            }

            var config = Config.Load();
            var enabledPlugins = config.EnabledPlugins;

            var dllFiles = Directory.GetFiles(_pluginsPath, "*.dll", SearchOption.AllDirectories);

            Console.WriteLine($"扫描插件路径: {_pluginsPath}");
            Console.WriteLine($"找到 {dllFiles.Length} 个 DLL 文件");

            foreach (var dllFile in dllFiles)
            {
                try
                {
                    Console.WriteLine($"正在加载插件文件: {Path.GetFileName(dllFile)}");

                    var pluginContext = new PluginLoadContext(dllFile);
                    var assembly = pluginContext.LoadFromAssemblyPath(dllFile);

                    var pluginTypes = assembly.GetTypes()
                        .Where(t => typeof(IPlugin).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
                        .ToList();

                    Console.WriteLine($"  找到 {pluginTypes.Count} 个插件类型");

                    foreach (var pluginType in pluginTypes)
                    {
                        IPlugin? pluginInstance = null;
                        try
                        {
                            pluginInstance = (IPlugin?)Activator.CreateInstance(pluginType);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"创建插件实例失败: {ex.Message}");
                            Console.WriteLine($"  堆栈跟踪: {ex.StackTrace}");
                            if (ex.InnerException != null)
                            {
                                Console.WriteLine($"  内部异常: {ex.InnerException.Message}");
                                Console.WriteLine($"  内部异常堆栈: {ex.InnerException.StackTrace}");
                            }
                            continue;
                        }

                        if (pluginInstance == null) continue;

                        var pluginName = pluginInstance.Name;
                        var isEnabled = enabledPlugins.Contains(pluginName);

                        Console.WriteLine($"  插件名称: {pluginName}, 已启用: {isEnabled}");

                        var pluginInfo = new PluginInfo
                        {
                            Name = pluginName,
                            Version = pluginInstance.Version,
                            Description = pluginInstance.Description,
                            Author = pluginInstance.Author,
                            FilePath = dllFile,
                            IsEnabled = isEnabled,
                            PluginInstance = pluginInstance,
                            Assembly = assembly
                        };

                        if (isEnabled)
                        {
                            try
                            {
                                pluginInstance.Initialize();
                                pluginInstance.OnLoad();
                                pluginInfo.IsLoaded = true;
                                Console.WriteLine($"  插件 {pluginName} 加载成功");
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"插件 {pluginName} 加载失败: {ex.Message}");
                                Console.WriteLine($"  堆栈跟踪: {ex.StackTrace}");
                            }
                        }
                        else
                        {
                            Console.WriteLine($"  插件 {pluginName} 未启用，正在自动启用...");

                            try
                            {
                                pluginInstance.Initialize();
                                pluginInstance.OnLoad();
                                pluginInfo.IsEnabled = true;
                                pluginInfo.IsLoaded = true;

                                enabledPlugins.Add(pluginName);
                                config.Save();

                                Console.WriteLine($"  插件 {pluginName} 已自动启用并加载成功");
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"插件 {pluginName} 自动启用失败: {ex.Message}");
                                Console.WriteLine($"  堆栈跟踪: {ex.StackTrace}");
                            }
                        }

                        _plugins.Add(pluginInfo);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"加载插件文件 {dllFile} 失败: {ex.Message}");
                    Console.WriteLine($"  堆栈跟踪: {ex.StackTrace}");
                }
            }
        }

        public void EnablePlugin(string pluginName)
        {
            var plugin = _plugins.FirstOrDefault(p => p.Name == pluginName);
            if (plugin == null || plugin.IsEnabled) return;

            try
            {
                plugin.PluginInstance?.Initialize();
                plugin.PluginInstance?.OnLoad();
                plugin.IsEnabled = true;
                plugin.IsLoaded = true;

                var config = Config.Load();
                if (!config.EnabledPlugins.Contains(pluginName))
                {
                    config.EnabledPlugins.Add(pluginName);
                    config.Save();
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"启用插件 {pluginName} 失败: {ex.Message}");
            }
        }

        public void DisablePlugin(string pluginName)
        {
            var plugin = _plugins.FirstOrDefault(p => p.Name == pluginName);
            if (plugin == null || !plugin.IsEnabled) return;

            try
            {
                plugin.PluginInstance?.OnUnload();
                plugin.IsEnabled = false;
                plugin.IsLoaded = false;

                var config = Config.Load();
                config.EnabledPlugins.Remove(pluginName);
                config.Save();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"禁用插件 {pluginName} 失败: {ex.Message}");
            }
        }

        public void ReloadPlugin(string pluginName)
        {
            var plugin = _plugins.FirstOrDefault(p => p.Name == pluginName);
            if (plugin == null) return;

            if (plugin.IsEnabled)
            {
                DisablePlugin(pluginName);
            }

            if (plugin.Assembly != null)
            {
                try
                {
                    var pluginContext = new PluginLoadContext(plugin.FilePath);
                    var assembly = pluginContext.LoadFromAssemblyPath(plugin.FilePath);

                    var pluginTypes = assembly.GetTypes()
                        .Where(t => typeof(IPlugin).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
                        .ToList();

                    if (pluginTypes.Count > 0)
                    {
                        var pluginInstance = (IPlugin?)Activator.CreateInstance(pluginTypes[0]);
                        if (pluginInstance != null)
                        {
                            plugin.PluginInstance = pluginInstance;
                            plugin.Assembly = assembly;
                            plugin.Name = pluginInstance.Name;
                            plugin.Version = pluginInstance.Version;
                            plugin.Description = pluginInstance.Description;
                            plugin.Author = pluginInstance.Author;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"重新加载插件 {pluginName} 失败: {ex.Message}");
                }
            }
        }

        public PluginInfo? GetPlugin(string pluginName)
        {
            return _plugins.FirstOrDefault(p => p.Name == pluginName);
        }

        public List<PluginToolbarButton> GetBottomToolbarButtons()
        {
            var buttons = new List<PluginToolbarButton>();

            foreach (var plugin in _plugins.Where(p => p.IsEnabled && p.IsLoaded))
            {
                if (plugin.PluginInstance is IBottomToolbarPlugin bottomToolbarPlugin)
                {
                    try
                    {
                        var pluginButtons = bottomToolbarPlugin.GetToolbarButtons();
                        buttons.AddRange(pluginButtons);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"获取插件 {plugin.Name} 的工具栏按钮失败: {ex.Message}");
                    }
                }
            }

            return buttons.OrderBy(b => b.Order).ToList();
        }

        public void RefreshToolbarButtons()
        {
            foreach (var plugin in _plugins.Where(p => p.IsEnabled && p.IsLoaded))
            {
                if (plugin.PluginInstance is IBottomToolbarPlugin bottomToolbarPlugin)
                {
                    try
                    {
                        bottomToolbarPlugin.UpdateToolbarButtons();
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"刷新插件 {plugin.Name} 的工具栏按钮失败: {ex.Message}");
                    }
                }
            }
        }
    }

    public class PluginLoadContext : AssemblyLoadContext
    {
        private readonly AssemblyDependencyResolver _resolver;

        public PluginLoadContext(string pluginPath) : base(isCollectible: true)
        {
            _resolver = new AssemblyDependencyResolver(pluginPath);
        }

        protected override Assembly? Load(AssemblyName assemblyName)
        {
            var assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
            if (assemblyPath != null)
            {
                return LoadFromAssemblyPath(assemblyPath);
            }

            return null;
        }
    }
}
