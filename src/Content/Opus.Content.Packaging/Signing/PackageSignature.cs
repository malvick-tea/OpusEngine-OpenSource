using System.Text.Json;
using System.Text.Json.Serialization;

namespace Opus.Content.Packaging.Signing;

/// <summary>
/// Detached signature envelope for a content package, stored as the reserved
/// <c>opus.package.sig</c> archive entry. It authenticates the package transitively: it signs
/// the manifest bytes, and the manifest carries a SHA-256 for every payload file, so a valid
/// signature over an unaltered manifest proves the whole package is unaltered.
/// </summary>
/// <param name="Algorithm">Signature algorithm identifier (see <see cref="PackageSignatureAlgorithm"/>).</param>
/// <param name="KeyId">Caller-chosen identifier of the signing key, so a verifier can select the
/// matching public key from a trust store. Opaque to the engine.</param>
/// <param name="ManifestSha256">Lower-case hex SHA-256 of the exact manifest bytes that were
/// signed. A cross-check that lets verification report an altered manifest distinctly from a
/// bad signature.</param>
/// <param name="Signature">Base64-encoded signature bytes over the manifest.</param>
public sealed record PackageSignature(
    string Algorithm,
    string KeyId,
    string ManifestSha256,
    string Signature)
{
    /// <summary>Unknown future signature fields preserved for forward compatibility.</summary>
    [JsonExtensionData]
    public IDictionary<string, JsonElement>? ExtensionData { get; init; }
}
