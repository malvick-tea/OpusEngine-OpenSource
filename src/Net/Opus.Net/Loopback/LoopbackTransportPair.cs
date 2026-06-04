using System.Collections.Concurrent;
using Opus.Net.Transport;

namespace Opus.Net.Loopback;

/// <summary>
/// Factory that wires two <see cref="LoopbackTransport"/> instances against a shared
/// mailbox pair so each side sees the other as its peer. Returned tuple is
/// <c>(client, server)</c>; the labels are convention only — the transport is
/// symmetric.
/// </summary>
/// <remarks>
/// <para>
/// Each side starts <see cref="INetTransport.IsOpen"/> = true and observes a
/// <see cref="NetEventKind.Connected"/> on its first poll. The connection ids are
/// assigned deterministically (client = <c>conn#1</c>, server = <c>conn#2</c>) so test
/// assertions stay readable. Multi-peer hosting is a future phase — this pair models a
/// single 1:1 link, which is all the loopback path needs for unit tests of the
/// higher-level session / room / match-mode code.
/// </para>
/// </remarks>
public static class LoopbackTransportPair
{
    private const ulong ClientConnectionId = 1UL;
    private const ulong ServerConnectionId = 2UL;

    public static LoopbackTransportLink Create()
    {
        var clientId = new ConnectionId(ClientConnectionId);
        var serverId = new ConnectionId(ServerConnectionId);
        var clientInbox = new ConcurrentQueue<NetEvent>();
        var serverInbox = new ConcurrentQueue<NetEvent>();

        // The client peer's id (from the server's vantage) is "the client". The server
        // peer's id (from the client's vantage) is "the server". Each side knows the
        // other's id at construction time — loopback never needs a discovery handshake.
        var client = new LoopbackTransport(
            name: "loopback-client",
            peerId: serverId,
            selfIdAsSeenByPeer: clientId,
            ownInbox: clientInbox,
            peerInbox: serverInbox);
        var server = new LoopbackTransport(
            name: "loopback-server",
            peerId: clientId,
            selfIdAsSeenByPeer: serverId,
            ownInbox: serverInbox,
            peerInbox: clientInbox);

        // Surface Connected on the first poll of each side so consumers can wire up
        // session bookkeeping symmetrically with a real transport.
        clientInbox.Enqueue(NetEvent.Connected(serverId));
        serverInbox.Enqueue(NetEvent.Connected(clientId));

        return new LoopbackTransportLink(client, server, clientId, serverId);
    }
}
