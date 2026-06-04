using System;
using System.Linq;
using FluentAssertions;
using Opus.Engine.Net.Soak;
using Opus.Engine.Net.Tests.Support;
using Xunit;

namespace Opus.Engine.Net.Tests.Soak;

/// <summary>Real-UDP integration smoke for <see cref="NetSoakHarness"/>. The harness
/// drives N <see cref="Opus.Net.Udp.Transport.UdpClientTransport"/>s against one
/// <see cref="Opus.Net.Udp.Transport.UdpServerTransport"/> on <c>127.0.0.1</c>, so
/// every packet leaves the process through a real socket. Sized small enough to finish
/// inside seconds — the M11 pass will introduce 20-peer stress.</summary>
[Collection(nameof(NetSoakUdpTests))]
[CollectionDefinition(nameof(NetSoakUdpTests), DisableParallelization = true)]
public sealed class NetSoakUdpTests
{
    [Fact]
    public void Udp_run_completes_clean_for_small_cohort()
    {
        var profile = new NetSoakProfile(
            PeerCount: 3,
            PacketsPerPeer: 16,
            PayloadBytes: 128,
            EchoFromServer: true,
            ConnectBudget: TimeSpan.FromSeconds(4),
            WorkloadBudget: TimeSpan.FromSeconds(6));
        using var rig = UdpSoakRig.Create(profile.PeerCount);

        var report = NetSoakHarness.Run(profile, rig);

        report.Issues.Should().BeEmpty();
        report.Peers.All(p => p.Connected).Should().BeTrue();
        report.TotalPacketsServerReceived.Should().Be(profile.PeerCount * profile.PacketsPerPeer);
        report.TotalEchoPacketsReceived.Should().Be(profile.PeerCount * profile.PacketsPerPeer);
    }

    [Fact]
    public void Udp_run_reports_wall_clock_inside_workload_budget()
    {
        var profile = new NetSoakProfile(
            PeerCount: 2,
            PacketsPerPeer: 8,
            PayloadBytes: 64,
            EchoFromServer: false,
            ConnectBudget: TimeSpan.FromSeconds(3),
            WorkloadBudget: TimeSpan.FromSeconds(4));
        using var rig = UdpSoakRig.Create(profile.PeerCount);

        var report = NetSoakHarness.Run(profile, rig);

        report.IsClean.Should().BeTrue();
        report.ElapsedWallClock.Should().BeLessThan(profile.WorkloadBudget);
    }
}
