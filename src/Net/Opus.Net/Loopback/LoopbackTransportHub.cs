using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using Opus.Net.Transport;

namespace Opus.Net.Loopback;

/// <summary>
/// In-process multi-peer <see cref="INetTransport"/>. The server side of an N:1 loopback
/// link — one hub accepts arbitrarily many client-side <see cref="LoopbackTransport"/>
/// instances, each on its own <see cref="ConnectionId"/>. The 1:1 sibling
/// <see cref="LoopbackTransportPair"/> ships a single fixed link; the hub is for any test
/// or single-process dev run where the server must talk to several clients at once (the
/// closed-alpha multiplayer build's 5v5 / 10v10 / 20-player modes all need this shape).
/// </summary>
/// <remarks>
/// <para>
/// Two queues per peer: the hub's own inbox is shared across every accepted peer
/// (one server thread drains everything via <see cref="Poll"/>); each peer owns its own
/// per-peer client-inbox so server → peer-X sends never disturb peer-Y. The client side
/// is a stock <see cref="LoopbackTransport"/> instance — the hub doesn't need a custom
/// per-peer wrapper, the existing class already addresses exactly one peer (here, the
/// hub) and routes through caller-supplied inboxes.
/// </para>
/// <para>
/// Threading: <see cref="Send"/> + <see cref="Disconnect"/> are safe from any thread;
/// <see cref="Poll"/> drains on the calling thread, expected from the game tick. Peer
/// connect/disconnect bookkeeping is serialised via the internal lock — the dictionary
/// is touched on both <see cref="Accept"/> (potentially off the game thread, e.g.
/// an accept loop in a future UDP transport's analogue) and <see cref="Poll"/> (game
/// thread, when a Disconnected event mutates the slot).
/// </para>
/// </remarks>
public sealed class LoopbackTransportHub : INetTransport
{
    private const string HubName = "loopback-hub";

    /// <summary>The single <see cref="ConnectionId"/> every accepted client uses to
    /// address the hub on its own <see cref="INetTransport.Send"/>. The hub is the only
    /// peer any one client sees, so a sentinel is enough — there's no per-server
    /// disambiguation to make.</summary>
    public static readonly ConnectionId HubSentinelId = new(ulong.MaxValue);

    private readonly ConcurrentQueue<NetEvent> _hubInbox = new();
    private readonly Dictionary<ConnectionId, PeerSlot> _peers = new();
    private readonly object _peersLock = new();
    private ulong _nextPeerCounter = 1;
    private int _disposed;

    private LoopbackTransportHub()
    {
    }

    public string Name => HubName;

    public bool IsOpen => Volatile.Read(ref _disposed) == 0;

    /// <summary>Constructs an empty hub. Accept clients via <see cref="Accept"/>.</summary>
    public static LoopbackTransportHub Create() => new();

    /// <summary>Accepts a new peer. Allocates a fresh <see cref="ConnectionId"/>, creates
    /// a per-peer <see cref="LoopbackTransport"/> for the client side, and queues
    /// Connected events on both sides so the first poll on each surfaces the
    /// connection — same contract as <see cref="LoopbackTransportPair.Create"/>.</summary>
    public LoopbackTransportHubConnection Accept(string clientName = "loopback-hub-client")
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) == 1, this);

        ConnectionId peerId;
        ConcurrentQueue<NetEvent> clientInbox;
        lock (_peersLock)
        {
            peerId = new ConnectionId(_nextPeerCounter++);
            clientInbox = new ConcurrentQueue<NetEvent>();
            _peers[peerId] = new PeerSlot(clientInbox);
        }

        var clientTransport = new LoopbackTransport(
            name: clientName,
            peerId: HubSentinelId,
            selfIdAsSeenByPeer: peerId,
            ownInbox: clientInbox,
            peerInbox: _hubInbox);

        clientInbox.Enqueue(NetEvent.Connected(HubSentinelId));
        _hubInbox.Enqueue(NetEvent.Connected(peerId));

        return new LoopbackTransportHubConnection(clientTransport, peerId);
    }

    public bool Send(ConnectionId target, ReadOnlySpan<byte> payload)
    {
        if (!IsOpen)
        {
            return false;
        }

        PeerSlot? slot;
        lock (_peersLock)
        {
            if (!_peers.TryGetValue(target, out slot) || !slot.IsConnected)
            {
                return false;
            }
        }

        var copy = payload.ToArray();
        slot.ClientInbox.Enqueue(NetEvent.Received(HubSentinelId, copy));
        return true;
    }

    public void Poll(List<NetEvent> into)
    {
        ArgumentNullException.ThrowIfNull(into);
        into.Clear();

        while (_hubInbox.TryDequeue(out var ev))
        {
            into.Add(ev);
            if (ev.Kind == NetEventKind.Disconnected)
            {
                lock (_peersLock)
                {
                    if (_peers.TryGetValue(ev.Connection, out var slot))
                    {
                        slot.IsConnected = false;
                    }
                }
            }
        }
    }

    public void Disconnect(ConnectionId target)
    {
        PeerSlot? slot;
        lock (_peersLock)
        {
            if (!_peers.TryGetValue(target, out slot) || !slot.IsConnected)
            {
                return;
            }

            slot.IsConnected = false;
        }

        // Bilateral notification — peer sees Disconnected addressed by the hub's sentinel;
        // hub side surfaces the dropped peer's id so consumer code can clean up its
        // per-peer state symmetrically with the real-transport path.
        slot.ClientInbox.Enqueue(NetEvent.Disconnected(HubSentinelId));
        _hubInbox.Enqueue(NetEvent.Disconnected(target));
    }

    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 1)
        {
            return;
        }

        ConnectionId[] livePeers;
        lock (_peersLock)
        {
            var live = new List<ConnectionId>(_peers.Count);
            foreach (var (id, slot) in _peers)
            {
                if (slot.IsConnected)
                {
                    live.Add(id);
                }
            }

            livePeers = live.ToArray();
        }

        foreach (var peerId in livePeers)
        {
            Disconnect(peerId);
        }
    }

    private sealed class PeerSlot
    {
        public PeerSlot(ConcurrentQueue<NetEvent> clientInbox)
        {
            ClientInbox = clientInbox;
            IsConnected = true;
        }

        public ConcurrentQueue<NetEvent> ClientInbox { get; }

        public bool IsConnected { get; set; }
    }
}
