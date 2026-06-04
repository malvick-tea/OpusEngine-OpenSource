using System;

namespace Opus.Engine.Net.Session;

/// <summary>
/// Mutable rolling counters owned exclusively by <see cref="NetSession"/>. Exposed as a
/// dedicated type so the session orchestrator stays focused on state-machine work and
/// every counter mutation can be reviewed in one place. Snapshot reads are pure.
/// </summary>
internal sealed class NetSessionStatistics
{
    /// <summary>Default capacity of the rolling RTT sample window (64 samples).</summary>
    public const int DefaultRttWindowCapacity = 64;

    private readonly NetSessionRttAggregator _rtt;
    private readonly NetSessionRateAggregator _rate = new();

    private long _peersAccepted;
    private long _peersDisconnected;
    private long _packetsReceived;
    private long _packetsSent;
    private long _packetsSendDropped;
    private long _bytesReceived;
    private long _bytesSent;
    private int _reconnectAttempts;
    private long _queuedPayloadsDropped;

    public NetSessionStatistics()
        : this(DefaultRttWindowCapacity)
    {
    }

    public NetSessionStatistics(int rttWindowCapacity)
    {
        _rtt = new NetSessionRttAggregator(rttWindowCapacity);
    }

    public void RecordPeerAccepted() => _peersAccepted++;

    public void RecordPeerDisconnected() => _peersDisconnected++;

    public void RecordPacketReceived(int byteCount)
    {
        _packetsReceived++;
        _bytesReceived += byteCount;
    }

    public void RecordPacketSent(int byteCount)
    {
        _packetsSent++;
        _bytesSent += byteCount;
    }

    public void RecordSendDropped() => _packetsSendDropped++;

    public void RecordReconnectAttempt() => _reconnectAttempts++;

    public void RecordQueuedPayloadDropped() => _queuedPayloadsDropped++;

    public void RecordRtt(TimeSpan rtt) => _rtt.Record(rtt);

    public void ResetRateBaseline() => _rate.Reset();

    public NetSessionStatisticsSnapshot Snapshot(
        int connectedPeerCount,
        NetTransportGuardCounts transportGuards,
        DateTimeOffset capturedAtUtc)
    {
        var nowUtc = capturedAtUtc.ToUniversalTime();
        var rate = _rate.Sample(
            nowUtc,
            _packetsReceived,
            _packetsSent,
            _bytesReceived,
            _bytesSent);
        return new NetSessionStatisticsSnapshot(
            ConnectedPeerCount: connectedPeerCount,
            PeersAcceptedTotal: _peersAccepted,
            PeersDisconnectedTotal: _peersDisconnected,
            PacketsReceived: _packetsReceived,
            PacketsSent: _packetsSent,
            PacketsSendDropped: _packetsSendDropped,
            BytesReceived: _bytesReceived,
            BytesSent: _bytesSent,
            ReconnectAttempts: _reconnectAttempts,
            QueuedPayloadsDropped: _queuedPayloadsDropped,
            TransportGuards: transportGuards,
            Rtt: _rtt.BuildSummary(),
            Rate: rate,
            ObservedAtUtc: nowUtc);
    }
}
