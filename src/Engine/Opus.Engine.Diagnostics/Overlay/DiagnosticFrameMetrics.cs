using System;

namespace Opus.Engine.Diagnostics.Overlay;

/// <summary>Renderer-neutral rolling frame timing snapshot for diagnostics UI.</summary>
public sealed record DiagnosticFrameMetrics(
    int SampleCount,
    long TotalFramesObserved,
    TimeSpan Mean,
    TimeSpan Min,
    TimeSpan Max,
    TimeSpan P95)
{
    /// <summary>Zero-valued frame metrics for a host that has not rendered yet.</summary>
    public static DiagnosticFrameMetrics Empty { get; } = new(
        SampleCount: 0,
        TotalFramesObserved: 0,
        Mean: TimeSpan.Zero,
        Min: TimeSpan.Zero,
        Max: TimeSpan.Zero,
        P95: TimeSpan.Zero);
}
