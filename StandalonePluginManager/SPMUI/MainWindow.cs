using ECommons.Automation.NeoTaskManager;
using ECommons.Reflection;
using ECommons.SimpleGui;
using StandalonePluginManager.SPMData;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StandalonePluginManager.UI;
public sealed class MainWindow : ConfigWindow
{
    public MainWindow()
    {
        EzConfigGui.Init(this);
    }

    public string PluginURL = "";
    public string Error = "";
    public string Status = "";
    public PluginDescriptor PluginDescriptor = null;

    public override void Draw()
    {
        if(S.PluginDownloader.IsBusy)
        {
            ImGuiEx.Text($"Downloading file, please wait...");
            if(ImGuiEx.IconButtonWithText(FontAwesomeIcon.Ban, "Cancel"))
            {
                S.PluginDownloader.Cancel();
            }
            return;
        }
        if(S.PluginInstaller.IsBusy)
        {
            ImGuiEx.Text($"Installing plugin, please wait...");
            return;
        }
        if(Error != "")
        {
            ImGuiEx.TextWrapped(EColor.RedBright, $"Error downloading plugin:\n{Error}");
            if(ImGuiEx.IconButtonWithText(FontAwesomeIcon.Ban, "Dismiss"))
            {
                Error = "";
            }
            return;
        }
        if(Status != "")
        {
            ImGuiEx.TextWrapped(EColor.GreenBright, Status);
            if(ImGuiEx.IconButtonWithText(FontAwesomeIcon.Check, "Continue"))
            {
                Status = "";
            }
            return;
        }
        ImGuiEx.TextWrapped($"This plugin will help you to install or update a plugin from zip archive.");
        ImGuiEx.TextWrapped($"1. Enter archive URL and press \"Download\" button, or press \"From URL in Clipboard\" button:");
        ImGuiEx.SetNextItemFullWidth();
        var pressed = ImGui.InputText("##url", ref PluginURL, 2000, ImGuiInputTextFlags.EnterReturnsTrue);
        ImGuiEx.LineCentered(() =>
        {
            if(ImGuiEx.IconButtonWithText(FontAwesomeIcon.Download, "Download"))
            {
                DownloadPlugin(PluginURL);
            }
            ImGui.SameLine();
            if(ImGuiEx.IconButtonWithText(FontAwesomeIcon.Paste, "Download from URL in Clipboard"))
            {
                DownloadPlugin(Paste());
            }
        });
        ImGui.Separator();
        if(PluginDescriptor != null)
        {
            var d = PluginDescriptor;
            ImGuiEx.LineCentered("DP", () => ImGuiEx.Text(EColor.GreenBright, $"Downloaded plugin: {d.Manifest?.Name ?? d.MainFileName}"));
            var error = false;
            if(d.Manifest == null)
            {
                error = true;
                ImGuiEx.Text(EColor.RedBright, UiBuilder.IconFont, FontAwesomeIcon.Times.ToIconString());
                ImGui.SameLine();
                ImGuiEx.TextWrapped($"Manifest is missing. You can still try to load the plugin, but it may malfunction.");
            }
            else
            {
                if(d.Manifest.DalamudApiLevel < Svc.PluginInterface.Manifest.DalamudApiLevel)
                {
                    error = true;
                    ImGuiEx.Text(EColor.RedBright, UiBuilder.IconFont, FontAwesomeIcon.Times.ToIconString());
                    ImGui.SameLine();
                    ImGuiEx.TextWrapped($"API level is less than required. Plugin will likely fail to load.");
                }
                if(d.Manifest.DalamudApiLevel > Svc.PluginInterface.Manifest.DalamudApiLevel)
                {
                    error = true;
                    ImGuiEx.Text(EColor.RedBright, UiBuilder.IconFont, FontAwesomeIcon.Times.ToIconString());
                    ImGui.SameLine();
                    ImGuiEx.TextWrapped($"API level is more than required. Usually this indicates that you are using CN/KR client and attempting to download plugin for global FFXIV version. Plugin will likely fail to load.");
                }
                if(!d.Manifest.InternalName.EqualsIgnoreCase(Path.GetFileNameWithoutExtension(d.MainFileName)))
                {
                    error = true;
                    ImGuiEx.Text(EColor.RedBright, UiBuilder.IconFont, FontAwesomeIcon.Times.ToIconString());
                    ImGui.SameLine();
                    ImGuiEx.TextWrapped($"Internal name differs from DLL name. Plugin will likely fail to load.");
                }
            }
            if(!error)
            {
                ImGuiEx.Text(EColor.GreenBright, UiBuilder.IconFont, FontAwesomeIcon.Check.ToIconString());
                ImGui.SameLine();
                ImGuiEx.TextWrapped($"Plugin {d.Manifest.Name} v{d.Manifest.AssemblyVersion} has passed all checks and is ready for installation.");
            }

            var internalName = d.Manifest?.InternalName ?? Path.GetFileNameWithoutExtension(d.MainFileName);
            var path = Utils.GetDevPluginPathName(internalName);
            var installationNewPath = Path.Combine(Svc.PluginInterface.ConfigDirectory.Parent.Parent.FullName, "customPlugins", internalName);
            if(path != "")
            {
                ImGuiEx.Text(EColor.GreenBright, UiBuilder.IconFont, "\uf021");
                ImGui.SameLine();
                ImGuiEx.TextWrapped($"Plugin {d.Manifest.Name} already installed into {path} folder. It will be updated.");
            }
            else
            {
                ImGuiEx.Text(EColor.GreenBright, UiBuilder.IconFont, FontAwesomeIcon.Download.ToIconString());
                ImGui.SameLine();
                ImGuiEx.TextWrapped($"Plugin {d.Manifest.Name} is not installed. It will be installed into {installationNewPath}.");
            }

                ImGuiEx.LineCentered("Install", () =>
                {
                    if(ImGuiEx.IconButtonWithText(FontAwesomeIcon.ArrowsDownToLine, $"{(path == ""?"Install":"Update")} {d.Manifest?.Name ?? internalName}"))
                    {
                        if(path == "")
                        {
                            Task.Run(() => S.PluginInstaller.InstallPlugin(d, installationNewPath, x => Error = x))
                            .ContinueWith(x =>
                            {
                                new TickScheduler(() =>
                                {
                                    Utils.AddDevPlguinLocation(Path.Combine(installationNewPath, d.MainFileName), true);
                                    if(Error == "")
                                    {
                                        Status = "Plugin successfully installed. SPM will now attempt to enable it. If it fails, please open plugin installer and enable it manually. ";
                                        var tm = new TaskManager();
                                        tm.Enqueue(() => Svc.Commands.ProcessCommand("/xlplugins"));
                                        tm.Enqueue(() => Svc.Commands.ProcessCommand("/xlplugins"));
                                        tm.Enqueue(() => Svc.Commands.ProcessCommand($"/xlenableplugin {internalName}"));
                                        tm.Enqueue(() => tm.Dispose());
                                    }
                                });
                            });
                        }
                        else
                        {
                            Task.Run(() => S.PluginInstaller.InstallPlugin(d, path, x => Error = x))
                            .ContinueWith(x =>
                            {
                                new TickScheduler(() =>
                                {
                                    if(Error == "")
                                    {
                                        Status = "Plugin successfully updated. SPM will now attempt to enable it. If it fails, please open plugin installer and enable it manually. ";
                                        var tm = new TaskManager();
                                        tm.Enqueue(() => Svc.Commands.ProcessCommand("/xlplugins"));
                                        tm.Enqueue(() => Svc.Commands.ProcessCommand("/xlplugins"));
                                        tm.Enqueue(() => Svc.Commands.ProcessCommand($"/xlenableplugin {internalName}"));
                                        tm.Enqueue(() => tm.Dispose());
                                    }
                                });
                            });
                        }
                    }
                    ImGui.SameLine();
                    if(ImGuiEx.IconButtonWithText(FontAwesomeIcon.Ban, $"Cancel"))
                    {
                        new TickScheduler(() => PluginDescriptor = null);
                    }
                });

            if(ImGuiEx.BeginDefaultTable(["~File Name", "Size", "Version", "Platform"]))
            {
                foreach(var f in (FileInfoData[])[d.MainFile, ..d.Files])
                {
                    ImGui.TableNextRow();
                    ImGui.TableNextColumn();
                    ImGuiEx.Text(f.FileName);

                    ImGui.TableNextColumn();
                    ImGuiEx.Text(Utils.ToReadableFileSize(f.FileSize));

                    ImGui.TableNextColumn();
                    if(f.Version != null)
                    {
                        ImGuiEx.Text(f.Version);
                    }

                    ImGui.TableNextColumn();
                    if(f.Platform != null)
                    {
                        ImGuiEx.Text(f.Platform);
                    }
                }

                ImGui.EndTable();
            }
        }

        async void DownloadPlugin(string url)
        {
            var plugin = await S.PluginDownloader.DownloadAndAnalyzeAsync(url, x => Error = x)
                .ContinueWith(x => PluginDescriptor = x.Result);
        }
    }
}