using System.Text.Json;
using System.Text.Json.Serialization;

namespace Opus.Content.Packaging.Manifest;

/// <summary>
/// Authoring metadata block for package provenance and reproducibility diagnostics.
/// </summary>
public sealed record ContentPackageAuthoring(ContentPackageBuildInfo? ToolBuild)
{
    /// <summary>Unknown future authoring fields preserved for forward compatibility.</summary>
    [JsonExtensionData]
    public IDictionary<string, JsonElement>? ExtensionData { get; init; }
}
