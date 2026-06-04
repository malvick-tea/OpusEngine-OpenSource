using System;
using System.Collections.Generic;

namespace Opus.Net.Transport;

/// <summary>
/// Lowest layer of the network stack: send a byte buffer to one peer, poll for events
/// (connect / disconnect / receive) coming back. Payload framing, ordering guarantees,
/// retransmit policy, and connection lifecycle are all transport-specific — the
/// abstraction here is intentionally **datagram-level**, not stream-level, because the
/// snapshot channel and the input channel are both fire-and-forget at the game-layer.
/// </summary>
/// <remarks>
/// <para>
/// Two implementations land first: the in-process <c>LoopbackTransport</c> (this phase)
/// for tests and single-process dev, and a real UDP / ENet transport in a later phase.
/// Both must obey the contract below so a `a game server` host can swap transports
/// without touching the room / lobby / match-session code on top.
/// </para>
/// <para>
/// Threading: implementations may receive on a background thread but
/// <see cref="Poll"/> is the only blessed way to observe events from game code. The
/// poll call drains everything queued since the previous poll into the caller-supplied
/// list, on the calling thread, so game-tick code never races with the receive thread.
/// </para>
/// </remarks>
public interface INetTransport : IDisposable
{
    /// <summary>Identifies this side of the wire for logging / metrics. Stable across
    /// reconnects.</summary>
    string Name { get; }

    /// <summary>True once the transport has accepted at least one peer and not yet been
    /// disposed. Single-use transports stay <c>false</c> after their first disconnect.</summary>
    bool IsOpen { get; }

    /// <summary>Sends <paramref name="payload"/> to <paramref name="target"/>. Returns
    /// false when the connection is gone, the transport is disposed, or the payload is
    /// over the transport's per-datagram cap — every other failure throws. The
    /// transport copies the buffer before queueing; the caller may reuse / mutate it
    /// immediately on return.</summary>
    bool Send(ConnectionId target, ReadOnlySpan<byte> payload);

    /// <summary>Drains every event observed since the previous poll into <paramref name="into"/>
    /// in receive order, then returns. The list is cleared before the drain — pass a
    /// pre-allocated scratch list to avoid per-tick allocations.</summary>
    void Poll(List<NetEvent> into);

    /// <summary>Closes the connection cleanly (the peer observes a
    /// <see cref="NetEventKind.Disconnected"/>). No-op when the connection is already
    /// gone. The peer keeps any events already drained out of its poll buffer.</summary>
    void Disconnect(ConnectionId target);
}
