namespace Opus.App.OpusAlpha.Cli;

/// <summary>
/// Failure family the alpha host can deliberately inject at startup so a tester or lead can
/// confirm, on their own machine, that the diagnostics path produces a complete failure-report
/// bundle before a real crash ever happens. Selected through the <c>--inject-failure</c> window
/// option; <see cref="None"/> is the normal no-injection default. The injected fault is raised at
/// the host-build boundary, before any GPU work, so the self-check runs without a live device and
/// flows through the same classification / capture / write path a genuine engine failure would.
/// </summary>
public enum AlphaFaultKind
{
    /// <summary>No fault injected — normal host startup.</summary>
    None,

    /// <summary>Simulated startup failure (for example an out-of-memory at boot); surfaces as a
    /// startup-failure report.</summary>
    Startup,

    /// <summary>Simulated content-load failure; surfaces as a content-failure report.</summary>
    Content,

    /// <summary>Simulated graphics device loss; surfaces as a device-lost report.</summary>
    DeviceLost,
}
