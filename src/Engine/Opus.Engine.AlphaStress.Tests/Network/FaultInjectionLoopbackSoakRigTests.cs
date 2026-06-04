using System;
using System.Collections.Generic;
using FluentAssertions;
using Opus.Engine.AlphaStress.Network;
using Opus.Engine.Net.Transport;
using Opus.Net.Loopback;
using Opus.Net.Transport;
using Xunit;

namespace Opus.Engine.AlphaStress.Tests.Network;

public sealed class FaultInjectionLoopbackSoakRigTests
{
    [Fact]
    public void Create_zero_peers_throws()
    {
        var act = () => FaultInjectionLoopbackSoakRig.Create(0, LatencyLossInjectionProfile.None);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Create_null_injection_throws()
    {
        var act = () => FaultInjectionLoopbackSoakRig.Create(2, null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Create_invalid_injection_throws()
    {
        var act = () => FaultInjectionLoopbackSoakRig.Create(
            2,
            new LatencyLossInjectionProfile(LossRate: -0.1, AddedLatency: TimeSpan.Zero, Seed: 0));

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Create_returns_rig_with_expected_peer_count()
    {
        using var rig = FaultInjectionLoopbackSoakRig.Create(3, LatencyLossInjectionProfile.None);

        rig.PeerCount.Should().Be(3);
        rig.Server.Should().NotBeNull();
        for (var i = 0; i < 3; i++)
        {
            rig.Client(i).Should().NotBeNull();
        }
    }

    [Fact]
    public void Per_peer_wrappers_drop_independently_under_same_seed_root()
    {
        var injection = new LatencyLossInjectionProfile(LossRate: 1.0, AddedLatency: TimeSpan.Zero, Seed: 42);
        using var rig = FaultInjectionLoopbackSoakRig.Create(2, injection);
        var payload = new byte[] { 1, 2, 3 };

        for (var i = 0; i < 4; i++)
        {
            rig.Client(0).Send(rig.ServerSentinel, payload);
            rig.Client(1).Send(rig.ServerSentinel, payload);
        }

        rig.TotalDroppedPackets.Should().Be(8);
    }

    [Fact]
    public void Pass_through_profile_routes_packets_to_server()
    {
        using var rig = FaultInjectionLoopbackSoakRig.Create(1, LatencyLossInjectionProfile.None);
        var payload = new byte[] { 7, 7, 7 };

        rig.Client(0).Send(rig.ServerSentinel, payload).Should().BeTrue();

        var events = new List<NetEvent>();
        rig.Server.Poll(events);
        events.Should().Contain(static ev => ev.Kind == NetEventKind.Received);
    }

    [Fact]
    public void Dispose_is_idempotent()
    {
        var rig = FaultInjectionLoopbackSoakRig.Create(2, LatencyLossInjectionProfile.None);

        rig.Dispose();
        var act = () => rig.Dispose();

        act.Should().NotThrow();
    }

    [Fact]
    public void Inbound_counters_aggregate_across_wrappers()
    {
        var injection = LatencyLossInjectionProfile.None with { InboundLossRate = 1.0 };
        using var rig = FaultInjectionLoopbackSoakRig.Create(2, injection);
        var hub = (LoopbackTransportHub)rig.Server;
        var pollBuffer = new List<NetEvent>();
        rig.Client(0).Poll(pollBuffer);
        rig.Client(1).Poll(pollBuffer);
        var peerIds = new List<ConnectionId>();
        hub.Poll(pollBuffer);
        foreach (var ev in pollBuffer)
        {
            if (ev.Kind == NetEventKind.Connected)
            {
                peerIds.Add(ev.Connection);
            }
        }

        for (var i = 0; i < 3; i++)
        {
            hub.Send(peerIds[0], new byte[] { (byte)i });
            hub.Send(peerIds[1], new byte[] { (byte)(10 + i) });
        }

        rig.Client(0).Poll(pollBuffer);
        rig.Client(1).Poll(pollBuffer);

        rig.TotalInboundDroppedPackets.Should().Be(6);
        rig.TotalInboundAttempts.Should().Be(6);
    }
}
