using Opus.Content.Packaging.Diagnostics;
using Opus.Content.Packaging.Manifest;

namespace Opus.Content.Packaging.Archive;

/// <summary>
/// Outcome of verifying a <c>.opkg</c> archive: integrity (file hashes against the manifest),
/// structure (bounds, safe entry names), and optionally authenticity (manifest signature).
/// <see cref="Succeeded"/> is true when no error-severity diagnostic was raised;
/// <see cref="SignatureVerified"/> is true only when a signature was cryptographically validated
/// against the supplied public key.
/// </summary>
public sealed record PackageArchiveVerificationResult(
    bool Succeeded,
    bool SignatureVerified,
    ContentPackageManifest? Manifest,
    IReadOnlyList<PackageDiagnostic> Diagnostics);
