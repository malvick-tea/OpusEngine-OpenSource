using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace Opus.Engine.AlphaStress.KnownIssues;

/// <summary>
/// Difference between two <see cref="KnownIssueLedger"/> snapshots. Holds a flat,
/// deterministically-ordered list of changes plus pre-computed counts per kind so
/// reporter pipelines can render the headline numbers without re-scanning the change
/// list. Built through <see cref="Compute"/>; immutable after construction.
/// </summary>
public sealed class KnownIssueLedgerDiff
{
    private readonly ImmutableArray<KnownIssueLedgerChange> _changes;

    /// <summary>Diff produced by comparing two identical (or both empty) ledgers.</summary>
    public static KnownIssueLedgerDiff Empty { get; } = new(ImmutableArray<KnownIssueLedgerChange>.Empty);

    /// <summary>Number of records present only on the right ledger.</summary>
    public int AddedCount { get; }

    /// <summary>Number of records present only on the left ledger.</summary>
    public int RemovedCount { get; }

    /// <summary>Number of records present on both ledgers with at least one field differing.</summary>
    public int ChangedCount { get; }

    /// <summary>Number of records present on both ledgers with identical content.</summary>
    public int UnchangedCount { get; }

    /// <summary>True when at least one record changed in any way.</summary>
    public bool HasChanges => AddedCount > 0 || RemovedCount > 0 || ChangedCount > 0;

    /// <summary>All change entries in stable order: change kind ascending
    /// (<see cref="KnownIssueChangeKind"/> declaration order), then id ordinal-ascending.</summary>
    public IReadOnlyList<KnownIssueLedgerChange> Changes => _changes;

    private KnownIssueLedgerDiff(ImmutableArray<KnownIssueLedgerChange> changes)
    {
        _changes = changes;
        foreach (var change in changes)
        {
            switch (change.Kind)
            {
                case KnownIssueChangeKind.Added:
                    AddedCount++;
                    break;
                case KnownIssueChangeKind.Removed:
                    RemovedCount++;
                    break;
                case KnownIssueChangeKind.Changed:
                    ChangedCount++;
                    break;
                case KnownIssueChangeKind.Unchanged:
                    UnchangedCount++;
                    break;
                default:
                    throw new InvalidOperationException(
                        $"Unhandled known-issue change kind '{change.Kind}'.");
            }
        }
    }

    /// <summary>Computes the diff between <paramref name="left"/> and
    /// <paramref name="right"/>. The diff is symmetric in id-coverage: every id from
    /// either side produces exactly one entry. Field comparison is by value-equality on
    /// the immutable <see cref="KnownIssueRecord"/> record type — `ObservedAtUtc` is
    /// converted to UTC inside <see cref="KnownIssueRecord.Normalised"/> so calling
    /// `Compute` against re-loaded ledgers produces stable results across time zones.</summary>
    public static KnownIssueLedgerDiff Compute(KnownIssueLedger left, KnownIssueLedger right)
    {
        ArgumentNullException.ThrowIfNull(left);
        ArgumentNullException.ThrowIfNull(right);

        var leftById = left.Records.ToDictionary(static r => r.Id, StringComparer.Ordinal);
        var rightById = right.Records.ToDictionary(static r => r.Id, StringComparer.Ordinal);
        var builder = ImmutableArray.CreateBuilder<KnownIssueLedgerChange>();
        var seenIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var leftRecord in left.Records)
        {
            if (!seenIds.Add(leftRecord.Id))
            {
                continue;
            }

            if (!rightById.TryGetValue(leftRecord.Id, out var rightRecord))
            {
                builder.Add(new KnownIssueLedgerChange(
                    Id: leftRecord.Id,
                    Kind: KnownIssueChangeKind.Removed,
                    Left: leftRecord,
                    Right: null));
                continue;
            }

            var kind = leftRecord.Equals(rightRecord)
                ? KnownIssueChangeKind.Unchanged
                : KnownIssueChangeKind.Changed;
            builder.Add(new KnownIssueLedgerChange(
                Id: leftRecord.Id,
                Kind: kind,
                Left: leftRecord,
                Right: rightRecord));
        }

        foreach (var rightRecord in right.Records)
        {
            if (!seenIds.Add(rightRecord.Id))
            {
                continue;
            }

            if (!leftById.ContainsKey(rightRecord.Id))
            {
                builder.Add(new KnownIssueLedgerChange(
                    Id: rightRecord.Id,
                    Kind: KnownIssueChangeKind.Added,
                    Left: null,
                    Right: rightRecord));
            }
        }

        builder.Sort(static (a, b) =>
        {
            var byKind = ((int)a.Kind).CompareTo((int)b.Kind);
            if (byKind != 0)
            {
                return byKind;
            }

            return string.CompareOrdinal(a.Id, b.Id);
        });
        return new KnownIssueLedgerDiff(builder.ToImmutable());
    }
}
