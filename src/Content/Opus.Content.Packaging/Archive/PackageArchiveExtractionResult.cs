using Opus.Content.Packaging.Diagnostics;

namespace Opus.Content.Packaging.Archive;

/// <summary>Outcome of extracting a <c>.opkg</c> archive to a directory: whether every entry was
/// written safely, plus any diagnostics raised while opening or unpacking the archive.</summary>
public sealed record PackageArchiveExtractionResult(
    bool Succeeded,
    IReadOnlyList<PackageDiagnostic> Diagnostics);
