using System.Text.Json;
using System.Text.Json.Serialization;

namespace Opus.Content.Packaging.Manifest;

/// <summary>
/// Optional package entrypoints that sample hosts or future runtime mounts may consume
/// after validation. Absence is valid for pure library packages.
/// </summary>
public sealed record ContentPackageEntrypoints(string? PrimaryScene, IReadOnlyList<string> Locales)
{
    /// <summary>Unknown future entrypoint fields preserved for forward compatibility.</summary>
    [JsonExtensionData]
    public IDictionary<string, JsonElement>? ExtensionData { get; init; }
}
