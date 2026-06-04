using System;
using FluentAssertions;
using Opus.Engine.AlphaStress.Network;
using Opus.Engine.Net.Soak;
using Opus.Engine.Net.Transport;
using Xunit;

namespace Opus.Engine.AlphaStress.Tests.Network;

public sealed class LoopbackFaultInjectionNetworkProbeTests
{
    [Fact]
    public void RunIteration_null_profile_throws()
    {
        using var probe = new LoopbackFaultInjectionNetworkProbe();

        var act = () => probe.RunIteration(0, null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void RunIteration_negative_iteration_throws()
    {
        using var probe = new LoopbackFaultInjectionNetworkProbe();

        var act = () => probe.RunIteration(-1, BuildProfile());

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void RunIteration_after_dispose_throws()
    {
        var probe = new LoopbackFaultInjectionNetworkProbe();
        probe.Dispose();

        var act = () => probe.RunIteration(0, BuildProfile());

        act.Should().Throw<ObjectDisposedException>();
    }

    [Fact]
    public void RunIteration_with_pass_through_injection_reports_zero_drops()
    {
        using var probe = new LoopbackFaultInjectionNetworkProbe();
        var profile = BuildProfile();

        var observation = probe.RunIteration(0, profile);

        observation.IterationIndex.Should().Be(0);
        observation.ClientSendAttempts.Should().Be((long)profile.Soak.PeerCount * profile.Soak.PacketsPerPeer);
        observation.DroppedPackets.Should().Be(0);
    }

    [Fact]
    public void RunIteration_with_full_loss_drops_every_attempt()
    {
        using var probe = new LoopbackFaultInjectionNetworkProbe();
        var profile = BuildProfile() with
        {
            Injection = new LatencyLossInjectionProfile(LossRate: 1.0, AddedLatency: TimeSpan.Zero, Seed: 7),
        };

        var observation = probe.RunIteration(0, profile);

        observation.DroppedPackets.Should().Be(observation.ClientSendAttempts);
    }

    [Fact]
    public void RunIteration_yields_deterministic_drop_count_across_runs()
    {
        var profile = BuildProfile() with
        {
            Injection = new LatencyLossInjectionProfile(LossRate: 0.5, AddedLatency: TimeSpan.Zero, Seed: 12345),
        };

        long firstRun;
        long secondRun;
        using (var probe = new LoopbackFaultInjectionNetworkProbe())
        {
            firstRun = probe.RunIteration(0, profile).DroppedPackets;
        }

        using (var probe = new LoopbackFaultInjectionNetworkProbe())
        {
            secondRun = probe.RunIteration(0, profile).DroppedPackets;
        }

        firstRun.Should().Be(secondRun);
    }

    [Fact]
    public void RunIteration_with_full_inbound_loss_records_inbound_drops()
    {
        using var probe = new LoopbackFaultInjectionNetworkProbe();
        var profile = BuildProfile() with
        {
            Injection = LatencyLossInjectionProfile.None with { InboundLossRate = 1.0 },
        };

        var observation = probe.RunIteration(0, profile);

        observation.InboundDroppedPackets.Should().BeGreaterThan(0);
        observation.InboundDroppedPackets.Should().Be(observation.InboundAttempts);
    }

    [Fact]
    public void RunIteration_pass_through_records_zero_inbound_drops()
    {
        using var probe = new LoopbackFaultInjectionNetworkProbe();
        var profile = BuildProfile();

        var observation = probe.RunIteration(0, profile);

        observation.InboundDroppedPackets.Should().Be(0);
        observation.InboundDelayedPackets.Should().Be(0);
    }

    private static AlphaStressNetworkProfile BuildProfile() => new(
        Injection: LatencyLossInjectionProfile.None,
        Soak: new NetSoakProfile(
            PeerCount: 2,
            PacketsPerPeer: 4,
            PayloadBytes: 16,
            EchoFromServer: true,
            ConnectBudget: TimeSpan.FromSeconds(1),
            WorkloadBudget: TimeSpan.FromMilliseconds(500)),
        Tolerance: AlphaStressFaultInjectionTolerance.Default);
}
