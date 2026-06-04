namespace Opus.Engine.AlphaStress.Stress;

/// <summary>
/// Strongly-typed issue kinds the stress harness records into
/// <see cref="AlphaStressOutcome.Issues"/>. The
/// <c>AlphaStressIssueCodeStrings.ToDiagnosticCode</c> helper translates these into
/// stable <c>OPDX-STR-*</c> codes so reporters do not depend on enum ordinals.
/// </summary>
public enum AlphaStressIssueCode
{
    /// <summary>Stress harness could not open the host before stepping any iteration —
    /// <c>OPDX-STR-001</c>.</summary>
    HostUnavailable,

    /// <summary>Stress harness exceeded its wall-clock budget — <c>OPDX-STR-002</c>.</summary>
    BudgetExceeded,

    /// <summary>A stress iteration's underlying smoke outcome was not clean —
    /// <c>OPDX-STR-003</c>.</summary>
    IterationFailed,

    /// <summary>Aggregated frame-pacing summary breached configured thresholds —
    /// <c>OPDX-STR-004</c>.</summary>
    FramePacingDegraded,

    /// <summary>Aggregated memory probe summary breached configured thresholds —
    /// <c>OPDX-STR-005</c>.</summary>
    MemoryGrowthExceeded,

    /// <summary>Fault-injection drove drops above the configured tolerance —
    /// <c>OPDX-STR-006</c>.</summary>
    FaultInjectionDegraded,

    /// <summary>A stress iteration threw an unhandled exception caught by the harness —
    /// <c>OPDX-STR-008</c>.</summary>
    IterationUnhandledException,

    /// <summary>A known-issue ledger blocker is open — <c>OPDX-STR-102</c>.</summary>
    KnownIssueBlockerOpen,

    /// <summary>A known-issue ledger must-fix entry is open — <c>OPDX-STR-103</c>.</summary>
    KnownIssueMustFixOpen,
}
