using System.Text.Json;
using System.Text.Json.Serialization;

namespace Opus.Content.Packaging.Manifest;

/// <summary>
/// Root manifest contract for an Opus alpha content package. Unknown fields are accepted
/// so additive M6.x schema growth does not break older validators.
/// </summary>
public sealed record ContentPackageManifest(
    ManifestFormatVersion FormatVersion,
    ContentPackageInfo Package,
    ContentPackageTarget Engine,
    ContentPackageAuthoring? Authoring,
    ContentPackageEntrypoints? Entrypoints,
    IReadOnlyList<string> RequiredFeatures,
    IReadOnlyList<ContentPackageFile> Files)
{
    /// <summary>Unknown future manifest fields preserved for forward compatibility.</summary>
    [JsonExtensionData]
    public IDictionary<string, JsonElement>? ExtensionData { get; init; }
}
