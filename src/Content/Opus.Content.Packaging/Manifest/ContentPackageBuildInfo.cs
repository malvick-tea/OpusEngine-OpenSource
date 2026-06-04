using System.Text.Json;
using System.Text.Json.Serialization;

namespace Opus.Content.Packaging.Manifest;

/// <summary>
/// Build identity of the tool or process that authored the manifest. This mirrors the
/// Foundation <c>BuildInfo</c> report shape while staying serialisation-only.
/// </summary>
public sealed record ContentPackageBuildInfo(
    string Product,
    string ProductVersion,
    string ReleaseChannel,
    string Assembly,
    string AssemblyVersion,
    string Configuration,
    string Framework,
    string Os,
    string ProcessArchitecture)
{
    /// <summary>Unknown future build-info fields preserved for forward compatibility.</summary>
    [JsonExtensionData]
    public IDictionary<string, JsonElement>? ExtensionData { get; init; }
}
