using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using Opus.Net.Transport;

namespace Opus.Net.Loopback;

/// <summary>
/// In-process <see cref="INetTransport"/> for a single peer-to-peer link. Each instance
/// pairs with exactly one counterpart created together via
/// <see cref="LoopbackTransportPair.Create"/>: a <see cref="Send"/> on one side enqueues
/// onto the other side's poll queue, and a <see cref="Disconnect"/> notifies both ends.
/// </summary>
/// <remarks>
/// <para>
/// Used for unit tests of higher-level protocol code and for single-process dev runs
/// where the "server" and "client" live in the same process. The real wire transport is
/// a sibling assembly (UDP / ENet) implementing the same interface — game-layer code
/// must compile against <see cref="INetTransport"/>, never reference this class.
/// </para>
/// <para>
/// Thread-safety: <c>Send</c> can be called from any thread (the inbox is a
/// <see cref="ConcurrentQueue{T}"/>). <c>Poll</c> drains on the calling thread and is
/// expected to run from the game tick. <c>Disconnect</c> is idempotent. <c>Dispose</c>
/// disconnects if still live.
/// </para>
/// </remarks>
internal sealed class LoopbackTransport : INetTransport
{
    private readonly ConcurrentQueue<NetEvent> _ownInbox;
    private readonly ConcurrentQueue<NetEvent> _peerInbox;
    private readonly ConnectionId _peerId;
    private readonly ConnectionId _selfIdAsSeenByPeer;

    private int _connected;
    private int _disposed;

    internal LoopbackTransport(
        string name,
        ConnectionId peerId,
        ConnectionId selfIdAsSeenByPeer,
        ConcurrentQueue<NetEvent> ownInbox,
        ConcurrentQueue<NetEvent> peerInbox)
    {
        Name = name;
        _peerId = peerId;
        _selfIdAsSeenByPeer = selfIdAsSeenByPeer;
        _ownInbox = ownInbox;
        _peerInbox = peerInbox;
        _connected = 1;
    }

    public string Name { get; }

    public bool IsOpen =>
        System.Threading.Volatile.Read(ref _disposed) == 0 &&
        System.Threading.Volatile.Read(ref _connected) == 1;

    public bool Send(ConnectionId target, ReadOnlySpan<byte> payload)
    {
        if (!IsOpen || target != _peerId)
        {
            return false;
        }

        // Copy before enqueue — the caller is free to mutate or reuse its buffer the
        // instant Send returns, exactly like a real datagram transport.
        var copy = payload.ToArray();
        _peerInbox.Enqueue(NetEvent.Received(_selfIdAsSeenByPeer, copy));
        return true;
    }

    public void Poll(List<NetEvent> into)
    {
        ArgumentNullException.ThrowIfNull(into);

        into.Clear();
        while (_ownInbox.TryDequeue(out var ev))
        {
            into.Add(ev);
            if (ev.Kind == NetEventKind.Disconnected)
            {
                System.Threading.Volatile.Write(ref _connected, 0);
            }
        }
    }

    public void Disconnect(ConnectionId target)
    {
        if (target != _peerId)
        {
            return;
        }

        if (System.Threading.Interlocked.Exchange(ref _connected, 0) == 0)
        {
            return;
        }

        // Notify both sides — the peer surfaces Disconnected with our id, we surface
        // Disconnected with the peer's id, so consumer code on either side reacts
        // uniformly to "the link is gone".
        _peerInbox.Enqueue(NetEvent.Disconnected(_selfIdAsSeenByPeer));
        _ownInbox.Enqueue(NetEvent.Disconnected(_peerId));
    }

    public void Dispose()
    {
        if (System.Threading.Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

        Disconnect(_peerId);
    }
}
