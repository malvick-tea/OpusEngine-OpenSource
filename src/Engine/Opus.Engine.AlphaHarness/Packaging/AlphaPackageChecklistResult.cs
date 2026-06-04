using System;
using System.Collections.Generic;
using System.Linq;

namespace Opus.Engine.AlphaHarness.Packaging;

/// <summary>
/// Outcome of <see cref="AlphaPackageChecklist.Run"/>. Sorts findings deterministically
/// (severity descending, code ascending, item ascending) so CI baselines stay diff-stable.
/// <see cref="IsClean"/> is the alpha-quality gate consumers compare against.
/// </summary>
public sealed record AlphaPackageChecklistResult
{
    private AlphaPackageChecklistResult(
        IReadOnlyList<AlphaPackageChecklistFinding> findings,
        int errorCount,
        int warningCount,
        int infoCount)
    {
        Findings = findings;
        ErrorCount = errorCount;
        WarningCount = warningCount;
        InfoCount = infoCount;
    }

    /// <summary>Findings in stable order — severity, code, item.</summary>
    public IReadOnlyList<AlphaPackageChecklistFinding> Findings { get; }

    /// <summary>Number of error findings.</summary>
    public int ErrorCount { get; }

    /// <summary>Number of warning findings.</summary>
    public int WarningCount { get; }

    /// <summary>Number of informational findings.</summary>
    public int InfoCount { get; }

    /// <summary>True when no finding has <see cref="AlphaPackageChecklistSeverity.Error"/>.</summary>
    public bool IsClean => ErrorCount == 0;

    /// <summary>Returns findings with the supplied stable diagnostic code; never null.</summary>
    public IEnumerable<AlphaPackageChecklistFinding> FindingsWithCode(string diagnosticCode) =>
        Findings.Where(finding => string.Equals(finding.DiagnosticCode, diagnosticCode, StringComparison.Ordinal));

    /// <summary>Builds a result, sorting findings deterministically and caching the
    /// per-severity counts so reporter loops are O(N) instead of O(N²).</summary>
    public static AlphaPackageChecklistResult From(IReadOnlyList<AlphaPackageChecklistFinding> findings)
    {
        ArgumentNullException.ThrowIfNull(findings);
        var sorted = findings
            .OrderByDescending(static finding => (int)finding.Severity)
            .ThenBy(static finding => finding.DiagnosticCode, StringComparer.Ordinal)
            .ThenBy(static finding => finding.Item, StringComparer.Ordinal)
            .ToArray();
        var errors = 0;
        var warnings = 0;
        var info = 0;
        foreach (var finding in sorted)
        {
            switch (finding.Severity)
            {
                case AlphaPackageChecklistSeverity.Error:
                    errors++;
                    break;
                case AlphaPackageChecklistSeverity.Warning:
                    warnings++;
                    break;
                case AlphaPackageChecklistSeverity.Info:
                    info++;
                    break;
            }
        }

        return new AlphaPackageChecklistResult(sorted, errors, warnings, info);
    }
}
