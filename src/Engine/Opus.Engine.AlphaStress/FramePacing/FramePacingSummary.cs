using System;

namespace Opus.Engine.AlphaStress.FramePacing;

/// <summary>
/// Aggregated frame-pacing summary over one stress iteration or one stress run.
/// Plain data shape so the writer can serialise it through <c>System.Text.Json</c>
/// without reflection trickery, and so tests can assert against the numbers without
/// reaching into harness internals.
/// </summary>
/// <param name="SampleCount">Number of frames the aggregator observed.</param>
/// <param name="Mean">Arithmetic mean of CPU frame times.</param>
/// <param name="Median">50th percentile (nearest-rank).</param>
/// <param name="Percentile95">95th percentile (nearest-rank).</param>
/// <param name="Percentile99">99th percentile (nearest-rank).</param>
/// <param name="Max">Maximum observed CPU frame time.</param>
/// <param name="HitchCount">Number of frames that exceeded the configured hitch
/// threshold.</param>
/// <param name="HitchThreshold">Hitch threshold the aggregator was configured with.</param>
public sealed record FramePacingSummary(
    int SampleCount,
    TimeSpan Mean,
    TimeSpan Median,
    TimeSpan Percentile95,
    TimeSpan Percentile99,
    TimeSpan Max,
    int HitchCount,
    TimeSpan HitchThreshold)
{
    /// <summary>Empty summary — emitted when the aggregator observed zero frames.</summary>
    public static FramePacingSummary Empty(TimeSpan hitchThreshold) => new(
        SampleCount: 0,
        Mean: TimeSpan.Zero,
        Median: TimeSpan.Zero,
        Percentile95: TimeSpan.Zero,
        Percentile99: TimeSpan.Zero,
        Max: TimeSpan.Zero,
        HitchCount: 0,
        HitchThreshold: hitchThreshold);

    /// <summary>True when at least one frame was observed.</summary>
    public bool HasSamples => SampleCount > 0;
}
