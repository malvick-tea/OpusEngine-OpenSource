using Opus.Content.Packaging.Diagnostics;

namespace Opus.Content.Packaging.Archive;

/// <summary>Outcome of packing a <c>.opkg</c> archive: whether it was written, where, whether it
/// was signed, and any diagnostics raised while validating the manifest or writing the file.</summary>
public sealed record PackageArchivePackResult(
    bool Succeeded,
    string? ArchivePath,
    bool IsSigned,
    IReadOnlyList<PackageDiagnostic> Diagnostics);
