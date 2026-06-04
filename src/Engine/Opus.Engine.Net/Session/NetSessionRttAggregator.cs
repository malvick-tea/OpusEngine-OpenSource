using System;

namespace Opus.Engine.Net.Session;

/// <summary>
/// Sliding-window aggregator over <see cref="TimeSpan"/> RTT samples. Stores at most
/// <see cref="WindowCapacity"/> samples in a ring buffer and computes mean / minimum /
/// maximum / nearest-rank P95 on demand. The aggregator does not measure RTT — it
/// receives values from <see cref="NetSessionStatistics.RecordRtt"/> when a consumer-
/// side ping protocol observes them.
/// </summary>
internal sealed class NetSessionRttAggregator
{
    private const double Percentile95Rank = 0.95;

    private readonly TimeSpan[] _samples;
    private readonly TimeSpan[] _sortBuffer;
    private int _writeIndex;
    private int _count;

    public NetSessionRttAggregator(int windowCapacity)
    {
        if (windowCapacity < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(windowCapacity), "Window capacity must be at least 1.");
        }

        WindowCapacity = windowCapacity;
        _samples = new TimeSpan[windowCapacity];
        _sortBuffer = new TimeSpan[windowCapacity];
    }

    public int WindowCapacity { get; }

    public int SampleCount => _count;

    public void Record(TimeSpan rtt)
    {
        if (rtt < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(rtt), "RTT must be non-negative.");
        }

        _samples[_writeIndex] = rtt;
        _writeIndex = (_writeIndex + 1) % WindowCapacity;
        if (_count < WindowCapacity)
        {
            _count++;
        }
    }

    public NetSessionRttSummary BuildSummary()
    {
        if (_count == 0)
        {
            return NetSessionRttSummary.Empty(WindowCapacity);
        }

        var total = TimeSpan.Zero;
        var min = TimeSpan.MaxValue;
        var max = TimeSpan.MinValue;
        for (var i = 0; i < _count; i++)
        {
            var sample = _samples[i];
            total += sample;
            if (sample < min)
            {
                min = sample;
            }

            if (sample > max)
            {
                max = sample;
            }

            _sortBuffer[i] = sample;
        }

        Array.Sort(_sortBuffer, 0, _count);
        var rankIndex = (int)Math.Ceiling(Percentile95Rank * _count) - 1;
        if (rankIndex < 0)
        {
            rankIndex = 0;
        }

        if (rankIndex >= _count)
        {
            rankIndex = _count - 1;
        }

        var p95 = _sortBuffer[rankIndex];
        var mean = TimeSpan.FromTicks(total.Ticks / _count);
        return new NetSessionRttSummary(
            SampleCount: _count,
            WindowCapacity: WindowCapacity,
            Mean: mean,
            Minimum: min,
            Maximum: max,
            Percentile95: p95);
    }

    public void Clear()
    {
        Array.Clear(_samples, 0, _samples.Length);
        Array.Clear(_sortBuffer, 0, _sortBuffer.Length);
        _writeIndex = 0;
        _count = 0;
    }
}
