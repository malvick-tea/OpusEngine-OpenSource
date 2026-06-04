namespace Opus.Engine.AlphaStress;

/// <summary>
/// Stable diagnostic codes emitted by the M11 alpha-host stress surface. Mirrors the
/// append-only convention used by <c>OPDX-OVR-*</c>, <c>OPDX-REP-*</c>, <c>OPDX-LOG-*</c>,
/// <c>OPDX-NET-*</c>, <c>OPDX-ALH-*</c>, and <c>OPDX-CSR-*</c>: never renumber or repurpose
/// a code once it has shipped to a tester machine. New behaviour extends the namespace
/// rather than shifting existing values so log greps in the field stay reliable across
/// alpha builds.
/// </summary>
public static class AlphaStressDiagnosticCodes
{
    // ---- Stress harness (per-run aggregation) ------------------------------

    /// <summary>Stress harness could not open the underlying alpha host before stepping
    /// any iteration; the iteration never produced a smoke outcome.</summary>
    public const string StressHostUnavailable = "OPDX-STR-001";

    /// <summary>Stress harness exceeded its wall-clock budget while iterating; remaining
    /// iterations were dropped.</summary>
    public const string StressBudgetExceeded = "OPDX-STR-002";

    /// <summary>A stress iteration completed but the underlying smoke outcome was not
    /// clean — every contained smoke issue is mirrored on the stress outcome under this
    /// code with the iteration index attached.</summary>
    public const string StressIterationFailed = "OPDX-STR-003";

    /// <summary>Aggregated frame pacing summary breached the stress profile thresholds
    /// (p95 / hitch count above limit).</summary>
    public const string StressFramePacingDegraded = "OPDX-STR-004";

    /// <summary>Aggregated memory probe summary breached the stress profile thresholds
    /// (working-set growth above limit / managed-heap growth above limit).</summary>
    public const string StressMemoryGrowthExceeded = "OPDX-STR-005";

    /// <summary>Stress iteration drove a network soak rig and recorded fault-injection
    /// drops above the configured tolerance.</summary>
    public const string StressFaultInjectionDegraded = "OPDX-STR-006";

    /// <summary>Stress writer could not persist the paired JSON+TXT stress report under
    /// the diagnostics directory.</summary>
    public const string StressReportWriteFailed = "OPDX-STR-007";

    /// <summary>Stress iteration emitted an unhandled exception caught by the harness; the
    /// iteration is recorded with no smoke outcome attached.</summary>
    public const string StressIterationUnhandledException = "OPDX-STR-008";

    // ---- Known issue ledger (long-lived blocker / must-fix tracking) -------

    /// <summary>Known-issue ledger writer could not persist the ledger to disk.</summary>
    public const string KnownIssueLedgerWriteFailed = "OPDX-STR-101";

    /// <summary>Known-issue ledger contains an open blocker — surfaced into the stress
    /// outcome so a tester run leaves explicit evidence that a release-blocking issue
    /// remains.</summary>
    public const string KnownIssueBlockerOpen = "OPDX-STR-102";

    /// <summary>Known-issue ledger contains an open must-fix entry — surfaced as a stress
    /// warning so triage notices it during alpha runs.</summary>
    public const string KnownIssueMustFixOpen = "OPDX-STR-103";
}
