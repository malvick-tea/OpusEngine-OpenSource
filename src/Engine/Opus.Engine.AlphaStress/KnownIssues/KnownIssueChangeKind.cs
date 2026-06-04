namespace Opus.Engine.AlphaStress.KnownIssues;

/// <summary>
/// Classification of a single record in a <see cref="KnownIssueLedgerDiff"/>. The
/// diff produces a flat sorted change list so reporter pipelines can grep for the
/// kind keyword without re-traversing the source ledgers.
/// </summary>
public enum KnownIssueChangeKind
{
    /// <summary>Record exists on both sides with identical content.</summary>
    Unchanged,

    /// <summary>Record id is missing from the left ledger and present on the right.</summary>
    Added,

    /// <summary>Record id is present on the left ledger and missing from the right.</summary>
    Removed,

    /// <summary>Record id is on both sides but at least one field differs.</summary>
    Changed,
}
