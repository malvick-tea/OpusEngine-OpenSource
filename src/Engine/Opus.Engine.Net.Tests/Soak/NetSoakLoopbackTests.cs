using System;
using System.Linq;
using FluentAssertions;
using Opus.Engine.Net.Session;
using Opus.Engine.Net.Soak;
using Opus.Engine.Net.Tests.Support;
using Xunit;

namespace Opus.Engine.Net.Tests.Soak;

/// <summary>Drives <see cref="NetSoakHarness"/> through a loopback rig. The loopback
/// path never drops packets, so a clean run is the expected baseline; tests here pin
/// that the harness counts correctly, surfaces the diagnostics it should, and produces
/// a structured report for tester evidence.</summary>
public sealed class NetSoakLoopbackTests
{
    private static NetSoakProfile FastProfile(int peerCount, int packetsPerPeer) => new(
        PeerCount: peerCount,
        PacketsPerPeer: packetsPerPeer,
        PayloadBytes: 64,
        EchoFromServer: true,
        ConnectBudget: TimeSpan.FromSeconds(2),
        WorkloadBudget: TimeSpan.FromSeconds(3));

    [Fact]
    public void Loopback_run_finishes_clean_with_no_drops()
    {
        var profile = FastProfile(peerCount: 4, packetsPerPeer: 32);
        using var rig = LoopbackSoakRig.Create(profile.PeerCount);

        var report = NetSoakHarness.Run(profile, rig);

        report.IsClean.Should().BeTrue();
        report.Peers.Should().HaveCount(profile.PeerCount);
        report.TotalPacketsServerReceived.Should().Be(profile.PeerCount * profile.PacketsPerPeer);
        report.TotalEchoPacketsReceived.Should().Be(profile.PeerCount * profile.PacketsPerPeer);
        report.TotalBytesServerReceived.Should().Be(profile.PeerCount * profile.PacketsPerPeer * profile.PayloadBytes);
        report.ServerGuards.Should().Be(
            NetTransportGuardCounts.None,
            "the loopback hub does not expose the guard-diagnostics capability, so no guards are reported");
    }

    [Fact]
    public void Loopback_run_reports_per_peer_counters()
    {
        var profile = FastProfile(peerCount: 3, packetsPerPeer: 16);
        using var rig = LoopbackSoakRig.Create(profile.PeerCount);

        var report = NetSoakHarness.Run(profile, rig);

        report.Peers.All(p => p.Connected).Should().BeTrue();
        report.Peers.Select(p => p.PacketsSent).Should().AllSatisfy(c => c.Should().Be(profile.PacketsPerPeer));
        report.Peers.Select(p => p.PacketsServerReceived).Should().AllSatisfy(c => c.Should().Be(profile.PacketsPerPeer));
        report.Peers.Select(p => p.PacketsEchoReceived).Should().AllSatisfy(c => c.Should().Be(profile.PacketsPerPeer));
    }

    [Fact]
    public void Loopback_run_without_echo_does_not_count_echoes()
    {
        var profile = FastProfile(peerCount: 2, packetsPerPeer: 4) with { EchoFromServer = false };
        using var rig = LoopbackSoakRig.Create(profile.PeerCount);

        var report = NetSoakHarness.Run(profile, rig);

        report.IsClean.Should().BeTrue();
        report.TotalEchoPacketsReceived.Should().Be(0);
    }

    [Fact]
    public void Profile_count_mismatch_throws()
    {
        var profile = FastProfile(peerCount: 4, packetsPerPeer: 4);
        using var rig = LoopbackSoakRig.Create(2);

        Action act = () => NetSoakHarness.Run(profile, rig);

        act.Should().Throw<ArgumentException>();
    }
}
