using System;
using System.Collections.Generic;
using System.Text;
using FluentAssertions;
using Opus.Net.Loopback;
using Opus.Net.Transport;
using Xunit;

namespace Opus.Net.Tests.Loopback;

/// <summary>Coverage for the multi-peer hub variant of the loopback transport.
/// <see cref="LoopbackTransportPair"/> is already covered by <see cref="LoopbackTransportTests"/>;
/// the tests here pin the **N:1** contract that the closed-alpha multiplayer build
/// needs — multiple clients addressed by distinct ids, server-side fan-out per peer,
/// per-peer disconnect that doesn't take other peers with it.</summary>
public sealed class LoopbackTransportHubTests
{
    [Fact]
    public void Hub_starts_open_with_no_peers_and_an_empty_poll_queue()
    {
        using var hub = LoopbackTransportHub.Create();
        var scratch = new List<NetEvent>();

        hub.Poll(scratch);

        hub.IsOpen.Should().BeTrue();
        scratch.Should().BeEmpty();
    }

    [Fact]
    public void Accepting_a_peer_surfaces_connected_on_both_sides_after_first_poll()
    {
        using var hub = LoopbackTransportHub.Create();
        var connection = hub.Accept();

        var hubEvents = Drain(hub);
        var clientEvents = Drain(connection.Client);

        hubEvents.Should().ContainSingle()
            .Which.Should().Be(NetEvent.Connected(connection.ServerSidePeerId));
        clientEvents.Should().ContainSingle()
            .Which.Should().Be(NetEvent.Connected(LoopbackTransportHub.HubSentinelId));
    }

    [Fact]
    public void Accept_assigns_distinct_connection_ids_per_peer()
    {
        using var hub = LoopbackTransportHub.Create();

        var a = hub.Accept();
        var b = hub.Accept();
        var c = hub.Accept();

        var ids = new[] { a.ServerSidePeerId, b.ServerSidePeerId, c.ServerSidePeerId };
        ids.Should().OnlyHaveUniqueItems();
        ids.Should().AllSatisfy(id => id.IsValid.Should().BeTrue());
    }

    [Fact]
    public void Send_from_hub_to_peer_x_reaches_x_and_not_y()
    {
        using var hub = LoopbackTransportHub.Create();
        var a = hub.Accept();
        var b = hub.Accept();
        FlushAll(hub, a.Client, b.Client); // flush the initial Connected events.

        var payload = Encoding.ASCII.GetBytes("hello-a");
        hub.Send(a.ServerSidePeerId, payload).Should().BeTrue();

        Drain(a.Client).Should().ContainSingle()
            .Which.Should().Match<NetEvent>(e => e.Kind == NetEventKind.Received);
        Drain(b.Client).Should().BeEmpty("peer B did not receive what peer A was sent");
    }

    [Fact]
    public void Send_from_peer_to_hub_surfaces_received_with_peers_id()
    {
        using var hub = LoopbackTransportHub.Create();
        var a = hub.Accept();
        FlushAll(hub, a.Client);

        var payload = Encoding.ASCII.GetBytes("from-a");
        a.Client.Send(LoopbackTransportHub.HubSentinelId, payload).Should().BeTrue();

        var hubEvents = Drain(hub);
        hubEvents.Should().ContainSingle();
        hubEvents[0].Kind.Should().Be(NetEventKind.Received);
        hubEvents[0].Connection.Should().Be(a.ServerSidePeerId);
        hubEvents[0].Payload.Should().Equal(payload);
    }

    [Fact]
    public void Send_to_an_unknown_connection_id_returns_false()
    {
        using var hub = LoopbackTransportHub.Create();

        var stranger = new ConnectionId(9999UL);

        hub.Send(stranger, new byte[] { 1, 2, 3 }).Should().BeFalse();
    }

    [Fact]
    public void Hub_disconnect_propagates_to_the_targeted_peer_only()
    {
        using var hub = LoopbackTransportHub.Create();
        var a = hub.Accept();
        var b = hub.Accept();
        FlushAll(hub, a.Client, b.Client);

        hub.Disconnect(a.ServerSidePeerId);

        var hubEvents = Drain(hub);
        hubEvents.Should().ContainSingle()
            .Which.Should().Be(NetEvent.Disconnected(a.ServerSidePeerId));
        Drain(a.Client).Should().ContainSingle()
            .Which.Should().Be(NetEvent.Disconnected(LoopbackTransportHub.HubSentinelId));
        Drain(b.Client).Should().BeEmpty("peer B's link must survive peer A's disconnect");

        hub.Send(a.ServerSidePeerId, new byte[] { 9 }).Should().BeFalse();
        hub.Send(b.ServerSidePeerId, new byte[] { 7 }).Should().BeTrue();
    }

