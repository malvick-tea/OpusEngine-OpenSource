namespace Opus.Content.Packaging.Signing;

/// <summary>
/// Stable identifiers for the package-signature algorithms Opus understands. The value is the
/// exact string written into the signature envelope's <c>algorithm</c> field, so it must never
/// change for a published algorithm.
/// </summary>
public static class PackageSignatureAlgorithm
{
    /// <summary>ECDSA over the NIST P-256 curve with SHA-256, signature in IEEE P1363 form.
    /// Chosen because it is available in the .NET base class library with no extra dependency
    /// and produces compact keys and signatures.</summary>
    public const string EcdsaP256Sha256 = "ecdsa-p256-sha256";

    /// <summary>True when <paramref name="algorithm"/> is an algorithm this build can verify.</summary>
    public static bool IsSupported(string? algorithm) =>
        string.Equals(algorithm, EcdsaP256Sha256, StringComparison.Ordinal);
}
