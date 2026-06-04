using System;

namespace Opus.Engine.Net.Session;

/// <summary>
/// Computes the per-second rate of cumulative session counters between two consecutive
/// <see cref="NetSessionStatistics.Snapshot"/> calls. Holds the previous baseline so the
/// next snapshot can diff against it. The first snapshot after a session starts (or after
/// <see cref="Reset"/>) returns <see cref="NetSessionRateSnapshot.Empty"/> because there
/// is no baseline to diff against.
/// </summary>
internal sealed class NetSessionRateAggregator
{
    private DateTimeOffset _baselineAtUtc;
    private long _baselinePacketsReceived;
    private long _baselinePacketsSent;
    private long _baselineBytesReceived;
    private long _baselineBytesSent;
    private bool _hasBaseline;

    public NetSessionRateSnapshot Sample(
        DateTimeOffset nowUtc,
        long packetsReceived,
        long packetsSent,
        long bytesReceived,
        long bytesSent)
    {
        if (!_hasBaseline)
        {
            UpdateBaseline(nowUtc, packetsReceived, packetsSent, bytesReceived, bytesSent);
            return NetSessionRateSnapshot.Empty;
        }

        var window = nowUtc - _baselineAtUtc;
        if (window <= TimeSpan.Zero)
        {
            // Snapshots arriving at the same instant — preserve the existing baseline and
            // surface an empty rate; the next non-zero-elapsed sample will produce a real
            // rate without losing the cumulative deltas.
            return NetSessionRateSnapshot.Empty;
        }

        var packetsInDelta = NonNegative(packetsReceived - _baselinePacketsReceived);
        var packetsOutDelta = NonNegative(packetsSent - _baselinePacketsSent);
        var bytesInDelta = NonNegative(bytesReceived - _baselineBytesReceived);
        var bytesOutDelta = NonNegative(bytesSent - _baselineBytesSent);
        var seconds = window.TotalSeconds;
        var rate = new NetSessionRateSnapshot(
            WindowDuration: window,
            PacketsReceivedPerSecond: packetsInDelta / seconds,
            PacketsSentPerSecond: packetsOutDelta / seconds,
            BytesReceivedPerSecond: bytesInDelta / seconds,
            BytesSentPerSecond: bytesOutDelta / seconds);

        UpdateBaseline(nowUtc, packetsReceived, packetsSent, bytesReceived, bytesSent);
        return rate;
    }

    public void Reset()
    {
        _hasBaseline = false;
        _baselineAtUtc = default;
        _baselinePacketsReceived = 0;
        _baselinePacketsSent = 0;
        _baselineBytesReceived = 0;
        _baselineBytesSent = 0;
    }

    private void UpdateBaseline(
        DateTimeOffset nowUtc,
        long packetsReceived,
        long packetsSent,
        long bytesReceived,
        long bytesSent)
    {
        _baselineAtUtc = nowUtc;
        _baselinePacketsReceived = packetsReceived;
        _baselinePacketsSent = packetsSent;
        _baselineBytesReceived = bytesReceived;
        _baselineBytesSent = bytesSent;
        _hasBaseline = true;
    }

    private static long NonNegative(long value) => value < 0 ? 0 : value;
}
