using System.Collections.Generic;
using FluentAssertions;
using Opus.Net.Loopback;
using Opus.Net.Transport;
using Xunit;

namespace Opus.Net.Tests.Loopback;

/// <summary>
/// Covers <see cref="LoopbackTransportPair"/> + <see cref="LoopbackTransport"/> end-to-end:
/// the contract surfaces (open / send / poll / disconnect / dispose) over an in-process
/// link. These tests pin the same observable behaviour a real UDP transport will need
/// to satisfy, so the higher layers (session, room) can be authored against the
/// interface without depending on the loopback impl.
/// </summary>
public sealed class LoopbackTransportTests
{
    private static readonly byte[] HelloBytes = { 0x48, 0x65, 0x6C, 0x6C, 0x6F };
    private static readonly byte[] WorldBytes = { 0x57, 0x6F, 0x72, 0x6C, 0x64 };

    [Fact]
    public void Pair_starts_with_both_sides_open()
    {
        var link = LoopbackTransportPair.Create();

        link.Client.IsOpen.Should().BeTrue();
        link.Server.IsOpen.Should().BeTrue();
        link.ClientPeerId.IsValid.Should().BeTrue();
        link.ServerPeerId.IsValid.Should().BeTrue();
        link.ClientPeerId.Should().NotBe(link.ServerPeerId);
    }

    [Fact]
    public void First_poll_on_each_side_surfaces_Connected_for_its_peer()
    {
        var link = LoopbackTransportPair.Create();
        var clientEvents = new List<NetEvent>();
        var serverEvents = new List<NetEvent>();

        link.Client.Poll(clientEvents);
        link.Server.Poll(serverEvents);

        clientEvents.Should().HaveCount(1);
        clientEvents[0].Kind.Should().Be(NetEventKind.Connected);
        clientEvents[0].Connection.Should().Be(link.ServerPeerId);

        serverEvents.Should().HaveCount(1);
        serverEvents[0].Kind.Should().Be(NetEventKind.Connected);
        serverEvents[0].Connection.Should().Be(link.ClientPeerId);
    }

    [Fact]
    public void Client_send_appears_on_server_poll_with_matching_payload()
    {
        var link = LoopbackTransportPair.Create();
        DrainConnectedEvents(link);

        var sent = link.Client.Send(link.ServerPeerId, HelloBytes);

        sent.Should().BeTrue();
        var events = new List<NetEvent>();
        link.Server.Poll(events);
        events.Should().HaveCount(1);
        events[0].Kind.Should().Be(NetEventKind.Received);
        events[0].Connection.Should().Be(link.ClientPeerId);
        events[0].Payload.Should().Equal(HelloBytes);
    }

    [Fact]
    public void Send_preserves_send_order_into_poll_order()
    {
        var link = LoopbackTransportPair.Create();
        DrainConnectedEvents(link);

        link.Client.Send(link.ServerPeerId, HelloBytes);
        link.Client.Send(link.ServerPeerId, WorldBytes);

        var events = new List<NetEvent>();
        link.Server.Poll(events);
        events.Should().HaveCount(2);
        events[0].Payload.Should().Equal(HelloBytes);
        events[1].Payload.Should().Equal(WorldBytes);
    }

    [Fact]
    public void Send_copies_the_buffer_so_caller_can_mutate_post_send()
    {
        var link = LoopbackTransportPair.Create();
        DrainConnectedEvents(link);

        var buffer = new byte[] { 1, 2, 3, 4 };
        link.Client.Send(link.ServerPeerId, buffer);
        buffer[0] = 0xFF;
        buffer[3] = 0xFF;

        var events = new List<NetEvent>();
        link.Server.Poll(events);
        events[0].Payload.Should().Equal((byte)1, (byte)2, (byte)3, (byte)4);
    }

    [Fact]
    public void Send_to_an_unknown_target_returns_false_and_queues_nothing()
    {
        var link = LoopbackTransportPair.Create();
        DrainConnectedEvents(link);
        var stranger = new ConnectionId(99UL);

        var sent = link.Client.Send(stranger, HelloBytes);

        sent.Should().BeFalse();
        var events = new List<NetEvent>();
        link.Server.Poll(events);
        events.Should().BeEmpty();
    }

    [Fact]
    public void Disconnect_surfaces_Disconnected_on_both_sides_and_closes_both_transports()
    {
        var link = LoopbackTransportPair.Create();
        DrainConnectedEvents(link);

        link.Client.Disconnect(link.ServerPeerId);

        var clientEvents = new List<NetEvent>();
        var serverEvents = new List<NetEvent>();
        link.Client.Poll(clientEvents);
        link.Server.Poll(serverEvents);

        clientEvents.Should().ContainSingle(e => e.Kind == NetEventKind.Disconnected);
        serverEvents.Should().ContainSingle(e => e.Kind == NetEventKind.Disconnected);
        link.Client.IsOpen.Should().BeFalse();
        link.Server.IsOpen.Should().BeFalse();
    }

    [Fact]
    public void Disconnect_is_idempotent()
    {
        var link = LoopbackTransportPair.Create();
        DrainConnectedEvents(link);

        link.Client.Disconnect(link.ServerPeerId);
        link.Client.Disconnect(link.ServerPeerId);

        var events = new List<NetEvent>();
        link.Client.Poll(events);
        events.Count(e => e.Kind == NetEventKind.Disconnected).Should().Be(1);
    }

    [Fact]
    public void Send_after_disconnect_returns_false()
    {
        var link = LoopbackTransportPair.Create();
        DrainConnectedEvents(link);
        link.Client.Disconnect(link.ServerPeerId);
        DrainPoll(link);

        link.Client.Send(link.ServerPeerId, HelloBytes).Should().BeFalse();
    }

    [Fact]
    public void Dispose_disconnects_a_still_open_transport()
    {
        var link = LoopbackTransportPair.Create();
        DrainConnectedEvents(link);

        link.Client.Dispose();

        var serverEvents = new List<NetEvent>();
        link.Server.Poll(serverEvents);
        serverEvents.Should().ContainSingle(e => e.Kind == NetEventKind.Disconnected);
        link.Server.IsOpen.Should().BeFalse();
    }

    [Fact]
    public void Poll_throws_on_a_null_buffer()
    {
        var link = LoopbackTransportPair.Create();

        var act = () => link.Client.Poll(null!);

        act.Should().Throw<System.ArgumentNullException>();
    }

    [Fact]
    public void Poll_clears_the_supplied_list_before_draining()
    {
        var link = LoopbackTransportPair.Create();
        var events = new List<NetEvent> { NetEvent.Connected(new ConnectionId(123UL)) };

        link.Client.Poll(events);

        // The first poll should produce exactly Connected for the peer, and the stale
        // sentinel must be gone — Poll-into-scratch is the expected hot-path pattern.
        events.Should().HaveCount(1);
        events[0].Connection.Should().Be(link.ServerPeerId);
    }

    private static void DrainConnectedEvents(LoopbackTransportLink link)
    {
        var buffer = new List<NetEvent>();
        link.Client.Poll(buffer);
        link.Server.Poll(buffer);
    }

    private static void DrainPoll(LoopbackTransportLink link)
    {
        var buffer = new List<NetEvent>();
        link.Client.Poll(buffer);
        link.Server.Poll(buffer);
    }
}
