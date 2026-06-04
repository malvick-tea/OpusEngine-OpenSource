using System;

namespace Opus.Engine.Net.Session;

/// <summary>
/// Instantaneous per-second rate snapshot surfaced inside
/// <see cref="NetSessionStatisticsSnapshot"/>. Each field is the rate observed between
/// the previous <see cref="NetSessionStatistics.Snapshot"/> call (the "baseline") and
/// the current one, divided by the elapsed time. The first snapshot after
/// <see cref="NetSession.Start"/> returns zeros — there is no baseline to diff against.
/// </summary>
/// <param name="WindowDuration">Wall-clock duration the rate covers. Zero when no
/// baseline exists yet or when two consecutive snapshots arrive at the same instant.</param>
/// <param name="PacketsReceivedPerSecond">Inbound datagram rate across the window.</param>
/// <param name="PacketsSentPerSecond">Outbound datagram rate accepted by the transport
/// across the window.</param>
/// <param name="BytesReceivedPerSecond">Inbound payload-byte rate across the window.</param>
/// <param name="BytesSentPerSecond">Outbound payload-byte rate accepted by the transport
/// across the window.</param>
public readonly record struct NetSessionRateSnapshot(
    TimeSpan WindowDuration,
    double PacketsReceivedPerSecond,
    double PacketsSentPerSecond,
    double BytesReceivedPerSecond,
    double BytesSentPerSecond)
{
    /// <summary>The empty snapshot — used until at least two samples have arrived.</summary>
    public static NetSessionRateSnapshot Empty { get; } = new(
        WindowDuration: TimeSpan.Zero,
        PacketsReceivedPerSecond: 0.0,
        PacketsSentPerSecond: 0.0,
        BytesReceivedPerSecond: 0.0,
        BytesSentPerSecond: 0.0);
}
