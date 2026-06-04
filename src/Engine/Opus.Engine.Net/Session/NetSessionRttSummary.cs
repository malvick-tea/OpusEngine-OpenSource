using System;

namespace Opus.Engine.Net.Session;

/// <summary>
/// Rolling round-trip-time summary surfaced by <see cref="NetSessionStatisticsSnapshot"/>.
/// Populated only when a consumer-side ping protocol calls
/// <see cref="INetSession.RecordRtt"/> — the engine does not measure RTT itself because
/// the protocol shape is genre-specific.
/// </summary>
/// <param name="SampleCount">Number of RTT samples currently in the rolling window.
/// Zero when no consumer has fed an observation yet.</param>
/// <param name="WindowCapacity">Maximum number of samples the rolling window retains
/// before evicting the oldest. Constant for the lifetime of the session.</param>
/// <param name="Mean">Arithmetic mean of every retained sample. <c>TimeSpan.Zero</c>
/// when <see cref="SampleCount"/> is zero.</param>
/// <param name="Minimum">Smallest retained sample. <c>TimeSpan.Zero</c> when empty.</param>
/// <param name="Maximum">Largest retained sample. <c>TimeSpan.Zero</c> when empty.</param>
/// <param name="Percentile95">Nearest-rank P95 across the retained window.
/// <c>TimeSpan.Zero</c> when empty.</param>
public readonly record struct NetSessionRttSummary(
    int SampleCount,
    int WindowCapacity,
    TimeSpan Mean,
    TimeSpan Minimum,
    TimeSpan Maximum,
    TimeSpan Percentile95)
{
    /// <summary>The empty summary returned for a session that has no RTT data yet.</summary>
    public static NetSessionRttSummary Empty(int windowCapacity) => new(
        SampleCount: 0,
        WindowCapacity: windowCapacity,
        Mean: TimeSpan.Zero,
        Minimum: TimeSpan.Zero,
        Maximum: TimeSpan.Zero,
        Percentile95: TimeSpan.Zero);
}
