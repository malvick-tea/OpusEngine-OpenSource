using System;

namespace Opus.Engine.Pal.Time;

/// <summary>
/// Platform high-resolution clock. Distinct from <c>Opus.Foundation.GameTime</c>:
/// this is wall-clock for profiling, telemetry timestamps, and frame pacing — it is
/// NOT advanced by the Sim fixed-step loop.
/// </summary>
public interface IHighResClock
{
    /// <summary>Monotonic seconds since process start with sub-microsecond precision.</summary>
    double GetElapsedSeconds();

    /// <summary>UTC wall-clock at the moment of the call.</summary>
    DateTimeOffset UtcNow();

    /// <summary>Frequency of the underlying timer in ticks per second.</summary>
    long TickFrequency { get; }
}
