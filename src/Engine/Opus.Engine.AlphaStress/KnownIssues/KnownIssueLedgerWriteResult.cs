using System;

namespace Opus.Engine.AlphaStress.KnownIssues;

/// <summary>
/// Outcome of <see cref="KnownIssueLedgerWriter.Write"/>. Either a successful path to
/// the persisted JSON or a structured <see cref="KnownIssueLedgerWriteIssue"/>; never
/// both.
/// </summary>
public sealed record KnownIssueLedgerWriteResult(
    string? Path,
    KnownIssueLedgerWriteIssue? Issue)
{
    /// <summary>True when the writer persisted the ledger successfully.</summary>
    public bool IsSuccess => Path is not null && Issue is null;

    /// <summary>Builds a success outcome with the persisted path.</summary>
    public static KnownIssueLedgerWriteResult Success(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return new KnownIssueLedgerWriteResult(path, Issue: null);
    }

    /// <summary>Builds a failed outcome with a structured issue.</summary>
    public static KnownIssueLedgerWriteResult Failed(KnownIssueLedgerWriteIssue issue)
    {
        ArgumentNullException.ThrowIfNull(issue);
        return new KnownIssueLedgerWriteResult(Path: null, issue);
    }
}
