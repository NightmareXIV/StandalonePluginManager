namespace StandalonePluginManager.SPMData;

// Assume PluginManifest is defined elsewhere in your project

public class PluginDescriptor
{
    public string URL;
    public byte[] ArchiveData;
    public string MainFileName;
    public FileInfoData MainFile;
    public List<FileInfoData> Files;
    public SPMPluginManifest Manifest;
}
