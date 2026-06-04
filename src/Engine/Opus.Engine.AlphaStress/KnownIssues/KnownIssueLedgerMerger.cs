using System;
using System.Collections.Generic;

namespace Opus.Engine.AlphaStress.KnownIssues;

/// <summary>
/// Merges two <see cref="KnownIssueLedger"/> snapshots into a single ledger using the
/// "overlay wins on collision" policy. Records that appear in both inputs with the
/// same id collapse to the overlay version; records unique to either side carry over
/// unchanged. The output ledger goes through <see cref="KnownIssueLedger.Create"/> so
/// the merged record set is re-validated and re-ordered to the canonical
/// severity-then-id shape.
/// </summary>
public static class KnownIssueLedgerMerger
{
    /// <summary>Merges <paramref name="overlay"/> into <paramref name="baseLedger"/>.
    /// Overlay wins on id collision so tester pipelines can rebase incoming fixes on
    /// top of an existing ledger without manual diffing.</summary>
    public static KnownIssueLedger Merge(KnownIssueLedger baseLedger, KnownIssueLedger overlay)
    {
        ArgumentNullException.ThrowIfNull(baseLedger);
        ArgumentNullException.ThrowIfNull(overlay);

        var merged = new Dictionary<string, KnownIssueRecord>(StringComparer.Ordinal);
        foreach (var record in baseLedger.Records)
        {
            merged[record.Id] = record;
        }

        foreach (var record in overlay.Records)
        {
            merged[record.Id] = record;
        }

        return KnownIssueLedger.Create(merged.Values);
    }
}
