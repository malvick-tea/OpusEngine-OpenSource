using System;
using FluentAssertions;
using Opus.Engine.Net.Soak;
using Xunit;

namespace Opus.Engine.Net.Tests.Soak;

/// <summary>Boundary validation for <see cref="NetSoakProfile"/>.</summary>
public sealed class NetSoakProfileTests
{
    [Fact]
    public void Default_profile_validates()
    {
        Action act = NetSoakProfile.Default.Validate;
        act.Should().NotThrow();
    }

    [Fact]
    public void Zero_peers_rejected()
    {
        var profile = NetSoakProfile.Default with { PeerCount = 0 };
        Action act = profile.Validate;
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Zero_payload_bytes_rejected()
    {
        var profile = NetSoakProfile.Default with { PayloadBytes = 0 };
        Action act = profile.Validate;
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Negative_budget_rejected()
    {
        var profile = NetSoakProfile.Default with { ConnectBudget = TimeSpan.FromSeconds(-1) };
        Action act = profile.Validate;
        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Total_expected_bytes_multiplies_correctly()
    {
        var profile = new NetSoakProfile(
            PeerCount: 4,
            PacketsPerPeer: 10,
            PayloadBytes: 100,
            EchoFromServer: false,
            ConnectBudget: TimeSpan.FromSeconds(1),
            WorkloadBudget: TimeSpan.FromSeconds(1));

        profile.TotalExpectedBytes.Should().Be(4L * 10 * 100);
    }
}
