using System;
using System.IO;

namespace ShowWrite
{
    public static class PluginDebugger
    {
        public static void PrintPluginStatus()
        {



            var pluginsPath = Config.GetPluginsPath();




            if (Directory.Exists(pluginsPath))
            {
                var dllFiles = Directory.GetFiles(pluginsPath, "*.dll", SearchOption.AllDirectories);


                foreach (var dllFile in dllFiles)
                {

                }

            }

            var plugins = PluginManager.Instance.Plugins;



            foreach (var plugin in plugins)
            {








                if (plugin.PluginInstance is IBottomToolbarPlugin bottomToolbarPlugin)
                {
                    try
                    {
                        var buttons = bottomToolbarPlugin.GetToolbarButtons();

                        foreach (var btn in buttons)
                        {

                        }
                    }
                    catch (Exception ex)
                    {

                    }
                }
                else
                {

                }


            }

            var toolbarButtons = PluginManager.Instance.GetBottomToolbarButtons();

            foreach (var btn in toolbarButtons)
            {

            }



        }
    }
}
