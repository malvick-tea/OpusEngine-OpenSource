using System;

namespace Opus.Engine.Net.Session;

/// <summary>
/// Immutable rolling counters describing the network session's traffic since
/// <see cref="NetSession.Start"/>. Sampled through <see cref="INetSession.Statistics"/>
/// for telemetry and through the soak harness for stress-run summaries.
/// </summary>
/// <param name="ConnectedPeerCount">Live peer count at the time of capture.</param>
/// <param name="PeersAcceptedTotal">Cumulative count of peers that ever entered
/// <c>Connected</c>. Useful for join evidence in logs.</param>
/// <param name="PeersDisconnectedTotal">Cumulative count of peers that ever entered
/// <c>Disconnected</c>. Useful for leave evidence in logs.</param>
/// <param name="PacketsReceived">Cumulative inbound datagrams.</param>
/// <param name="PacketsSent">Cumulative outbound datagrams accepted by the transport.</param>
/// <param name="PacketsSendDropped">Cumulative outbound datagrams the transport refused
/// because the connection was gone or the payload was too large.</param>
/// <param name="BytesReceived">Cumulative inbound payload bytes.</param>
/// <param name="BytesSent">Cumulative outbound payload bytes accepted by the transport.</param>
/// <param name="ReconnectAttempts">Cumulative reconnect attempts launched from this
/// session. Always zero for server sessions.</param>
/// <param name="QueuedPayloadsDropped">Cumulative inbound payloads dropped because the
/// receive queue hit
/// <see cref="NetSessionOptions.MaxQueuedPayloads"/>.</param>
/// <param name="TransportGuards">Cumulative untrusted-input guard counters read from the underlying
/// transport when it implements <c>INetServerTransportDiagnostics</c> (connection-flood rejects,
/// inbound-queue-cap drops, per-peer rate-limit sheds); <see cref="NetTransportGuardCounts.None"/>
/// for a client session or a transport without the capability.</param>
/// <param name="Rtt">Rolling round-trip-time summary populated from consumer-driven
/// <see cref="INetSession.RecordRtt"/> calls. <see cref="NetSessionRttSummary.Empty(int)"/>
/// when the consumer protocol has not fed any sample yet.</param>
/// <param name="Rate">Instantaneous per-second rate observed between the previous and
/// current snapshot. <see cref="NetSessionRateSnapshot.Empty"/> on the first snapshot
/// after a session starts (no baseline to diff against).</param>
/// <param name="ObservedAtUtc">Timestamp the snapshot was sampled at.</param>
public readonly record struct NetSessionStatisticsSnapshot(
    int ConnectedPeerCount,
    long PeersAcceptedTotal,
    long PeersDisconnectedTotal,
    long PacketsReceived,
    long PacketsSent,
    long PacketsSendDropped,
    long BytesReceived,
    long BytesSent,
    int ReconnectAttempts,
    long QueuedPayloadsDropped,
    NetTransportGuardCounts TransportGuards,
    NetSessionRttSummary Rtt,
    NetSessionRateSnapshot Rate,
    DateTimeOffset ObservedAtUtc)
{
    /// <summary>The empty snapshot returned for a session that has never started.</summary>
    public static NetSessionStatisticsSnapshot Empty(DateTimeOffset capturedAtUtc) => new(
        ConnectedPeerCount: 0,
        PeersAcceptedTotal: 0,
        PeersDisconnectedTotal: 0,
        PacketsReceived: 0,
        PacketsSent: 0,
        PacketsSendDropped: 0,
        BytesReceived: 0,
        BytesSent: 0,
        ReconnectAttempts: 0,
        QueuedPayloadsDropped: 0,
        TransportGuards: NetTransportGuardCounts.None,
        Rtt: NetSessionRttSummary.Empty(NetSessionStatistics.DefaultRttWindowCapacity),
        Rate: NetSessionRateSnapshot.Empty,
        ObservedAtUtc: capturedAtUtc.ToUniversalTime());
}
