using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using FluentAssertions;
using Opus.Net.Transport;
using Opus.Net.Udp.Frame;
using Opus.Net.Udp.Transport;
using Xunit;

namespace Opus.Net.Udp.Tests.Transport;

public sealed partial class UdpTransportIntegrationTests
{
    [Fact]
    public void Invalid_hello_mac_does_not_consume_source_rate_limit()
    {
        var correctKey = UdpAuthentication.DeriveKey("valid-source-rate-limit-key");
        using var harness = UdpIntegrationHarness.Start("server", () => new UdpTransportOptions
        {
            AuthenticationKey = correctKey,
            HeartbeatInterval = TimeSpan.FromMilliseconds(100),
            DeadlineDuration = TimeSpan.FromSeconds(2),
            ReceivePollInterval = TimeSpan.FromMilliseconds(25),
            ConnectTimeout = TimeSpan.FromMilliseconds(600),
            HelloBurstPerSource = 1,
            HelloRefillPerSecondPerSource = 1,
        });
        using var attacker = new Socket(
            AddressFamily.InterNetwork,
            SocketType.Dgram,
            ProtocolType.Udp);
        var wrongKey = UdpAuthentication.DeriveKey("invalid-source-rate-limit-key");
        var nonce = UdpAuthentication.CreateNonce();
        var invalidHello = new byte[
            UdpFrameHeader.SizeBytes
            + UdpAuthentication.NonceBytes
            + UdpFrameHeader.AuthenticationTagBytes];
        UdpFrameCodec.EncodeAuthenticated(
            UdpFrameKind.Hello,
            ConnectionId.None,
            sequence: 0,
            nonce,
            wrongKey,
            invalidHello);

        attacker.SendTo(invalidHello, harness.Server.BoundEndpoint);
        Thread.Sleep(100);
        var client = harness.AddClient("legitimate-client");

        var connection = harness.WaitForConnected(
            client,
            TimeSpan.FromMilliseconds(800));

        connection.IsValid.Should().BeTrue(
            "an unauthenticated datagram must not consume the authenticated Hello budget");
    }

    [Fact]
    public void Server_drops_clients_outside_the_address_allowlist()
    {
        using var harness = UdpIntegrationHarness.Start("server", () =>
            UdpIntegrationHarness.AuthenticatedFastOptions() with
            {
                AllowedRemoteAddresses = new[] { IPAddress.Parse("192.0.2.10") },
                ConnectTimeout = TimeSpan.FromMilliseconds(300),
            });
        var client = harness.AddClient("disallowed-client");

        harness.WaitFor(
                () => client.Events.Any(e => e.Kind == NetEventKind.Disconnected),
                TimeSpan.FromSeconds(1))
            .Should().BeTrue();
        harness.ServerEvents.Should().NotContain(
            entry => entry.Kind == NetEventKind.Connected);
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
    public void Bind_rejects_a_non_positive_per_address_peer_cap()
    {
        var act = () => UdpServerTransport.Bind(
            "bad-address-cap",
            new IPEndPoint(IPAddress.Loopback, 0),
            new UdpTransportOptions { MaxConcurrentPeersPerAddress = 0 });

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Server_rejects_source_ports_beyond_the_per_address_cap()
    {
        using var harness = UdpIntegrationHarness.Start("server", () => new UdpTransportOptions
        {
            HeartbeatInterval = TimeSpan.FromMilliseconds(100),
            DeadlineDuration = TimeSpan.FromSeconds(5),
            ReceivePollInterval = TimeSpan.FromMilliseconds(50),
            ConnectTimeout = TimeSpan.FromSeconds(2),
            MaxConcurrentPeers = 8,
            MaxConcurrentPeersPerAddress = 2,
        });

        var clientA = harness.AddClient("client-a");
        var clientB = harness.AddClient("client-b");
        var clientC = harness.AddClient("client-c");
        int ConnectedClientCount() => new[] { clientA, clientB, clientC }
            .Count(entry => entry.Events.Any(e => e.Kind == NetEventKind.Connected));

        var capHeld = harness.WaitFor(() =>
            harness.ServerEvents.Count(e => e.Kind == NetEventKind.Connected) == 2
            && harness.Server.RejectedHelloCount > 0
            && ConnectedClientCount() == 2);

        capHeld.Should().BeTrue();
        ConnectedClientCount().Should().Be(
            2,
            "one source address cannot consume the entire global peer table by rotating ports");
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
    public void Server_transport_surfaces_its_guard_counters_through_the_diagnostics_capability()
    {
        using var harness = UdpIntegrationHarness.Start();

        harness.Server.Should().BeAssignableTo<INetServerTransportDiagnostics>(
            "the engine telemetry layer reads the DoS-guard counters through this capability");
        var diagnostics = (INetServerTransportDiagnostics)harness.Server;

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
}
