using System.Text.Json;
using System.Text.Json.Serialization;

namespace Opus.Content.Packaging.Manifest;

/// <summary>
/// One file declared by the package manifest. Paths are package-relative POSIX-style
/// paths and are validated before any filesystem access.
/// </summary>
public sealed record ContentPackageFile(
    string Path,
    string Type,
    long SizeBytes,
    string Sha256,
    IReadOnlyDictionary<string, string>? Metadata)
{
    /// <summary>Unknown future file fields preserved for forward compatibility.</summary>
    [JsonExtensionData]
    public IDictionary<string, JsonElement>? ExtensionData { get; init; }
}
