using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using FluentAssertions;
using Opus.Net.Transport;
using Opus.Net.Udp.Transport;
using Xunit;

namespace Opus.Net.Udp.Tests.Transport;

[Collection(nameof(UdpTransportIntegrationTests))]
[CollectionDefinition(nameof(UdpTransportIntegrationTests), DisableParallelization = true)]
public sealed class UdpTransportIntegrationTests
{
    [Fact]
    public void Server_binds_to_an_ephemeral_loopback_port()
    {
        using var harness = UdpIntegrationHarness.Start();

        harness.Server.BoundEndpoint.Address.Should().Be(IPAddress.Loopback);
        harness.Server.BoundEndpoint.Port.Should().BeGreaterThan(0);
        harness.Server.IsOpen.Should().BeTrue();
    }

    [Fact]
    public void Client_handshake_surfaces_connected_on_both_sides()
    {
        using var harness = UdpIntegrationHarness.Start();
        var entry = harness.AddClient("client");

        var clientView = harness.WaitForConnected(entry);
        var serverView = harness.WaitForServerToAccept(entry);

        clientView.Should().Be(UdpClientTransport.ServerSentinelId);
        serverView.IsValid.Should().BeTrue();
        serverView.Should().NotBe(UdpClientTransport.ServerSentinelId);
    }

    [Fact]
    public void Client_to_server_payload_round_trips()
    {
        using var harness = UdpIntegrationHarness.Start();
        var entry = harness.AddClient("client");
        harness.WaitForConnected(entry);
        var serverPeerId = harness.WaitForServerToAccept(entry);

        var bytes = Encoding.UTF8.GetBytes("hello-from-client");
        entry.Client.Send(UdpClientTransport.ServerSentinelId, bytes).Should().BeTrue();

        var arrived = harness.WaitFor(() => harness.ServerEvents
            .Any(e => e.Kind == NetEventKind.Received && e.Connection == serverPeerId));
        arrived.Should().BeTrue();

        var received = harness.ServerEvents
            .First(e => e.Kind == NetEventKind.Received && e.Connection == serverPeerId);
        received.Payload.Should().Equal(bytes);
    }

    [Fact]
    public void Server_to_client_payload_round_trips()
    {
        using var harness = UdpIntegrationHarness.Start();
        var entry = harness.AddClient("client");
        harness.WaitForConnected(entry);
        var serverPeerId = harness.WaitForServerToAccept(entry);

        var bytes = Encoding.UTF8.GetBytes("hello-from-server");
        harness.Server.Send(serverPeerId, bytes).Should().BeTrue();

        var arrived = harness.WaitFor(() => entry.Events.Any(e => e.Kind == NetEventKind.Received));
        arrived.Should().BeTrue();

        var received = entry.Events.First(e => e.Kind == NetEventKind.Received);
        received.Connection.Should().Be(UdpClientTransport.ServerSentinelId);
        received.Payload.Should().Equal(bytes);
    }

    [Fact]
    public void Multiple_payloads_arrive_in_order_on_loopback()
    {
        using var harness = UdpIntegrationHarness.Start();
        var entry = harness.AddClient("client");
        harness.WaitForConnected(entry);
        var serverPeerId = harness.WaitForServerToAccept(entry);

        for (var i = 0; i < 6; i++)
        {
            entry.Client.Send(
                UdpClientTransport.ServerSentinelId,
                new[] { (byte)i }).Should().BeTrue();
        }

        var arrived = harness.WaitFor(() => harness.ServerEvents
            .Count(e => e.Kind == NetEventKind.Received && e.Connection == serverPeerId) >= 6);
        arrived.Should().BeTrue();

        var received = harness.ServerEvents
            .Where(e => e.Kind == NetEventKind.Received && e.Connection == serverPeerId)
            .Select(e => e.Payload[0])
            .Take(6)
            .ToArray();
        received.Should().Equal(0, 1, 2, 3, 4, 5);
    }

    [Fact]
    public void Client_disconnect_surfaces_on_both_sides()
    {
        using var harness = UdpIntegrationHarness.Start();
        var entry = harness.AddClient("client");
        harness.WaitForConnected(entry);
        var serverPeerId = harness.WaitForServerToAccept(entry);

        entry.Client.Disconnect(UdpClientTransport.ServerSentinelId);

        var clientSaw = harness.WaitFor(() =>
            entry.Events.Any(e => e.Kind == NetEventKind.Disconnected));
        var serverSaw = harness.WaitFor(() =>
            harness.ServerEvents.Any(e => e.Kind == NetEventKind.Disconnected && e.Connection == serverPeerId));

        clientSaw.Should().BeTrue();
        serverSaw.Should().BeTrue();
        entry.Client.IsOpen.Should().BeFalse();
    }

