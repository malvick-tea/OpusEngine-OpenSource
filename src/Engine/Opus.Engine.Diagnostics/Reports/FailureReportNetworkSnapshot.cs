using System;

namespace Opus.Engine.Diagnostics.Reports;

/// <summary>
/// Engine-neutral network-state snapshot embedded in a <see cref="FailureReport"/>.
/// Data-only shape — Diagnostics deliberately does not reference
/// <c>Opus.Engine.Net</c>; the host composition layer flattens its
/// <c>NetSessionTelemetry</c> into this record before constructing the report so the
/// payload stays grep-friendly and stable across consumer projects.
/// </summary>
/// <param name="DisplayName">Stable display name of the session that produced the
/// snapshot.</param>
/// <param name="Role">Session role at capture time (<c>"client"</c> / <c>"server"</c>
/// / other label chosen by the host).</param>
/// <param name="State">State-machine position at capture time
/// (<c>"idle"</c>, <c>"connecting"</c>, <c>"connected"</c>, <c>"reconnecting"</c>,
/// <c>"faulted"</c>, <c>"disposed"</c>, or other label chosen by the host).</param>
/// <param name="ConnectedPeerCount">Number of peers in the <c>Connected</c> state at
/// the time of capture.</param>
/// <param name="PacketsReceived">Cumulative inbound datagrams.</param>
/// <param name="PacketsSent">Cumulative outbound datagrams accepted by the transport.</param>
/// <param name="PacketsSendDropped">Cumulative outbound datagrams rejected by the
/// transport.</param>
/// <param name="BytesReceived">Cumulative inbound payload bytes.</param>
/// <param name="BytesSent">Cumulative outbound payload bytes accepted by the
/// transport.</param>
/// <param name="ReconnectAttempts">Cumulative reconnect attempts launched. Zero for
/// non-reconnecting roles.</param>
/// <param name="QueuedPayloadsDropped">Cumulative inbound payloads dropped because
/// the receive queue saturated.</param>
/// <param name="RejectedConnections">Cumulative inbound connection requests the
/// transport rejected because the concurrent-peer cap was full (connection-flood
/// guard). Zero for a client session or a transport without guard diagnostics.</param>
/// <param name="DroppedInboundPayloads">Cumulative inbound payloads the transport shed
/// because its shared receive queue was at its cap (queue-memory guard).</param>
/// <param name="RateLimitedInboundPayloads">Cumulative inbound payloads the transport
/// shed because a single peer exceeded its per-peer rate limit (fairness guard).</param>
/// <param name="RttSampleCount">Count of RTT samples retained in the consumer-driven
/// rolling window at capture time.</param>
/// <param name="RttMean">Mean RTT across the retained window.
/// <see cref="TimeSpan.Zero"/> when <see cref="RttSampleCount"/> is zero.</param>
/// <param name="RttP95">Nearest-rank P95 across the retained window.
/// <see cref="TimeSpan.Zero"/> when <see cref="RttSampleCount"/> is zero.</param>
/// <param name="PacketsReceivedPerSecond">Inbound packet rate observed over the latest
/// snapshot window.</param>
/// <param name="PacketsSentPerSecond">Outbound packet rate observed over the latest
/// snapshot window.</param>
/// <param name="BytesReceivedPerSecond">Inbound byte rate observed over the latest
/// snapshot window.</param>
/// <param name="BytesSentPerSecond">Outbound byte rate observed over the latest
/// snapshot window.</param>
/// <param name="RateWindow">Wall-clock duration the rate fields cover.
/// <see cref="TimeSpan.Zero"/> when no baseline existed yet.</param>
/// <param name="LastFaultCode">Short fault-code label from the session's last fault,
/// or null when the session has never faulted.</param>
/// <param name="LastFaultDetail">Free-form detail string from the session's last
/// fault, or null.</param>
public sealed record FailureReportNetworkSnapshot(
    string DisplayName,
    string Role,
    string State,
    int ConnectedPeerCount,
    long PacketsReceived,
    long PacketsSent,
    long PacketsSendDropped,
    long BytesReceived,
    long BytesSent,
    int ReconnectAttempts,
    long QueuedPayloadsDropped,
    long RejectedConnections,
    long DroppedInboundPayloads,
    long RateLimitedInboundPayloads,
    int RttSampleCount,
    TimeSpan RttMean,
    TimeSpan RttP95,
    double PacketsReceivedPerSecond,
    double PacketsSentPerSecond,
    double BytesReceivedPerSecond,
    double BytesSentPerSecond,
    TimeSpan RateWindow,
    string? LastFaultCode,
    string? LastFaultDetail);
