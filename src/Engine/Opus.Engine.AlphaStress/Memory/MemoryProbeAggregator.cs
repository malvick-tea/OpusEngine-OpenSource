using System;
using System.Collections.Generic;

namespace Opus.Engine.AlphaStress.Memory;

/// <summary>
/// Streaming aggregator for <see cref="MemoryProbeSample"/>. Captures the first and last
/// samples, tracks peak working set and peak managed heap, and emits the growth deltas
/// the stress harness needs to evaluate <see cref="MemoryProbeThresholds"/>. Thread-
/// affinity: not thread-safe — the stress harness owns a per-run aggregator and consumes
/// it on the orchestration thread.
/// </summary>
public sealed class MemoryProbeAggregator
{
    private readonly List<MemoryProbeSample> _samples = new();
    private MemoryProbeSample? _first;
    private MemoryProbeSample? _last;
    private long _peakWorkingSetBytes;
    private long _peakManagedHeapBytes;

    /// <summary>Number of samples observed so far.</summary>
    public int SampleCount => _samples.Count;

    /// <summary>Records a sample. Rejects out-of-order observations loudly so a caller
    /// bug (sample taken before the previous one's timestamp) surfaces immediately
    /// instead of silently producing nonsense deltas.</summary>
    public void Record(MemoryProbeSample sample)
    {
        ArgumentNullException.ThrowIfNull(sample);
        sample.Validate();
        if (_last is not null && sample.ObservedAtUtc < _last.ObservedAtUtc)
        {
            throw new ArgumentOutOfRangeException(
                nameof(sample),
                "ObservedAtUtc must not move backwards across consecutive samples.");
        }

        _samples.Add(sample);
        _first ??= sample;
        _last = sample;

        if (sample.WorkingSetBytes > _peakWorkingSetBytes)
        {
            _peakWorkingSetBytes = sample.WorkingSetBytes;
        }

        if (sample.ManagedHeapBytes > _peakManagedHeapBytes)
        {
            _peakManagedHeapBytes = sample.ManagedHeapBytes;
        }
    }

    /// <summary>Builds the aggregated summary. Returns <see cref="MemoryProbeSummary.Empty"/>
    /// when no samples were observed.</summary>
    public MemoryProbeSummary BuildSummary()
    {
        if (_first is null || _last is null)
        {
            return MemoryProbeSummary.Empty;
        }

        return new MemoryProbeSummary(
            SampleCount: _samples.Count,
            First: _first,
            Last: _last,
            PeakWorkingSetBytes: _peakWorkingSetBytes,
            PeakManagedHeapBytes: _peakManagedHeapBytes,
            ManagedHeapGrowthBytes: Math.Max(0, _last.ManagedHeapBytes - _first.ManagedHeapBytes),
            WorkingSetGrowthBytes: Math.Max(0, _last.WorkingSetBytes - _first.WorkingSetBytes),
            Gen2CollectionsDelta: Math.Max(0, _last.Gen2Collections - _first.Gen2Collections));
    }
}
