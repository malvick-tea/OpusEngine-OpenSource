namespace Opus.Engine.AlphaStress.KnownIssues;

/// <summary>
/// One entry inside a <see cref="KnownIssueLedgerDiff"/>. Captures the change kind
/// together with the left-side and right-side projection of the record so testers can
/// see what the ledger said before and after in one place.
/// </summary>
/// <param name="Id">Stable identifier shared between left and right (or the unique side's
/// id when the change is Added / Removed).</param>
/// <param name="Kind">Classification of the change.</param>
/// <param name="Left">Record as it appears on the left ledger, or null when the change
/// is <see cref="KnownIssueChangeKind.Added"/>.</param>
/// <param name="Right">Record as it appears on the right ledger, or null when the change
/// is <see cref="KnownIssueChangeKind.Removed"/>.</param>
public sealed record KnownIssueLedgerChange(
    string Id,
    KnownIssueChangeKind Kind,
    KnownIssueRecord? Left,
    KnownIssueRecord? Right);
