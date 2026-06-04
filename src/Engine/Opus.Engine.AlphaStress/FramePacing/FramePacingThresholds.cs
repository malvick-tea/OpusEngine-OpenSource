using System;

namespace Opus.Engine.AlphaStress.FramePacing;

/// <summary>
/// Engine-neutral thresholds the stress harness applies against an aggregated
/// <see cref="FramePacingSummary"/>. A summary exceeding any threshold marks the
/// stress run as <c>StressFramePacingDegraded</c> (<c>OPDX-STR-004</c>).
/// </summary>
/// <param name="P95Limit">Maximum allowed p95 CPU frame time. Defaults to the M5.1
/// alpha-frame budget (~33.4 ms at 30 Hz).</param>
/// <param name="P99Limit">Maximum allowed p99 CPU frame time. Defaults to one
/// missed-frame headroom above <paramref name="P95Limit"/>.</param>
/// <param name="MaxLimit">Maximum allowed peak CPU frame time. Defaults to 100 ms — a
/// single frame past 100 ms is a visible hitch even on tester hardware.</param>
/// <param name="HitchThreshold">Per-frame CPU time at which the frame counts as a hitch.
/// Defaults to twice the M5.1 budget (~66 ms = a dropped frame at 30 Hz).</param>
/// <param name="HitchCountLimit">Maximum number of hitches tolerated across the entire
/// stress run. Defaults to a single hitch — alpha-quality runs allow zero or one, never
/// a sustained pattern.</param>
public sealed record FramePacingThresholds(
    TimeSpan P95Limit,
    TimeSpan P99Limit,
    TimeSpan MaxLimit,
    TimeSpan HitchThreshold,
    int HitchCountLimit)
{
    /// <summary>Default thresholds calibrated for the M5.1 alpha-frame contract.</summary>
    public static FramePacingThresholds Default { get; } = new(
        P95Limit: TimeSpan.FromMilliseconds(33.4),
        P99Limit: TimeSpan.FromMilliseconds(50.0),
        MaxLimit: TimeSpan.FromMilliseconds(100.0),
        HitchThreshold: TimeSpan.FromMilliseconds(66.8),
        HitchCountLimit: 1);

    /// <summary>Throws when the thresholds are internally inconsistent.</summary>
    public void Validate()
    {
        if (P95Limit <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(P95Limit), "P95Limit must be positive.");
        }

        if (P99Limit < P95Limit)
        {
            throw new ArgumentOutOfRangeException(nameof(P99Limit), "P99Limit must be at least P95Limit.");
        }

        if (MaxLimit < P99Limit)
        {
            throw new ArgumentOutOfRangeException(nameof(MaxLimit), "MaxLimit must be at least P99Limit.");
        }

        if (HitchThreshold <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(HitchThreshold), "HitchThreshold must be positive.");
        }

        if (HitchCountLimit < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(HitchCountLimit), "HitchCountLimit must be non-negative.");
        }
    }
}
