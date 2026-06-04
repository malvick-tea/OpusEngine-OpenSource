using System.Collections.Generic;
using System.Linq;
using Opus.Content.Packaging.Diagnostics;
using Opus.Content.Packaging.Manifest;

namespace Opus.Content.Packaging.Generation;

/// <summary>
/// Outcome of a manifest generation pass: the generated manifest (null when the content root
/// is missing) plus any diagnostics raised while classifying files. Generation is best-effort
/// per file — an unclassifiable file is reported as a warning and omitted rather than failing
/// the whole pass, so a single stray file does not block manifesting the rest of the package.
/// </summary>
public sealed record PackageGenerationResult(
    ContentPackageManifest? Manifest,
    IReadOnlyList<PackageDiagnostic> Diagnostics)
{
    /// <summary>True when a manifest was produced.</summary>
    public bool HasManifest => Manifest is not null;

    /// <summary>True when a manifest was produced and no error-severity diagnostic was raised.
    /// Warnings (such as an unclassified file) do not clear this flag.</summary>
    public bool Succeeded =>
        HasManifest && Diagnostics.All(static d => d.Severity != PackageDiagnosticSeverity.Error);
}
