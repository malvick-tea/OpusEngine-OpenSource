using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Opus.Engine.AlphaStress.KnownIssues;

/// <summary>
/// Immutable snapshot of the alpha known-issue ledger. Loud at the boundary: rejects
/// null records and duplicate ids at construction so downstream consumers (the stress
/// harness, the ledger writer) never have to handle ambiguous state. Open counts are
/// pre-computed so the harness checks blocker / must-fix presence in O(1) on the hot
/// path.
/// </summary>
public sealed class KnownIssueLedger
{
    /// <summary>Empty ledger — used when no known issues have been authored yet.</summary>
    public static KnownIssueLedger Empty { get; } = new(ImmutableArray<KnownIssueRecord>.Empty);

    private readonly ImmutableArray<KnownIssueRecord> _records;

    /// <summary>Public count of <see cref="KnownIssueStatus.Open"/> blockers.</summary>
    public int OpenBlockerCount { get; }

    /// <summary>Public count of <see cref="KnownIssueStatus.Open"/> must-fix entries.</summary>
    public int OpenMustFixCount { get; }

    /// <summary>Public count of <see cref="KnownIssueStatus.Open"/> post-alpha entries.</summary>
    public int OpenPostAlphaCount { get; }

    /// <summary>Total number of records in the ledger regardless of status.</summary>
    public int TotalCount => _records.Length;

    /// <summary>All records in deterministic order: severity ascending, then id
    /// ordinal-ascending. Lifecycle status is left untouched so closed entries follow
    /// open ones inside a severity bucket.</summary>
    public IReadOnlyList<KnownIssueRecord> Records => _records;

    private KnownIssueLedger(ImmutableArray<KnownIssueRecord> records)
    {
        _records = records;
        foreach (var record in records)
        {
            if (record.Status != KnownIssueStatus.Open)
            {
                continue;
            }

            switch (record.Severity)
            {
                case KnownIssueSeverity.Blocker:
                    OpenBlockerCount++;
                    break;
                case KnownIssueSeverity.MustFix:
                    OpenMustFixCount++;
                    break;
                case KnownIssueSeverity.PostAlpha:
                    OpenPostAlphaCount++;
                    break;
                default:
                    throw new InvalidOperationException(
                        $"Unhandled known-issue severity '{record.Severity}'.");
            }
        }
    }

    /// <summary>Builds a ledger from the supplied records. Loud at the boundary —
    /// rejects null collections, null entries, and duplicate ids.</summary>
    public static KnownIssueLedger Create(IEnumerable<KnownIssueRecord> records)
    {
        ArgumentNullException.ThrowIfNull(records);
        var normalised = new List<KnownIssueRecord>();
        var seenIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var raw in records)
        {
            if (raw is null)
            {
                throw new ArgumentException("Records must not contain null entries.", nameof(records));
            }

            var record = raw.Normalised();
            record.Validate();
            if (!seenIds.Add(record.Id))
            {
                throw new ArgumentException(
                    $"Duplicate known-issue id '{record.Id}'.",
                    nameof(records));
            }

            normalised.Add(record);
        }

        var ordered = normalised
            .OrderBy(static r => (int)r.Severity)
            .ThenBy(static r => r.Id, StringComparer.Ordinal)
            .ToImmutableArray();
        return new KnownIssueLedger(ordered);
    }

    /// <summary>Returns every record matching the supplied severity, in the ledger's
    /// canonical order. Never null.</summary>
    public IEnumerable<KnownIssueRecord> WithSeverity(KnownIssueSeverity severity) =>
        _records.Where(r => r.Severity == severity);

    /// <summary>Returns every open record, in the ledger's canonical order. Never null.</summary>
    public IEnumerable<KnownIssueRecord> OpenRecords() =>
        _records.Where(static r => r.Status == KnownIssueStatus.Open);
}
