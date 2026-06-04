using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using FluentAssertions;
using Opus.Net.Transport;
using Opus.Net.Udp.Transport;
using Xunit;

namespace Opus.Net.Udp.Tests.Transport;

[Collection(nameof(UdpTransportIntegrationTests))]
public sealed class UdpTransportGuardIntegrationTests
{
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

        for (var i = 0; i < 400; i++)
        {
            entry.Client.Send(UdpClientTransport.ServerSentinelId, new[] { (byte)i });
        }

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
            MaxInboundQueuedEvents = 1024,
            MaxInboundPayloadBurstPerPeer = burst,
            InboundPayloadRefillPerSecondPerPeer = 8,
        });

        var entry = harness.AddClient("flooder");
        harness.WaitForConnected(entry);
        var serverPeerId = harness.WaitForServerToAccept(entry);

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
            "the engine telemetry layer reads the guard counters through this capability");
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
