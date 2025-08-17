using Dalamud.Plugin.Internal.Types.Manifest;
using ECommons.Networking;
using StandalonePluginManager.SPMData;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace StandalonePluginManager.SPMServices;

public class PluginDownloader : IDisposable
{
    private const int MaxSize = 100 * 1024 * 1024;
    private HttpClient client
    {
        get
        {
            field ??= new HttpClient()
                {
                    Timeout = TimeSpan.FromSeconds(60)
                }.ApplyProxySettings(C.ProxySettings);
            return field;
        }
    }
    private CancellationTokenSource cts;

    public volatile bool IsBusy;

    private PluginDownloader()
    {
    }

    public void Dispose()
    {
        Cancel();
        client?.Dispose();
    }

    public void Cancel()
    {
        lock(this)
        {
            if(cts != null)
            {
                try
                {
                    if(!cts.IsCancellationRequested)
                    {
                        cts.Cancel();
                    }
                }
                catch
                {
                }
            }
        }
    }

    public async Task<PluginDescriptor> DownloadAndAnalyzeAsync(string url, Action<string> errorCallback)
    {
        if(IsBusy)
        {
            errorCallback?.Invoke("Already busy with another operation.");
            return null;
        }

        IsBusy = true;
        cts = new CancellationTokenSource();
        try
        {
            using var response = await client.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cts.Token);
            response.EnsureSuccessStatusCode();

            await using var ms = new MemoryStream();
            await using(var stream = await response.Content.ReadAsStreamAsync(cts.Token))
            {
                var buffer = new byte[81920];
                int read;
                while((read = await stream.ReadAsync(buffer.AsMemory(0, buffer.Length), cts.Token)) > 0)
                {
                    if(ms.Length + read > MaxSize)
                    {
                        errorCallback?.Invoke("File too large.");
                        return null;
                    }
                    ms.Write(buffer, 0, read);
                }
            }

            var data = ms.ToArray();
            using var zip = new ZipArchive(new MemoryStream(data, writable: false), ZipArchiveMode.Read, leaveOpen: false);

            var descriptor = new PluginDescriptor
            {
                ArchiveData = data,
                Files = new List<FileInfoData>()
            };

            ZipArchiveEntry mainDllEntry = null;

            foreach(var entry in zip.Entries)
            {
                if(entry.FullName.Contains("/") || entry.FullName.Contains("\\"))
                {
                    continue;
                }
                if(entry.FullName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                {
                    if(IsDalamudPlugin(entry))
                    {
                        if(mainDllEntry != null)
                        {
                            errorCallback?.Invoke("Multiple plugin DLL candidates found.");
                            return null;
                        }
                        mainDllEntry = entry;
                    }
                }
            }

            if(mainDllEntry == null)
            {
                errorCallback?.Invoke("No valid Dalamud plugin found.");
                return null;
            }

            descriptor.MainFileName = mainDllEntry.FullName;
            descriptor.MainFile = GetFileInfo(mainDllEntry);

            foreach(var entry in zip.Entries)
            {
                if(entry == mainDllEntry)
                {
                    continue;
                }
                if(string.IsNullOrEmpty(entry.Name))
                {
                    continue;
                }
                descriptor.Files.Add(GetFileInfo(entry));
            }

            var manifestEntry = FindManifestEntry(zip, mainDllEntry.Name);
            if(manifestEntry != null)
            {
                try
                {
                    using var mstream = OpenSeekable(manifestEntry);
                    descriptor.Manifest = JsonSerializer.Deserialize<SPMPluginManifest>(mstream);
                }
                catch(Exception e)
                {
                    PluginLog.Error(e.ToStringFull());
                    descriptor.Manifest = null;
                }
            }
            else
            {
                descriptor.Manifest = null;
            }

            return descriptor;
        }
        catch(OperationCanceledException)
        {
            errorCallback?.Invoke("Operation was canceled.");
            return null;
        }
        catch(InvalidDataException)
        {
            errorCallback?.Invoke("Not a valid zip archive.");
            return null;
        }
        catch(Exception ex)
        {
            errorCallback?.Invoke("Unexpected error: " + ex.Message);
            return null;
        }
        finally
        {
            IsBusy = false;
            lock(this)
            {
                cts?.Dispose();
                cts = null;
            }
        }
    }