    [Fact]
    public void Server_initiated_disconnect_surfaces_on_both_sides()
    {
        using var harness = UdpIntegrationHarness.Start();
        var entry = harness.AddClient("client");
        harness.WaitForConnected(entry);
        var serverPeerId = harness.WaitForServerToAccept(entry);

        harness.Server.Disconnect(serverPeerId);

        var serverSaw = harness.WaitFor(() =>
            harness.ServerEvents.Any(e => e.Kind == NetEventKind.Disconnected && e.Connection == serverPeerId));
        var clientSaw = harness.WaitFor(() =>
            entry.Events.Any(e => e.Kind == NetEventKind.Disconnected));

        serverSaw.Should().BeTrue();
        clientSaw.Should().BeTrue();
    }

    [Fact]
    public void Server_send_to_disconnected_peer_returns_false()
    {
        using var harness = UdpIntegrationHarness.Start();
        var entry = harness.AddClient("client");
        harness.WaitForConnected(entry);
        var serverPeerId = harness.WaitForServerToAccept(entry);

        harness.Server.Disconnect(serverPeerId);
        harness.WaitFor(() =>
            harness.ServerEvents.Any(e => e.Kind == NetEventKind.Disconnected));

        harness.Server.Send(serverPeerId, new byte[] { 1, 2, 3 }).Should().BeFalse();
    }

    [Fact]
    public void Server_send_to_unknown_connection_returns_false()
    {
        using var harness = UdpIntegrationHarness.Start();

        harness.Server.Send(new ConnectionId(999999UL), new byte[] { 1 }).Should().BeFalse();
    }

    [Fact]
    public void Client_send_before_connected_returns_false()
    {
        using var harness = UdpIntegrationHarness.Start();
        var entry = harness.AddClient("client");

        entry.Client.Send(UdpClientTransport.ServerSentinelId, new byte[] { 1, 2 }).Should().BeFalse();
    }

