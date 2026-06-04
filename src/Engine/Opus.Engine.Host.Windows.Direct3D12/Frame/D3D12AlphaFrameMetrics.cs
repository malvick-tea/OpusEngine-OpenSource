using System;
using Opus.Engine.Renderer.Direct3D12.Alpha;

namespace Opus.Engine.Host.Windows.Direct3D12.Frame;

/// <summary>Rolling per-frame metrics aggregator for the D3D12 alpha host. Stores the
/// last <c>windowSize</c> CPU frame-time samples and produces an immutable
/// <see cref="D3D12AlphaFrameMetricsSnapshot"/> with mean / min / max / nearest-rank p95.
/// Thread-safe under the assumption used in practice: one writer (the render thread) and
/// occasional snapshot readers; <see cref="Snapshot"/> locks against
/// <see cref="Record"/> so a tester can dump metrics from another thread without
/// observing a torn buffer.</summary>
public sealed class D3D12AlphaFrameMetrics
{
    private readonly TimeSpan[] _buffer;
    private readonly object _writeLock = new();
    private int _writeIndex;
    private int _filled;
    private long _totalFramesObserved;

    public D3D12AlphaFrameMetrics(int windowSize)
    {
        if (windowSize < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(windowSize), "windowSize must be ≥ 1.");
        }

        _buffer = new TimeSpan[windowSize];
    }

    public int WindowSize => _buffer.Length;

    public long TotalFramesObserved
    {
        get
        {
            lock (_writeLock)
            {
                return _totalFramesObserved;
            }
        }
    }

    /// <summary>Records a CPU frame-time sample. The aggregator only needs the elapsed
    /// time; the wider <see cref="D3D12AlphaFrameDiagnostics"/> payload is consumed for
    /// API parity with the alpha-smoke contract but only its
    /// <see cref="D3D12AlphaFrameDiagnostics.CpuFrameTime"/> drives the windowed stats.</summary>
    public void Record(D3D12AlphaFrameDiagnostics diagnostics)
    {
        ArgumentNullException.ThrowIfNull(diagnostics);
        Record(diagnostics.CpuFrameTime);
    }

    /// <summary>Records a raw <paramref name="cpuFrameTime"/> sample. Negative durations
    /// are rejected: a negative frame time signals a Stopwatch bug at the caller, not a
    /// legitimate measurement.</summary>
    public void Record(TimeSpan cpuFrameTime)
    {
        if (cpuFrameTime < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(
                nameof(cpuFrameTime),
                "Frame time must be ≥ TimeSpan.Zero.");
        }

        lock (_writeLock)
        {
            _buffer[_writeIndex] = cpuFrameTime;
            _writeIndex = (_writeIndex + 1) % _buffer.Length;
            if (_filled < _buffer.Length)
            {
                _filled++;
            }

            _totalFramesObserved++;
        }
    }

    /// <summary>Returns an immutable snapshot of the current window. The caller may keep
    /// or share it freely; subsequent <see cref="Record"/> calls do not mutate it.</summary>
    public D3D12AlphaFrameMetricsSnapshot Snapshot()
    {
        TimeSpan[] copy;
        long totalFramesObserved;
        int sampleCount;
        lock (_writeLock)
        {
            if (_filled == 0)
            {
                return D3D12AlphaFrameMetricsSnapshot.Empty;
            }

            copy = new TimeSpan[_filled];
            sampleCount = _filled;
            totalFramesObserved = _totalFramesObserved;

            if (_filled < _buffer.Length)
            {
                Array.Copy(_buffer, copy, _filled);
            }
            else
            {
                var head = _writeIndex;
                Array.Copy(_buffer, head, copy, 0, _buffer.Length - head);
                Array.Copy(_buffer, 0, copy, _buffer.Length - head, head);
            }
        }

        return ComputeStats(copy, sampleCount, totalFramesObserved);
    }

    private static D3D12AlphaFrameMetricsSnapshot ComputeStats(
        TimeSpan[] samples,
        int sampleCount,
        long totalFramesObserved)
    {
        var sorted = (TimeSpan[])samples.Clone();
        Array.Sort(sorted);

        var sum = TimeSpan.Zero;
        for (var i = 0; i < sampleCount; i++)
        {
            sum += samples[i];
        }

        var mean = TimeSpan.FromTicks(sum.Ticks / sampleCount);
        var min = sorted[0];
        var max = sorted[sampleCount - 1];
        var p95Index = Math.Min(sampleCount - 1, (int)Math.Ceiling(0.95 * sampleCount) - 1);
        if (p95Index < 0)
        {
            p95Index = 0;
        }

        var p95 = sorted[p95Index];

        return new D3D12AlphaFrameMetricsSnapshot(
            SampleCount: sampleCount,
            TotalFramesObserved: totalFramesObserved,
            Mean: mean,
            Min: min,
            Max: max,
            P95: p95);
    }
}
