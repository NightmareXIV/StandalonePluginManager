using ECommons.Reflection;
using StandalonePluginManager.SPMData;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Runtime.Loader;
using System.Text;
using System.Threading.Tasks;
using TerraFX.Interop.Windows;

namespace StandalonePluginManager;
public unsafe static class Utils
{
    public static string ToReadableFileSize(long size)
    {
        string[] suffixes = { "B", "KB", "MB", "GB", "TB" };
        double dSize = size;
        int order = 0;

        while(dSize >= 1024 && order < suffixes.Length - 1)
        {
            order++;
            dSize /= 1024;
        }

        return $"{dSize:0.##} {suffixes[order]}";
    }

    public static string GetDevPluginPathName(string internalName)
    {
        var pluginManager = DalamudReflector.GetPluginManager();
        var installedPlugins = (System.Collections.IList)pluginManager.GetType().GetProperty("InstalledPlugins").GetValue(pluginManager);

        foreach(var t in installedPlugins)
        {
            if((string)t.GetType().GetProperty("InternalName").GetValue(t) == internalName)
            {
                if(t.GetType().Name != "LocalDevPlugin") continue;
                var type = t.GetType().Name == "LocalDevPlugin" ? t.GetType().BaseType : t.GetType();

                var fileInfo = type.GetProperty("DllFile").GetValue(t) as FileInfo;
                if(fileInfo != null)
                {
                    return fileInfo.DirectoryName;
                }
            }
        }
        return "";
    }

    public static void AddDevPlguinLocation(string dllFile, bool enabled)
    {
        var conf = DalamudReflector.GetService("Dalamud.Configuration.Internal.DalamudConfiguration");
        var repolist = (System.Collections.IEnumerable)conf.GetFoP("DevPluginLoadLocations");
        if(repolist != null)
            foreach(var r in repolist)
                if((string)r.GetFoP("Path") == dllFile)
                    return;
        var instance = Activator.CreateInstance(Svc.PluginInterface.GetType().Assembly.GetType("Dalamud.Configuration.DevPluginLocationSettings")!);
        instance.SetFoP("Path", dllFile);
        instance.SetFoP("IsEnabled", enabled);
        conf.GetFoP<System.Collections.IList>("DevPluginLoadLocations").Add(instance);
        var pluginConfig = Activator.CreateInstance(Svc.PluginInterface.GetType().Assembly.GetType("Dalamud.Configuration.Internal.DevPluginSettings"));
        pluginConfig.SetFoP("AutomaticReloading", true);
        pluginConfig.SetFoP("NotifyForErrors", false);
        conf.GetFoP<System.Collections.IDictionary>("DevPluginSettings")[dllFile] = pluginConfig;
        DalamudReflector.SaveDalamudConfig();
    }
}