    private static ZipArchiveEntry FindManifestEntry(ZipArchive zip, string mainDllName)
    {
        var expectedName = Path.GetFileNameWithoutExtension(mainDllName) + ".json";

        foreach(var e in zip.Entries)
        {
            if(e.FullName.Contains("/") || e.FullName.Contains("\\"))
            {
                continue;
            }

            if(string.Equals(e.Name, expectedName, StringComparison.OrdinalIgnoreCase))
            {
                return e;
            }
        }

        return null;
    }


    private static MemoryStream OpenSeekable(ZipArchiveEntry entry)
    {
        var capacity = entry.Length > 0 && entry.Length <= int.MaxValue ? (int)entry.Length : 0;
        var ms = capacity > 0 ? new MemoryStream(capacity) : new MemoryStream();
        using(var s = entry.Open())
        {
            var buffer = new byte[81920];
            int read;
            while((read = s.Read(buffer, 0, buffer.Length)) > 0)
            {
                ms.Write(buffer, 0, read);
            }
        }
        ms.Position = 0;
        return ms;
    }

    private static FileInfoData GetFileInfo(ZipArchiveEntry entry)
    {
        var info = new FileInfoData
        {
            FileName = entry.FullName,
            FileSize = entry.Length,
            Version = null,
            Platform = null
        };

        if(entry.FullName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase) ||
            entry.FullName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
        {
            using var seekable = OpenSeekable(entry);
            using var peReader = new PEReader(seekable, PEStreamOptions.LeaveOpen);

            if(peReader.HasMetadata)
            {
                info.Platform = "Managed .NET";
                try
                {
                    var mdReader = peReader.GetMetadataReader();
                    var asmDef = mdReader.GetAssemblyDefinition();
                    info.Version = asmDef.Version.ToString();

                    foreach(var caHandle in asmDef.GetCustomAttributes())
                    {
                        var ca = mdReader.GetCustomAttribute(caHandle);
                        if(ca.Constructor.Kind == HandleKind.MemberReference)
                        {
                            var mr = mdReader.GetMemberReference((MemberReferenceHandle)ca.Constructor);
                            if(mr.Parent.Kind == HandleKind.TypeReference)
                            {
                                var tr = mdReader.GetTypeReference((TypeReferenceHandle)mr.Parent);
                                var name = mdReader.GetString(tr.Name);
                                var ns = mdReader.GetString(tr.Namespace);
                                if(name == "TargetFrameworkAttribute" && ns == "System.Runtime.Versioning")
                                {
                                    var blobReader = mdReader.GetBlobReader(ca.Value);
                                    if(blobReader.ReadUInt16() == 0x0001)
                                    {
                                        var framework = blobReader.ReadSerializedString();
                                        if(!string.IsNullOrEmpty(framework))
                                        {
                                            info.Platform = $"Managed .NET ({framework})";
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
                catch
                {
                }
            }
            else
            {
                info.Platform = "Native/Unmanaged";
            }
        }

        return info;
    }

    private static bool IsDalamudPlugin(ZipArchiveEntry entry)
    {
        using var seekable = OpenSeekable(entry);
        using var peReader = new PEReader(seekable, PEStreamOptions.LeaveOpen);
        if(!peReader.HasMetadata)
        {
            return false;
        }

        var mdReader = peReader.GetMetadataReader();
        foreach(var typeHandle in mdReader.TypeDefinitions)
        {
            var typeDef = mdReader.GetTypeDefinition(typeHandle);
            foreach(var ifaceHandle in typeDef.GetInterfaceImplementations())
            {
                var iface = mdReader.GetInterfaceImplementation(ifaceHandle);
                if(iface.Interface.Kind == HandleKind.TypeReference)
                {
                    var tr = mdReader.GetTypeReference((TypeReferenceHandle)iface.Interface);
                    var ifaceName = mdReader.GetString(tr.Name);
                    var ifaceNamespace = mdReader.GetString(tr.Namespace);
                    if(ifaceName == "IDalamudPlugin" && ifaceNamespace == "Dalamud.Plugin")
                    {
                        return true;
                    }
                }
            }
        }
        return false;
    }
}
