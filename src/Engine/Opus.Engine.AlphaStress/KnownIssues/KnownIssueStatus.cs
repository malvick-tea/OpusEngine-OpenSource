namespace Opus.Engine.AlphaStress.KnownIssues;

/// <summary>
/// Lifecycle status for a known issue. Append-only enum — never reorder or repurpose
/// values once the ledger has shipped to a tester machine.
/// </summary>
public enum KnownIssueStatus
{
    /// <summary>Issue is active and unresolved. Counted by the stress harness against
    /// the configured severity thresholds.</summary>
    Open = 0,

    /// <summary>Issue is closed and verified. Recorded for audit only; not counted by
    /// the stress harness.</summary>
    Closed = 1,
}
