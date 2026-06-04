using System;

namespace Opus.Engine.AlphaStress.KnownIssues;

/// <summary>
/// One entry in the alpha known-issue ledger. The shape is engine-neutral and writes
/// round-trip cleanly through <c>System.Text.Json</c> with no custom converters: stable
/// id, severity tier, lifecycle status, short summary, optional detail body, and
/// observed-at timestamp. The harness derives blocker / must-fix counts directly from
/// these records.
/// </summary>
/// <param name="Id">Stable identifier (e.g. <c>"STR-2026-001"</c>). Used by the harness
/// to dedupe and by tester reports to cross-reference between runs.</param>
/// <param name="Severity">Triage tier — see <see cref="KnownIssueSeverity"/>.</param>
/// <param name="Status">Lifecycle state.</param>
/// <param name="Summary">One-line headline.</param>
/// <param name="Detail">Optional multi-line body. Empty/whitespace is normalised to
/// <c>null</c> at construction so the JSON shape stays compact.</param>
/// <param name="ObservedAtUtc">UTC timestamp the issue was first recorded.</param>
public sealed record KnownIssueRecord(
    string Id,
    KnownIssueSeverity Severity,
    KnownIssueStatus Status,
    string Summary,
    string? Detail,
    DateTimeOffset ObservedAtUtc)
{
    /// <summary>Maximum allowed length of <see cref="Id"/>; keeps grep-friendly output
    /// stable across tester reports.</summary>
    public const int MaxIdLength = 64;

    /// <summary>Maximum allowed length of <see cref="Summary"/>.</summary>
    public const int MaxSummaryLength = 256;

    /// <summary>Throws when the record is internally inconsistent.</summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Id))
        {
            throw new ArgumentException("Id must not be empty.", nameof(Id));
        }

        if (Id.Length > MaxIdLength)
        {
            throw new ArgumentException(
                $"Id must be at most {MaxIdLength} characters.",
                nameof(Id));
        }

        if (string.IsNullOrWhiteSpace(Summary))
        {
            throw new ArgumentException("Summary must not be empty.", nameof(Summary));
        }

        if (Summary.Length > MaxSummaryLength)
        {
            throw new ArgumentException(
                $"Summary must be at most {MaxSummaryLength} characters.",
                nameof(Summary));
        }
    }

    /// <summary>Returns a normalised copy: trimmed id, trimmed summary, null detail when
    /// the input is empty/whitespace. The harness uses this so author input that comes
    /// from JSON or CLI plumbing reaches downstream code in a canonical shape.</summary>
    public KnownIssueRecord Normalised()
    {
        var trimmedDetail = string.IsNullOrWhiteSpace(Detail) ? null : Detail!.Trim();
        return new KnownIssueRecord(
            Id: Id.Trim(),
            Severity: Severity,
            Status: Status,
            Summary: Summary.Trim(),
            Detail: trimmedDetail,
            ObservedAtUtc: ObservedAtUtc.ToUniversalTime());
    }
}
