using System.Security.Cryptography;
using Opus.Content.Packaging.Validation;

namespace Opus.Content.Packaging.Signing;

/// <summary>Outcome of verifying a <see cref="PackageSignature"/> against manifest bytes and a
/// public key.</summary>
public enum PackageSignatureVerification
{
    /// <summary>The signature is valid for the manifest bytes and public key.</summary>
    Valid,

    /// <summary>The envelope is missing required fields or its signature is not valid base64.</summary>
    Malformed,

    /// <summary>The signature names an algorithm this verifier does not implement.</summary>
    AlgorithmUnsupported,

    /// <summary>The manifest digest in the envelope does not match the supplied manifest bytes —
    /// the manifest was altered after it was signed.</summary>
    ManifestHashMismatch,

    /// <summary>The signature did not verify cryptographically against the public key.</summary>
    SignatureInvalid,
}

/// <summary>
/// Verifies a detached package signature. Pure: the caller supplies the manifest bytes (read
/// from the archive), the parsed envelope, and an opened public key.
/// </summary>
public static class PackageSignatureVerifier
{
    /// <summary>Verifies <paramref name="signature"/> over <paramref name="manifestBytes"/> using
    /// <paramref name="publicKey"/>.</summary>
    public static PackageSignatureVerification Verify(
        ReadOnlySpan<byte> manifestBytes,
        PackageSignature signature,
        ECDsa publicKey)
    {
        ArgumentNullException.ThrowIfNull(signature);
        ArgumentNullException.ThrowIfNull(publicKey);

        if (string.IsNullOrWhiteSpace(signature.Algorithm)
            || string.IsNullOrWhiteSpace(signature.ManifestSha256)
            || string.IsNullOrWhiteSpace(signature.Signature))
        {
            return PackageSignatureVerification.Malformed;
        }

        if (!PackageSignatureAlgorithm.IsSupported(signature.Algorithm))
        {
            return PackageSignatureVerification.AlgorithmUnsupported;
        }

        var actualSha256 = PackageFileHash.ComputeSha256Hex(manifestBytes);
        if (!string.Equals(actualSha256, signature.ManifestSha256, StringComparison.OrdinalIgnoreCase))
        {
            return PackageSignatureVerification.ManifestHashMismatch;
        }

        byte[] signatureBytes;
        try
        {
            signatureBytes = Convert.FromBase64String(signature.Signature);
        }
        catch (FormatException)
        {
            return PackageSignatureVerification.Malformed;
        }

        return publicKey.VerifyData(manifestBytes, signatureBytes, HashAlgorithmName.SHA256)
            ? PackageSignatureVerification.Valid
            : PackageSignatureVerification.SignatureInvalid;
    }
}
