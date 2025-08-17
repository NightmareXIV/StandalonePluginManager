namespace StandalonePluginManager.SPMServices;

using global::StandalonePluginManager.SPMData;
using System;
using System.IO;
using System.IO.Compression;
using System.Linq;

public class PluginInstaller
{
    public volatile bool IsBusy;

    public void InstallPlugin(PluginDescriptor descriptor, string targetDirectory, Action<string> errorCallback)
    {
        if(IsBusy)
        {
            errorCallback?.Invoke("Operation already in progress.");
            return;
        }

        if(descriptor == null || descriptor.ArchiveData == null)
        {
            errorCallback?.Invoke("Invalid plugin descriptor or missing archive data.");
            return;
        }

        if(string.IsNullOrWhiteSpace(targetDirectory))
        {
            errorCallback?.Invoke("Target directory is invalid.");
            return;
        }

        IsBusy = true;
        try
        {
            Directory.CreateDirectory(targetDirectory);

            using var zip = new ZipArchive(new MemoryStream(descriptor.ArchiveData, writable: false), ZipArchiveMode.Read);

            var zipRootDlls = zip.Entries
                .Where(e => !string.IsNullOrEmpty(e.Name) &&
                            !e.FullName.Contains("/") &&
                            e.FullName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                .Select(e => e.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            var existingDlls = Directory.GetFiles(targetDirectory, "*.dll", SearchOption.TopDirectoryOnly);
            foreach(var dllPath in existingDlls)
            {
                var fileName = Path.GetFileName(dllPath);
                if(!zipRootDlls.Contains(fileName))
                {
                    try
                    {
                        DeleteFileToRecycleBin(dllPath); 
                    }
                    catch(Exception ex)
                    {
                        errorCallback?.Invoke($"Failed to delete {fileName}: {ex.Message}");
                    }
                }
            }

            foreach(var entry in zip.Entries)
            {
                if(string.IsNullOrEmpty(entry.Name) || entry.FullName.Equals(descriptor.MainFileName, StringComparison.OrdinalIgnoreCase))
                {
                    continue; 
                }

                ExtractEntry(entry, targetDirectory, errorCallback);
            }

            var mainEntry = zip.GetEntry(descriptor.MainFileName);
            if(mainEntry != null)
            {
                ExtractEntry(mainEntry, targetDirectory, errorCallback);
            }
            else
            {
                errorCallback?.Invoke($"Main plugin DLL {descriptor.MainFileName} not found in archive.");
            }
        }
        catch(Exception ex)
        {
            errorCallback?.Invoke("Unexpected error during installation: " + ex.Message);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static void ExtractEntry(ZipArchiveEntry entry, string targetDirectory, Action<string> errorCallback)
    {
        try
        {
            var destPath = Path.Combine(targetDirectory, entry.FullName);
            var destDir = Path.GetDirectoryName(destPath);
            if(!string.IsNullOrEmpty(destDir))
            {
                Directory.CreateDirectory(destDir);
            }

            PluginLog.Information($"Copying {entry.FullName} to {destPath}");
            using var entryStream = entry.Open();
            using var fileStream = new FileStream(destPath, FileMode.Create, FileAccess.Write, FileShare.None);
            entryStream.CopyTo(fileStream);
        }
        catch(Exception ex)
        {
            errorCallback?.Invoke($"Failed to extract {entry.FullName}: {ex.Message}");
        }
    }
}
