using System;
using System.Collections.Generic;

namespace Opus.Engine.AlphaStress.FramePacing;

/// <summary>
/// Streaming aggregator for <see cref="FramePacingObservation"/>. Captures every CPU
/// frame time, computes deterministic nearest-rank percentiles on demand, and tracks
/// the hitch count above the configured threshold. Thread-affinity: not thread-safe —
/// the stress harness owns a per-iteration aggregator and consumes it from the host
/// callback thread.
/// </summary>
public sealed class FramePacingAggregator
{
    private readonly List<TimeSpan> _samples = new();
    private readonly TimeSpan _hitchThreshold;
    private TimeSpan _sum = TimeSpan.Zero;
    private TimeSpan _max = TimeSpan.Zero;
    private int _hitchCount;
    private long _previousFrameNumber;

    /// <summary>Creates an aggregator with the supplied hitch threshold.</summary>
    public FramePacingAggregator(TimeSpan hitchThreshold)
    {
        if (hitchThreshold <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(hitchThreshold), "hitchThreshold must be positive.");
        }

        _hitchThreshold = hitchThreshold;
    }

    /// <summary>Number of frames observed so far.</summary>
    public int SampleCount => _samples.Count;

    /// <summary>Records a frame observation. Rejects out-of-order frame numbers loudly so
    /// a caller bug (double-fire, missed reset) surfaces at the boundary instead of
    /// silently skewing the summary.</summary>
    public void Record(FramePacingObservation observation)
    {
        ArgumentNullException.ThrowIfNull(observation);
        observation.Validate();
        if (observation.FrameNumber <= _previousFrameNumber)
        {
            throw new ArgumentOutOfRangeException(
                nameof(observation),
                $"FrameNumber must strictly increase; received {observation.FrameNumber} after {_previousFrameNumber}.");
        }

        _samples.Add(observation.CpuFrameTime);
        _sum += observation.CpuFrameTime;
        if (observation.CpuFrameTime > _max)
        {
            _max = observation.CpuFrameTime;
        }

        if (observation.CpuFrameTime >= _hitchThreshold)
        {
            _hitchCount++;
        }

        _previousFrameNumber = observation.FrameNumber;
    }

    /// <summary>Builds an immutable summary from the captured samples. The summary uses
    /// nearest-rank percentiles for determinism — interpolation would change rounding on
    /// repeated CI runs.</summary>
    public FramePacingSummary BuildSummary()
    {
        if (_samples.Count == 0)
        {
            return FramePacingSummary.Empty(_hitchThreshold);
        }

        var sorted = _samples.ToArray();
        Array.Sort(sorted);
        var mean = TimeSpan.FromTicks(_sum.Ticks / _samples.Count);
        return new FramePacingSummary(
            SampleCount: _samples.Count,
            Mean: mean,
            Median: NearestRank(sorted, 50),
            Percentile95: NearestRank(sorted, 95),
            Percentile99: NearestRank(sorted, 99),
            Max: _max,
            HitchCount: _hitchCount,
            HitchThreshold: _hitchThreshold);
    }

    private static TimeSpan NearestRank(TimeSpan[] sorted, int percentile)
    {
        var rank = (int)Math.Ceiling(percentile / 100.0 * sorted.Length);
        if (rank <= 0)
        {
            return sorted[0];
        }

        if (rank > sorted.Length)
        {
            rank = sorted.Length;
        }

        return sorted[rank - 1];
    }
}
