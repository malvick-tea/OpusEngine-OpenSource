using System;

namespace Opus.Net.Transport;

/// <summary>
/// One transport-level notification: a peer connecting, a peer dropping, or a datagram
/// arriving. Pumped off <see cref="INetTransport.Poll"/> in receive order; the consumer
/// owns the payload buffer once <see cref="NetEvent"/> is observed (the transport will
/// not reuse it).
/// </summary>
/// <remarks>
/// Modeled as a single struct + discriminator instead of an abstract class hierarchy so
/// the poll loop can drain into a contiguous array without per-event allocation. The
/// <see cref="Payload"/> array is shared with the transport's send queue; transports
/// MUST copy on send so the consumer can hold or mutate the array without races.
/// </remarks>
public readonly record struct NetEvent
{
    private NetEvent(NetEventKind kind, ConnectionId connection, byte[] payload)
    {
        Kind = kind;
        Connection = connection;
        Payload = payload;
    }

    public NetEventKind Kind { get; }

    public ConnectionId Connection { get; }

    /// <summary>The datagram bytes for <see cref="NetEventKind.Received"/>; an empty
    /// array for the connection-state events.</summary>
    public byte[] Payload { get; }

    public static NetEvent Connected(ConnectionId connection) =>
        new(NetEventKind.Connected, connection, Array.Empty<byte>());

    public static NetEvent Disconnected(ConnectionId connection) =>
        new(NetEventKind.Disconnected, connection, Array.Empty<byte>());

    public static NetEvent Received(ConnectionId connection, byte[] payload) =>
        new(NetEventKind.Received, connection, payload ?? Array.Empty<byte>());
}
