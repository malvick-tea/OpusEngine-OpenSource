using System;

namespace Opus.Engine.AlphaStress.KnownIssues;

/// <summary>
/// Filesystem options for <see cref="KnownIssueLedgerWriter"/>. The writer persists the
/// ledger atomically (temp + replace) at the configured path.
/// </summary>
/// <param name="FilePath">Absolute or relative path the ledger is written to. The
/// writer creates the parent directory on demand.</param>
public sealed record KnownIssueLedgerWriterOptions(string FilePath)
{
    /// <summary>Throws when the options are inconsistent.</summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(FilePath))
        {
            throw new ArgumentException("FilePath must not be empty.", nameof(FilePath));
        }
    }
}
