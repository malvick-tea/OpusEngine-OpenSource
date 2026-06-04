using System;
using FluentAssertions;
using Opus.Engine.AlphaHarness.Soak;
using Opus.Engine.Net.Soak;
using Xunit;

namespace Opus.Engine.AlphaHarness.Tests.Soak;

public sealed class AlphaLoopbackSoakRigTests
{
    [Fact]
    public void Create_with_peer_count_exposes_client_transports()
    {
        using var rig = AlphaLoopbackSoakRig.Create(peerCount: 3);

        rig.PeerCount.Should().Be(3);
        rig.Server.Should().NotBeNull();
        rig.Client(0).Should().NotBeNull();
        rig.Client(2).Should().NotBeNull();
        rig.ServerSentinel.Should().NotBe(default);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Create_rejects_non_positive_peer_count(int peerCount)
    {
        Action act = () => AlphaLoopbackSoakRig.Create(peerCount);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Dispose_releases_clients_and_hub_without_throwing()
    {
        var rig = AlphaLoopbackSoakRig.Create(peerCount: 2);

        rig.Invoking(r => r.Dispose()).Should().NotThrow();
    }

    [Fact]
    public void Rig_drives_soak_harness_to_clean_outcome()
    {
        using var rig = AlphaLoopbackSoakRig.Create(peerCount: 2);
        var profile = new NetSoakProfile(
            PeerCount: 2,
            PacketsPerPeer: 4,
            PayloadBytes: 32,
            EchoFromServer: true,
            ConnectBudget: TimeSpan.FromSeconds(1),
            WorkloadBudget: TimeSpan.FromSeconds(2));

        var report = NetSoakHarness.Run(profile, rig);

        report.IsClean.Should().BeTrue();
        report.Profile.PeerCount.Should().Be(2);
    }
}
