using System.Security.Cryptography;
using Opus.Content.Packaging.Manifest;

namespace Opus.Content.Packaging.Archive;

/// <summary>
/// Inputs for <see cref="PackageArchivePacker"/>: the content directory to pack, the manifest
/// describing its files (produced by the generator or loaded from disk), and where to write the
/// <c>.opkg</c>. Signing is optional and the engine never supplies the key — the caller owns it.
/// </summary>
public sealed record PackageArchivePackRequest(
    string ContentRoot,
    ContentPackageManifest Manifest,
    string OutputArchivePath)
{
    /// <summary>Optional ECDSA P-256 private key. When set, the manifest is signed and the
    /// signature embedded as the reserved signature entry; <see cref="SigningKeyId"/> must also
    /// be set.</summary>
    public ECDsa? SigningKey { get; init; }

    /// <summary>Identifier recorded in the signature envelope so a verifier can pick the matching
    /// public key.</summary>
    public string? SigningKeyId { get; init; }

    /// <summary>Container limits the produced archive must fit within.</summary>
    public OpusPackageArchiveLimits Limits { get; init; } = OpusPackageArchiveLimits.Default;
}
