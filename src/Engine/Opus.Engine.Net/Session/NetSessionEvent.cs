using System;
using Opus.Net.Transport;

namespace Opus.Engine.Net.Session;

/// <summary>
/// One observation surfaced by <see cref="NetSession.Tick"/>. Immutable record; the
/// callback that consumes events sees a fresh instance per emission. Payload bytes for
/// <see cref="NetSessionEventKind.PayloadReceived"/> are not carried inline; consumers
/// drain them through <see cref="INetSession.NextReceivedPayload"/> to avoid copying
/// large datagrams into every event.
/// </summary>
public sealed record NetSessionEvent(
    NetSessionEventKind Kind,
    ConnectionId Peer,
    int PayloadByteCount,
    int ConnectedPeerCount,
    DateTimeOffset CapturedAtUtc,
    string? Detail,
    string? DiagnosticCode)
{
    /// <summary>Constructs a session-lifecycle event without a peer.</summary>
    public static NetSessionEvent ForLifecycle(
        NetSessionEventKind kind,
        int connectedPeerCount,
        DateTimeOffset capturedAtUtc,
        string? detail = null,
        string? diagnosticCode = null) =>
        new(kind, ConnectionId.None, 0, connectedPeerCount, capturedAtUtc, detail, diagnosticCode);

    /// <summary>Constructs an event tied to a specific peer.</summary>
    public static NetSessionEvent ForPeer(
        NetSessionEventKind kind,
        ConnectionId peer,
        int connectedPeerCount,
        DateTimeOffset capturedAtUtc,
        int payloadByteCount = 0,
        string? detail = null,
        string? diagnosticCode = null) =>
        new(kind, peer, payloadByteCount, connectedPeerCount, capturedAtUtc, detail, diagnosticCode);
}
