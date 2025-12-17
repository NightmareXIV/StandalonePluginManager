using System.Collections.Generic;
using Dalamud.Common.Game;
using Dalamud.Plugin.Internal.Types.Manifest;
using Newtonsoft.Json;

namespace StandalonePluginManager.SPMData;

/// <summary>
/// Information about a plugin, packaged in a json file with the DLL.
/// </summary>
public class SPMPluginManifest
{
    /// <inheritdoc/>
    [JsonProperty]
    public string Author { get; set; }

    /// <inheritdoc/>
    [JsonProperty]
    public string Name { get; set; } = null!;

    /// <inheritdoc/>
    [JsonProperty]
    public string Punchline { get; set; }

    /// <inheritdoc/>
    [JsonProperty]
    public string Description { get; set; }

    /// <inheritdoc/>
    [JsonProperty]
    public string Changelog { get; set; }

    /// <inheritdoc/>
    [JsonProperty]
    public List<string> Tags { get; set; }

    /// <summary>
    /// Gets a list of category tags defined on the plugin.
    /// </summary>
    [JsonProperty]
    public List<string> CategoryTags { get; set; }

    /// <summary>
    /// Gets or sets a value indicating whether the plugin is hidden in the plugin installer.
    /// This value comes from the plugin master and is in addition to the list of hidden names kept by Dalamud.
    /// </summary>
    [JsonProperty]
    public bool IsHide { get; set; }

    /// <inheritdoc/>
    [JsonProperty]
    public string InternalName { get; set; } = null!;

    /// <inheritdoc/>
    [JsonProperty]
    public Version AssemblyVersion { get; set; } = null!;

    /// <inheritdoc/>
    [JsonProperty]
    public Version TestingAssemblyVersion { get; set; }

    /// <inheritdoc/>
    [JsonProperty]
    public bool IsTestingExclusive { get; set; }

    /// <inheritdoc/>
    [JsonProperty]
    public string RepoUrl { get; set; }

    /// <inheritdoc/>
    [JsonProperty]
    public Version MinimumDalamudVersion { get; set; }

    /// <inheritdoc/>
    [JsonProperty]
    public int DalamudApiLevel { get; set; }

    /// <inheritdoc/>
    [JsonProperty]
    public int? TestingDalamudApiLevel { get; set; }

    /// <inheritdoc/>
    [JsonProperty]
    public long DownloadCount { get; set; }

    /// <inheritdoc/>
    [JsonProperty]
    public long LastUpdate { get; set; }

    /// <summary>
    /// Gets the download link used to install the plugin.
    /// </summary>
    [JsonProperty]
    public string DownloadLinkInstall { get; set; } = null!;

    /// <summary>
    /// Gets the download link used to update the plugin.
    /// </summary>
    [JsonProperty]
    public string DownloadLinkUpdate { get; set; } = null!;

    /// <summary>
    /// Gets the download link used to get testing versions of the plugin.
    /// </summary>
    [JsonProperty]
    public string DownloadLinkTesting { get; set; } = null!;

    /// <summary>
    /// Gets the required Dalamud load step for this plugin to load. Takes precedence over LoadPriority.
    /// Valid values are:
    /// 0. During Framework.Tick, when drawing facilities are available.
    /// 1. During Framework.Tick.
    /// 2. No requirement.
    /// </summary>
    [JsonProperty]
    public int LoadRequiredState { get; set; }

    /// <summary>
    /// Gets a value indicating whether Dalamud must load this plugin not at the same time with other plugins and the game.
    /// </summary>
    [JsonProperty]
    public bool LoadSync { get; set; }

    /// <summary>
    /// Gets the load priority for this plugin. Higher values means higher priority. 0 is default priority.
    /// </summary>
    [JsonProperty]
    public int LoadPriority { get; set; }

    /// <inheritdoc/>
    [JsonProperty]
    public bool CanUnloadAsync { get; set; }

    /// <inheritdoc/>
    [JsonProperty]
    public bool SupportsProfiles { get; set; } = true;

    /// <inheritdoc/>
    public List<string> ImageUrls { get; set; }

    /// <inheritdoc/>
    public string IconUrl { get; set; }

    /// <summary>
    /// Gets a value indicating whether this plugin accepts feedback.
    /// </summary>
    public bool AcceptsFeedback { get; set; } = true;

    /// <inheritdoc/>
    public string FeedbackMessage { get; set; }

    /// <inheritdoc/>
    [JsonProperty("_Dip17Channel")]
    public string Dip17Channel { get; set; }
}