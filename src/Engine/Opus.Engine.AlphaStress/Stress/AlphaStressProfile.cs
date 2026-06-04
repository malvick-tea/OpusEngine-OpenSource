using System;
using Opus.Engine.AlphaHarness.Smoke;
using Opus.Engine.AlphaStress.FramePacing;
using Opus.Engine.AlphaStress.Memory;
using Opus.Engine.AlphaStress.Network;

namespace Opus.Engine.AlphaStress.Stress;

/// <summary>
/// Knobs for a complete stress run. Captures the underlying per-iteration smoke shape,
/// the iteration count, the run-wide wall-clock budget, and the deterministic
/// thresholds the harness applies against aggregated frame pacing and memory probe
/// summaries. Defaults are calibrated for the M11 20-player promise: a five-iteration
/// run reuses the M9 alpha smoke shape (60 frames each) inside a fifteen-second wall
/// budget — enough to surface a memory leak, a hitch pattern, or a degraded loopback
/// soak without spending a full tester slot.
/// </summary>
/// <param name="IterationProfile">Per-iteration smoke profile reused across every
/// iteration. The stress harness does not vary the inner shape across iterations —
/// repeatability is the contract.</param>
/// <param name="IterationCount">Number of iterations the stress harness runs.</param>
/// <param name="WallClockBudget">Maximum wall-clock duration the entire stress run is
/// allowed before the harness records <see cref="AlphaStressIssueCode.BudgetExceeded"/>
/// and stops scheduling further iterations.</param>
/// <param name="StressName">Stable display name surfaced in stress report headers and
/// log lines.</param>
/// <param name="FramePacing">Thresholds applied against the aggregated frame-pacing
/// summary.</param>
/// <param name="Memory">Thresholds applied against the aggregated memory probe
/// summary.</param>
/// <param name="Network">Optional fault-injection network profile. When non-null the
/// stress harness drives an <see cref="IAlphaStressNetworkProbe"/> per iteration and
/// evaluates the aggregated drop / soak-issue counters against the embedded
/// <see cref="AlphaStressFaultInjectionTolerance"/>. Null means the run skips the
/// network probe entirely — the legacy M11 shape.</param>
public sealed record AlphaStressProfile(
    AlphaSmokeProfile IterationProfile,
    int IterationCount,
    TimeSpan WallClockBudget,
    string StressName,
    FramePacingThresholds FramePacing,
    MemoryProbeThresholds Memory,
    AlphaStressNetworkProfile? Network = null)
{
    /// <summary>Minimum allowed iteration count. A zero-iteration run is not a stress
    /// run.</summary>
    public const int MinimumIterationCount = 1;

    /// <summary>Maximum allowed iteration count. Sized so a manual or CI stress
    /// completes in tens of seconds rather than hours; runtime overnight runs belong
    /// to a future M12 hardening harness.</summary>
    public const int MaximumIterationCount = 200;

    /// <summary>Default iteration count (5) — enough to surface a leak pattern without
    /// burning a full tester slot.</summary>
    public const int DefaultIterationCount = 5;

    /// <summary>Default canonical stress profile.</summary>
    public static AlphaStressProfile Default { get; } = new(
        IterationProfile: AlphaSmokeProfile.Default with { SmokeName = "opus-alpha-stress-iter" },
        IterationCount: DefaultIterationCount,
        WallClockBudget: TimeSpan.FromSeconds(15),
        StressName: "opus-alpha-stress",
        FramePacing: FramePacingThresholds.Default,
        Memory: MemoryProbeThresholds.Default);

    /// <summary>Throws when the profile is internally inconsistent.</summary>
    public void Validate()
    {
        ArgumentNullException.ThrowIfNull(IterationProfile);
        ArgumentNullException.ThrowIfNull(FramePacing);
        ArgumentNullException.ThrowIfNull(Memory);
        IterationProfile.Validate();
        FramePacing.Validate();
        Memory.Validate();
        Network?.Validate();
        if (IterationCount < MinimumIterationCount || IterationCount > MaximumIterationCount)
        {
            throw new ArgumentOutOfRangeException(
                nameof(IterationCount),
                $"IterationCount must be between {MinimumIterationCount} and {MaximumIterationCount}.");
        }

        if (WallClockBudget <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(WallClockBudget),
                "WallClockBudget must be positive.");
        }

        if (string.IsNullOrWhiteSpace(StressName))
        {
            throw new ArgumentException("StressName must not be empty.", nameof(StressName));
        }
    }
}
