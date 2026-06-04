using System;
using System.Collections.Generic;
using System.Linq;

namespace Opus.Engine.AlphaHarness.Machine;

/// <summary>
/// Outcome of <see cref="MachineProfileComparer.Compare"/>. Sorts differences
/// deterministically (severity descending, code ascending, field ascending) so the
/// printed table stays diff-stable between runs.
/// </summary>
public sealed record MachineProfileComparison
{
    private MachineProfileComparison(
        KnownGoodMachineProfile reference,
        KnownGoodMachineProfile captured,
        IReadOnlyList<MachineProfileDifference> differences,
        int errorCount,
        int warningCount,
        int infoCount)
    {
        Reference = reference;
        Captured = captured;
        Differences = differences;
        ErrorCount = errorCount;
        WarningCount = warningCount;
        InfoCount = infoCount;
    }

    /// <summary>Reference profile the captured one is compared against.</summary>
    public KnownGoodMachineProfile Reference { get; }

    /// <summary>Captured profile taken from the current host.</summary>
    public KnownGoodMachineProfile Captured { get; }

    /// <summary>Differences in stable order.</summary>
    public IReadOnlyList<MachineProfileDifference> Differences { get; }

    /// <summary>Count of error-severity differences.</summary>
    public int ErrorCount { get; }

    /// <summary>Count of warning-severity differences.</summary>
    public int WarningCount { get; }

    /// <summary>Count of info-severity differences.</summary>
    public int InfoCount { get; }

    /// <summary>True when no difference has <see cref="MachineProfileDifferenceSeverity.Error"/>.
    /// Hosts treat this as the alpha-quality machine compatibility gate.</summary>
    public bool IsCompatible => ErrorCount == 0;

    /// <summary>Builds a comparison and caches per-severity counts.</summary>
    public static MachineProfileComparison From(
        KnownGoodMachineProfile reference,
        KnownGoodMachineProfile captured,
        IReadOnlyList<MachineProfileDifference> differences)
    {
        ArgumentNullException.ThrowIfNull(reference);
        ArgumentNullException.ThrowIfNull(captured);
        ArgumentNullException.ThrowIfNull(differences);
        var sorted = differences
            .OrderByDescending(static difference => (int)difference.Severity)
            .ThenBy(static difference => difference.DiagnosticCode, StringComparer.Ordinal)
            .ThenBy(static difference => difference.Field, StringComparer.Ordinal)
            .ToArray();
        var errors = 0;
        var warnings = 0;
        var info = 0;
        foreach (var difference in sorted)
        {
            switch (difference.Severity)
            {
                case MachineProfileDifferenceSeverity.Error:
                    errors++;
                    break;
                case MachineProfileDifferenceSeverity.Warning:
                    warnings++;
                    break;
                case MachineProfileDifferenceSeverity.Info:
                    info++;
                    break;
            }
        }

        return new MachineProfileComparison(reference, captured, sorted, errors, warnings, info);
    }
}
