using System.Text.Json;
using System.Text.Json.Serialization;

namespace Opus.Content.Packaging.Manifest;

/// <summary>
/// Engine target information declared by a package. M6 validates identity and version
/// compatibility without binding the package to a particular renderer implementation.
/// </summary>
public sealed record ContentPackageTarget(
    string Product,
    string TargetVersion,
    string? MinVersion,
    string? AssemblyCompatibility,
    IReadOnlyList<string> TargetAdapterFamilies)
{
    /// <summary>Unknown future target fields preserved for forward compatibility.</summary>
    [JsonExtensionData]
    public IDictionary<string, JsonElement>? ExtensionData { get; init; }
}