    [Fact]
    public void Two_clients_get_distinct_connection_ids()
    {
        using var harness = UdpIntegrationHarness.Start();

        var clientA = harness.AddClient("client-a");
        harness.WaitForConnected(clientA);
        var serverIdA = harness.WaitForServerToAccept(clientA);

        var clientB = harness.AddClient("client-b");
        harness.WaitForConnected(clientB);
        var serverIdB = harness.WaitForServerToAccept(clientB);

        serverIdA.Should().NotBe(serverIdB);
        serverIdA.IsValid.Should().BeTrue();
        serverIdB.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Server_rejects_hello_beyond_the_peer_cap()
    {
        using var harness = UdpIntegrationHarness.Start("server", () => new UdpTransportOptions
        {
            HeartbeatInterval = TimeSpan.FromMilliseconds(100),
            DeadlineDuration = TimeSpan.FromSeconds(5),
            ReceivePollInterval = TimeSpan.FromMilliseconds(50),
            ConnectTimeout = TimeSpan.FromSeconds(2),
            MaxConcurrentPeers = 2,
        });

        var clientA = harness.AddClient("client-a");
        var clientB = harness.AddClient("client-b");
        var clientC = harness.AddClient("client-c");
        int ConnectedClientCount() => new[] { clientA, clientB, clientC }
            .Count(entry => entry.Events.Any(e => e.Kind == NetEventKind.Connected));

        // The server accepts at most two concurrent peers; the third endpoint's Hello is
        // rejected without allocating a slot, so exactly two Connected events ever surface and
        // the reject counter climbs while the third client keeps retrying.
        var capHeld = harness.WaitFor(() =>
            harness.ServerEvents.Count(e => e.Kind == NetEventKind.Connected) == 2
            && harness.Server.RejectedHelloCount > 0
            && ConnectedClientCount() == 2);

        capHeld.Should().BeTrue();
        harness.ServerEvents.Count(e => e.Kind == NetEventKind.Connected).Should().Be(2);
        ConnectedClientCount().Should().Be(2, "the peer cap admits two of the three clients and rejects the third.");
    }

    [Fact]
    public void Bind_rejects_a_non_positive_peer_cap()
    {
        var act = () => UdpServerTransport.Bind(
            "bad-cap",
            new IPEndPoint(IPAddress.Loopback, 0),
            new UdpTransportOptions { MaxConcurrentPeers = 0 });

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Server_broadcast_reaches_only_addressed_client()
    {
        using var harness = UdpIntegrationHarness.Start();
        var clientA = harness.AddClient("client-a");
        harness.WaitForConnected(clientA);
        var serverIdA = harness.WaitForServerToAccept(clientA);

        var clientB = harness.AddClient("client-b");
        harness.WaitForConnected(clientB);
        harness.WaitForServerToAccept(clientB);

        var payload = new byte[] { 7, 7, 7 };
        harness.Server.Send(serverIdA, payload).Should().BeTrue();

        var arrived = harness.WaitFor(() => clientA.Events.Any(e => e.Kind == NetEventKind.Received));
        arrived.Should().BeTrue();
        clientB.Events.Any(e => e.Kind == NetEventKind.Received).Should().BeFalse();
    }

    [Fact]
    public void Server_times_out_clients_that_stop_speaking()
    {
        using var harness = UdpIntegrationHarness.Start("server", () => new UdpTransportOptions
        {
            HeartbeatInterval = TimeSpan.FromMilliseconds(100),
            DeadlineDuration = TimeSpan.FromMilliseconds(700),
            ReceivePollInterval = TimeSpan.FromMilliseconds(50),
            ConnectTimeout = TimeSpan.FromSeconds(2),
        });

        var entry = harness.AddClient("ghost");
        harness.WaitForConnected(entry);
        var serverPeerId = harness.WaitForServerToAccept(entry);

        // Simulate the client vanishing: dispose its socket without sending Disconnect.
        ForceCloseSocket(entry.Client);

        var serverSawDisconnect = harness.WaitFor(
            () => harness.ServerEvents.Any(e =>
                e.Kind == NetEventKind.Disconnected && e.Connection == serverPeerId),
            timeout: TimeSpan.FromSeconds(4));

        serverSawDisconnect.Should().BeTrue();
        harness.Server.Send(serverPeerId, new byte[] { 9 }).Should().BeFalse();
    }

    [Fact]
    public void Client_times_out_when_server_goes_silent()
    {
        var fastOptions = new UdpTransportOptions
        {
            HeartbeatInterval = TimeSpan.FromMilliseconds(100),
            DeadlineDuration = TimeSpan.FromMilliseconds(700),
            ReceivePollInterval = TimeSpan.FromMilliseconds(50),
            ConnectTimeout = TimeSpan.FromSeconds(2),
        };
        using var harness = UdpIntegrationHarness.Start("server", () => fastOptions);

        var entry = harness.AddClient("client");
        harness.WaitForConnected(entry);
        harness.WaitForServerToAccept(entry);

        // Simulate the server vanishing: close its socket cold.
        ForceCloseSocket(harness.Server);

        var clientSaw = harness.WaitFor(
            () => entry.Events.Any(e => e.Kind == NetEventKind.Disconnected),
            timeout: TimeSpan.FromSeconds(4));

        clientSaw.Should().BeTrue();
    }

    [Fact]
    public void Client_dispose_disconnects_idempotently()
    {
        using var harness = UdpIntegrationHarness.Start();
        var entry = harness.AddClient("client");
        harness.WaitForConnected(entry);

        entry.Client.Dispose();
        entry.Client.Dispose();
        entry.Client.IsOpen.Should().BeFalse();

        harness.Drain();
        entry.Events.Count(e => e.Kind == NetEventKind.Disconnected).Should().Be(1);
    }

    [Fact]
    public void Server_sheds_inbound_payloads_past_the_inbox_cap()
    {
        const int inboxCap = 8;
        using var harness = UdpIntegrationHarness.Start("server", () => new UdpTransportOptions
        {
            HeartbeatInterval = TimeSpan.FromMilliseconds(100),
            DeadlineDuration = TimeSpan.FromSeconds(5),
            ReceivePollInterval = TimeSpan.FromMilliseconds(50),
            ConnectTimeout = TimeSpan.FromSeconds(2),
            MaxInboundQueuedEvents = inboxCap,
        });

        var entry = harness.AddClient("flooder");
        harness.WaitForConnected(entry);
        harness.WaitForServerToAccept(entry);

        // Flood far faster than anyone drains Poll. The bounded inbox must shed the surplus (UDP is
        // lossy, so a dropped payload is in-protocol) instead of growing the queue without bound.
        for (var i = 0; i < 400; i++)
        {
            entry.Client.Send(UdpClientTransport.ServerSentinelId, new[] { (byte)i });
        }

        // Watch the shed counter climb WITHOUT draining the server inbox — draining would free queue
        // space and stop the cap from ever being reached.
        var deadline = Environment.TickCount64 + 3000;
        while (Environment.TickCount64 < deadline && harness.Server.DroppedInboundPayloadCount == 0)
        {
            Thread.Sleep(20);
        }

        harness.Server.DroppedInboundPayloadCount.Should().BeGreaterThan(
            0, "a payload flood past the inbox cap must be shed, not queued without bound");

        var drained = new List<NetEvent>();
        harness.Server.Poll(drained);
        drained.Count(e => e.Kind == NetEventKind.Received).Should().BeLessThanOrEqualTo(
            inboxCap, "the bounded inbox never holds more than the configured cap of payload events");
        harness.Server.IsOpen.Should().BeTrue("shedding a flood must not tear the server down");
    }

    [Fact]
    public void Bind_rejects_a_non_positive_inbox_cap()
    {
        var act = () => UdpServerTransport.Bind(
            "bad-inbox",
            new IPEndPoint(IPAddress.Loopback, 0),
            new UdpTransportOptions { MaxInboundQueuedEvents = 0 });

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Client_ctor_rejects_a_non_positive_inbox_cap()
    {
        var act = () => new UdpClientTransport(
            "bad-inbox",
            new IPEndPoint(IPAddress.Loopback, 9),
            new UdpTransportOptions { MaxInboundQueuedEvents = 0 });

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Server_rate_limits_a_single_peer_payload_flood()
    {
        const int burst = 4;
        const int floodCount = 100;
        using var harness = UdpIntegrationHarness.Start("server", () => new UdpTransportOptions
        {
            HeartbeatInterval = TimeSpan.FromMilliseconds(100),
            DeadlineDuration = TimeSpan.FromSeconds(5),
            ReceivePollInterval = TimeSpan.FromMilliseconds(50),
            ConnectTimeout = TimeSpan.FromSeconds(2),
            // Generous global inbox cap so the PER-PEER rate limiter is the only thing that can shed
            // here — this is what lets the test attribute the shedding to fairness, not to memory.
            MaxInboundQueuedEvents = 1024,
            MaxInboundPayloadBurstPerPeer = burst,
            InboundPayloadRefillPerSecondPerPeer = 8,
        });

        var entry = harness.AddClient("flooder");
        harness.WaitForConnected(entry);
        var serverPeerId = harness.WaitForServerToAccept(entry);

        // Flood far above the peer's burst + sustained rate inside a sub-second window. The bucket
        // admits the burst (and the trickle refilled while the flood arrives), then sheds the rest.
        for (var i = 0; i < floodCount; i++)
        {
            entry.Client.Send(UdpClientTransport.ServerSentinelId, new[] { (byte)i });
        }

        var shed = harness.WaitFor(() => harness.Server.RateLimitedInboundPayloadCount > 0);

        shed.Should().BeTrue("a flood far above the per-peer burst and sustained rate must be rate limited");
        harness.Server.DroppedInboundPayloadCount.Should().Be(
            0, "the generous inbox cap never fills, so the shedding is per-peer rate limiting, not the queue cap");

        var delivered = harness.ServerEvents.Count(
            e => e.Kind == NetEventKind.Received && e.Connection == serverPeerId);
        delivered.Should().BeGreaterThan(0, "the bucket starts full, so the opening burst is admitted");
        delivered.Should().BeLessThan(
            floodCount, "the per-peer rate limiter bounds a flood far below the payloads sent");
        harness.Server.IsOpen.Should().BeTrue("rate limiting a peer must not tear the server down");
    }

    [Fact]
    public void Server_transport_surfaces_its_guard_counters_through_the_diagnostics_capability()
    {
        using var harness = UdpIntegrationHarness.Start();

        harness.Server.Should().BeAssignableTo<INetServerTransportDiagnostics>(
            "the engine telemetry layer reads the DoS-guard counters through this capability");
        var diagnostics = (INetServerTransportDiagnostics)harness.Server;

        // The capability re-exposes the same live counters the concrete transport publishes; the
        // connection-reject member maps onto the UDP-specific RejectedHelloCount.
        diagnostics.RejectedConnectionCount.Should().Be(harness.Server.RejectedHelloCount);
        diagnostics.DroppedInboundPayloadCount.Should().Be(harness.Server.DroppedInboundPayloadCount);
        diagnostics.RateLimitedInboundPayloadCount.Should().Be(harness.Server.RateLimitedInboundPayloadCount);
    }

    [Fact]
    public void Bind_rejects_a_non_positive_per_peer_burst()
    {
        var act = () => UdpServerTransport.Bind(
            "bad-burst",
            new IPEndPoint(IPAddress.Loopback, 0),
            new UdpTransportOptions { MaxInboundPayloadBurstPerPeer = 0 });

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Bind_rejects_a_non_positive_per_peer_refill_rate()
    {
        var act = () => UdpServerTransport.Bind(
            "bad-refill",
            new IPEndPoint(IPAddress.Loopback, 0),
            new UdpTransportOptions { InboundPayloadRefillPerSecondPerPeer = 0 });

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    private static void ForceCloseSocket(object transport)
    {
        var field = transport.GetType().GetField(
            "_socket",
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("transport has no _socket field");
        var socket = (Socket)field.GetValue(transport)!;
        socket.Close();
    }
}
