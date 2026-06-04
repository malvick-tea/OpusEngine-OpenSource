using System;

namespace Opus.Engine.AlphaStress.FramePacing;

/// <summary>
/// Single-frame CPU pacing observation. Engine-neutral by design: only the frame
/// number, observation timestamp, and CPU frame time are recorded. The aggregator
/// derives mean / median / p95 / p99 / max / hitch count from a stream of these.
/// </summary>
/// <param name="FrameNumber">Monotonic frame counter starting at 1. The aggregator
/// trusts callers to feed strictly increasing values inside one iteration.</param>
/// <param name="ObservedAtUtc">UTC timestamp the host captured the frame at.</param>
/// <param name="CpuFrameTime">Wall-clock CPU frame duration measured by the host.</param>
public sealed record FramePacingObservation(
    long FrameNumber,
    DateTimeOffset ObservedAtUtc,
    TimeSpan CpuFrameTime)
{
    /// <summary>Throws when the observation is internally inconsistent.</summary>
    public void Validate()
    {
        if (FrameNumber < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(FrameNumber), "FrameNumber must be positive (frame counter is 1-based).");
        }

        if (CpuFrameTime < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(CpuFrameTime), "CpuFrameTime must be non-negative.");
        }
    }
}
