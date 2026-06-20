using System;
using System.Threading;

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
    private readonly object _aggregatorSync = new();

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

    public void RecordPeerAccepted() => Interlocked.Increment(ref _peersAccepted);

    public void RecordPeerDisconnected() => Interlocked.Increment(ref _peersDisconnected);

    public void RecordPacketReceived(int byteCount)
    {
        Interlocked.Increment(ref _packetsReceived);
        Interlocked.Add(ref _bytesReceived, byteCount);
    }

    public void RecordPacketSent(int byteCount)
    {
        Interlocked.Increment(ref _packetsSent);
        Interlocked.Add(ref _bytesSent, byteCount);
    }

    public void RecordSendDropped() => Interlocked.Increment(ref _packetsSendDropped);

    public void RecordReconnectAttempt() => Interlocked.Increment(ref _reconnectAttempts);

    public void RecordQueuedPayloadDropped() => Interlocked.Increment(ref _queuedPayloadsDropped);

    public void RecordRtt(TimeSpan rtt)
    {
        lock (_aggregatorSync)
        {
            _rtt.Record(rtt);
        }
    }

    public void ResetRateBaseline()
    {
        lock (_aggregatorSync)
        {
            _rate.Reset();
        }
    }

    public NetSessionStatisticsSnapshot Snapshot(
        int connectedPeerCount,
        NetTransportGuardCounts transportGuards,
        DateTimeOffset capturedAtUtc)
    {
        var nowUtc = capturedAtUtc.ToUniversalTime();
        var packetsReceived = Interlocked.Read(ref _packetsReceived);
        var packetsSent = Interlocked.Read(ref _packetsSent);
        var bytesReceived = Interlocked.Read(ref _bytesReceived);
        var bytesSent = Interlocked.Read(ref _bytesSent);
        NetSessionRateSnapshot rate;
        NetSessionRttSummary rtt;
        lock (_aggregatorSync)
        {
            rate = _rate.Sample(
                nowUtc,
                packetsReceived,
                packetsSent,
                bytesReceived,
                bytesSent);
            rtt = _rtt.BuildSummary();
        }

        return new NetSessionStatisticsSnapshot(
            ConnectedPeerCount: connectedPeerCount,
            PeersAcceptedTotal: Interlocked.Read(ref _peersAccepted),
            PeersDisconnectedTotal: Interlocked.Read(ref _peersDisconnected),
            PacketsReceived: packetsReceived,
            PacketsSent: packetsSent,
            PacketsSendDropped: Interlocked.Read(ref _packetsSendDropped),
            BytesReceived: bytesReceived,
            BytesSent: bytesSent,
            ReconnectAttempts: Volatile.Read(ref _reconnectAttempts),
            QueuedPayloadsDropped: Interlocked.Read(ref _queuedPayloadsDropped),
            TransportGuards: transportGuards,
            Rtt: rtt,
            Rate: rate,
            ObservedAtUtc: nowUtc);
    }
}
