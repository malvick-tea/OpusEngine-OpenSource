using System;

namespace Opus.Engine.Host.Windows.Direct3D12.Frame;

/// <summary>Immutable rolling-window snapshot of CPU frame-time samples observed by the
/// D3D12 alpha host. Produced by <see cref="D3D12AlphaFrameMetrics.Snapshot"/>.</summary>
/// <param name="SampleCount">Number of samples in the snapshot's window (≤ window size).</param>
/// <param name="TotalFramesObserved">All-time frames the aggregator has seen; may exceed
/// <see cref="SampleCount"/> because the window discards the oldest sample on overflow.</param>
/// <param name="Mean">Arithmetic mean of the window.</param>
/// <param name="Min">Smallest sample in the window.</param>
/// <param name="Max">Largest sample in the window.</param>
/// <param name="P95">95th percentile of the window using nearest-rank.</param>
public readonly record struct D3D12AlphaFrameMetricsSnapshot(
    int SampleCount,
    long TotalFramesObserved,
    TimeSpan Mean,
    TimeSpan Min,
    TimeSpan Max,
    TimeSpan P95)
{
    public static D3D12AlphaFrameMetricsSnapshot Empty { get; } = new(
        SampleCount: 0,
        TotalFramesObserved: 0,
        Mean: TimeSpan.Zero,
        Min: TimeSpan.Zero,
        Max: TimeSpan.Zero,
        P95: TimeSpan.Zero);
}