    [Fact]
    public void Client_initiated_disconnect_surfaces_on_the_hub_and_blocks_subsequent_sends()
    {
        using var hub = LoopbackTransportHub.Create();
        var a = hub.Accept();
        FlushAll(hub, a.Client);

        a.Client.Disconnect(LoopbackTransportHub.HubSentinelId);

        var hubEvents = Drain(hub);
        hubEvents.Should().ContainSingle()
            .Which.Should().Be(NetEvent.Disconnected(a.ServerSidePeerId));

        hub.Send(a.ServerSidePeerId, new byte[] { 1 }).Should().BeFalse(
            "the hub's Poll dispatch flips the slot's IsConnected on a Disconnected event");
    }

    [Fact]
    public void Hub_disconnect_is_idempotent()
    {
        using var hub = LoopbackTransportHub.Create();
        var a = hub.Accept();
        FlushAll(hub, a.Client);

        hub.Disconnect(a.ServerSidePeerId);
        FlushAll(hub, a.Client);
        hub.Disconnect(a.ServerSidePeerId);

        Drain(hub).Should().BeEmpty();
        Drain(a.Client).Should().BeEmpty();
    }

    [Fact]
    public void Dispose_disconnects_every_live_peer()
    {
        var hub = LoopbackTransportHub.Create();
        var a = hub.Accept();
        var b = hub.Accept();
        var c = hub.Accept();
        FlushAll(hub, a.Client, b.Client, c.Client);

        hub.Dispose();

        Drain(a.Client).Should().ContainSingle().Which.Kind.Should().Be(NetEventKind.Disconnected);
        Drain(b.Client).Should().ContainSingle().Which.Kind.Should().Be(NetEventKind.Disconnected);
        Drain(c.Client).Should().ContainSingle().Which.Kind.Should().Be(NetEventKind.Disconnected);
        hub.IsOpen.Should().BeFalse();
    }

    [Fact]
    public void Accept_after_dispose_throws_object_disposed_exception()
    {
        var hub = LoopbackTransportHub.Create();
        hub.Dispose();

        var act = () => hub.Accept();
        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void Receive_order_preserved_across_multiple_peers()
    {
        using var hub = LoopbackTransportHub.Create();
        var a = hub.Accept();
        var b = hub.Accept();
        FlushAll(hub, a.Client, b.Client);

        a.Client.Send(LoopbackTransportHub.HubSentinelId, new byte[] { 1 });
        b.Client.Send(LoopbackTransportHub.HubSentinelId, new byte[] { 2 });
        a.Client.Send(LoopbackTransportHub.HubSentinelId, new byte[] { 3 });

        var hubEvents = Drain(hub);
        hubEvents.Should().HaveCount(3);
        hubEvents[0].Connection.Should().Be(a.ServerSidePeerId);
        hubEvents[1].Connection.Should().Be(b.ServerSidePeerId);
        hubEvents[2].Connection.Should().Be(a.ServerSidePeerId);
    }

    [Fact]
    public void Send_buffer_is_copied_so_caller_may_mutate_immediately()
    {
        using var hub = LoopbackTransportHub.Create();
        var a = hub.Accept();
        FlushAll(hub, a.Client);

        var buffer = new byte[] { 1, 2, 3 };
        hub.Send(a.ServerSidePeerId, buffer).Should().BeTrue();
        buffer[0] = 99;

        var clientEvents = Drain(a.Client);
        clientEvents.Should().ContainSingle();
        clientEvents[0].Payload.Should().Equal(new byte[] { 1, 2, 3 });
    }

    private static List<NetEvent> Drain(INetTransport transport)
    {
        var scratch = new List<NetEvent>();
        transport.Poll(scratch);
        return scratch;
    }

    private static void FlushAll(params INetTransport[] transports)
    {
        var scratch = new List<NetEvent>();
        foreach (var t in transports)
        {
            t.Poll(scratch);
        }
    }
}
