using System;
using System.Diagnostics;
using Opus.Engine.Pal.Time;

namespace Opus.Engine.Pal.Windows.Time;

/// <summary>
/// <see cref="Stopwatch"/>-backed implementation of <see cref="IHighResClock"/>. On
/// modern Windows / x64 this resolves to QueryPerformanceCounter — sub-microsecond
/// precision, monotonic across CPU sleeps.
/// </summary>
public sealed class StopwatchClock : IHighResClock
{
    private readonly Stopwatch _sw = Stopwatch.StartNew();

    public double GetElapsedSeconds() => _sw.Elapsed.TotalSeconds;

    public DateTimeOffset UtcNow() => DateTimeOffset.UtcNow;

    public long TickFrequency => Stopwatch.Frequency;
}
