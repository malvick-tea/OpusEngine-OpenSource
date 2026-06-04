using Opus.Content.Packaging.Validation;

namespace Opus.Content.Packaging.Archive;

/// <summary>
/// Well-known names and conventions for the Opus content package archive (<c>.opkg</c>). The
/// manifest and signature entries are container metadata — they are not payload files listed in
/// the manifest, so the verifier treats them as reserved.
/// </summary>
public static class OpusPackageArchive
{
    /// <summary>Canonical file extension for a packaged Opus content archive.</summary>
    public const string FileExtension = ".opkg";

    /// <summary>Reserved archive entry holding the package manifest.</summary>
    public const string ManifestEntryName = PackageValidator.DefaultManifestFileName;

    /// <summary>Reserved archive entry holding the detached signature envelope.</summary>
    public const string SignatureEntryName = PackageValidator.DefaultSignatureFileName;

    /// <summary>True when <paramref name="entryName"/> is a reserved container-metadata entry
    /// (manifest or signature) rather than a payload file listed in the manifest.</summary>
    public static bool IsReservedEntry(string entryName) =>
        string.Equals(entryName, ManifestEntryName, StringComparison.Ordinal)
        || string.Equals(entryName, SignatureEntryName, StringComparison.Ordinal);
}
