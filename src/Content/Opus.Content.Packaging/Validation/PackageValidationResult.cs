using Opus.Content.Packaging.Diagnostics;
using Opus.Content.Packaging.Manifest;

namespace Opus.Content.Packaging.Validation;

/// <summary>
/// Aggregated result for package validation. The package is valid only when it has no
/// error diagnostics. Diagnostic ordering is deterministic — severity descending, then
/// stable code ascending, then target path ascending — so CI baselines stay diff-stable
/// regardless of validator pass ordering.
/// </summary>
public sealed record PackageValidationResult
{
    private PackageValidationResult(
        ContentPackageManifest? manifest,
        IReadOnlyList<PackageDiagnostic> diagnostics,
        int errorCount,
        int warningCount,
        int infoCount)
    {
        Manifest = manifest;
        Diagnostics = diagnostics;
        ErrorCount = errorCount;
        WarningCount = warningCount;
        InfoCount = infoCount;
    }

    /// <summary>Parsed manifest when the JSON could be loaded; null when it could not.</summary>
    public ContentPackageManifest? Manifest { get; }

    /// <summary>Diagnostics in stable order — severity, code, target.</summary>
    public IReadOnlyList<PackageDiagnostic> Diagnostics { get; }

    /// <summary>True when no diagnostic has <see cref="PackageDiagnosticSeverity.Error"/>.</summary>
    public bool IsValid => ErrorCount == 0;

    /// <summary>Number of error diagnostics.</summary>
    public int ErrorCount { get; }

    /// <summary>Number of warning diagnostics.</summary>
    public int WarningCount { get; }

    /// <summary>Number of informational diagnostics.</summary>
    public int InfoCount { get; }

    /// <summary>Builds a result, sorting diagnostics deterministically and caching the
    /// per-severity counts so reporter loops are O(N) instead of O(N²).</summary>
    public static PackageValidationResult From(
        ContentPackageManifest? manifest,
        IReadOnlyList<PackageDiagnostic> diagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);

        var sorted = diagnostics
            .OrderByDescending(d => (int)d.Severity)
            .ThenBy(d => d.Code.Value, StringComparer.Ordinal)
            .ThenBy(d => d.Target.Path ?? string.Empty, StringComparer.Ordinal)
            .ToArray();

        var errors = 0;
        var warnings = 0;
        var info = 0;
        foreach (var diagnostic in sorted)
        {
            switch (diagnostic.Severity)
            {
                case PackageDiagnosticSeverity.Error:
                    errors++;
                    break;
                case PackageDiagnosticSeverity.Warning:
                    warnings++;
                    break;
                case PackageDiagnosticSeverity.Info:
                    info++;
                    break;
            }
        }

        return new PackageValidationResult(manifest, sorted, errors, warnings, info);
    }
}
