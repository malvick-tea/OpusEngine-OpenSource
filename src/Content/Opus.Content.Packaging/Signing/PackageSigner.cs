using System.Security.Cryptography;
using Opus.Content.Packaging.Validation;

namespace Opus.Content.Packaging.Signing;

/// <summary>
/// Produces a detached <see cref="PackageSignature"/> over a package manifest using ECDSA
/// P-256 / SHA-256. Pure of filesystem and archive concerns: the caller supplies the exact
/// manifest bytes and an opened private key, and chooses where the resulting envelope is stored.
/// The engine ships no keys — signing is a studio/consumer operation against a key it owns.
/// </summary>
public static class PackageSigner
{
    private const int RequiredKeySizeBits = 256;

    /// <summary>Signs <paramref name="manifestBytes"/> with <paramref name="privateKey"/>.</summary>
    /// <param name="manifestBytes">Exact manifest bytes to sign — the same bytes a verifier will
    /// re-read from the package.</param>
    /// <param name="privateKey">An ECDSA P-256 private key. The caller owns its lifetime.</param>
    /// <param name="keyId">Opaque identifier recorded in the envelope so a verifier can pick the
    /// matching public key.</param>
    public static PackageSignature Sign(ReadOnlySpan<byte> manifestBytes, ECDsa privateKey, string keyId)
    {
        ArgumentNullException.ThrowIfNull(privateKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(keyId);
        if (privateKey.KeySize != RequiredKeySizeBits)
        {
            throw new ArgumentException(
                $"Package signing requires a {RequiredKeySizeBits}-bit ECDSA key (P-256); the supplied key is {privateKey.KeySize}-bit.",
                nameof(privateKey));
        }

        var manifestSha256 = PackageFileHash.ComputeSha256Hex(manifestBytes);
        var signatureBytes = privateKey.SignData(manifestBytes, HashAlgorithmName.SHA256);
        return new PackageSignature(
            PackageSignatureAlgorithm.EcdsaP256Sha256,
            keyId,
            manifestSha256,
            Convert.ToBase64String(signatureBytes));
    }
}
