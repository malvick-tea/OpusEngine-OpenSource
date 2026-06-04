using System.Text.Json;
using System.Text.Json.Serialization;

namespace Opus.Content.Packaging.Manifest;

/// <summary>
/// Human/package identity block. It is engine-neutral: no consumer project names or game
/// rules belong here.
/// </summary>
public sealed record ContentPackageInfo(
    string Id,
    string DisplayName,
    string Version,
    string? CreatedAtUtc)
{
    /// <summary>Unknown future package-info fields preserved for forward compatibility.</summary>
    [JsonExtensionData]
    public IDictionary<string, JsonElement>? ExtensionData { get; init; }
}
