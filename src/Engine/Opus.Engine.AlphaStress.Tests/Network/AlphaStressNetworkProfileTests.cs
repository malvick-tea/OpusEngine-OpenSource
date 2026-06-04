using System;
using FluentAssertions;
using Opus.Engine.AlphaStress.Network;
using Opus.Engine.Net.Soak;
using Opus.Engine.Net.Transport;
using Xunit;

namespace Opus.Engine.AlphaStress.Tests.Network;

public sealed class AlphaStressNetworkProfileTests
{
    [Fact]
    public void Default_has_non_null_components()
    {
        var profile = AlphaStressNetworkProfile.Default;

        profile.Injection.Should().NotBeNull();
        profile.Soak.Should().NotBeNull();
        profile.Tolerance.Should().NotBeNull();
    }

    [Fact]
    public void Default_passes_validation()
    {
        var profile = AlphaStressNetworkProfile.Default;

        var act = () => profile.Validate();

        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_rejects_null_injection()
    {
        var profile = new AlphaStressNetworkProfile(null!, NetSoakProfile.Default, AlphaStressFaultInjectionTolerance.Default);

        var act = () => profile.Validate();

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Validate_rejects_null_soak()
    {
        var profile = new AlphaStressNetworkProfile(LatencyLossInjectionProfile.None, null!, AlphaStressFaultInjectionTolerance.Default);

        var act = () => profile.Validate();

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Validate_rejects_null_tolerance()
    {
        var profile = new AlphaStressNetworkProfile(LatencyLossInjectionProfile.None, NetSoakProfile.Default, null!);

        var act = () => profile.Validate();

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Validate_propagates_inner_validation_failure()
    {
        var profile = new AlphaStressNetworkProfile(
            new LatencyLossInjectionProfile(LossRate: 2.0, AddedLatency: TimeSpan.Zero, Seed: 0),
            NetSoakProfile.Default,
            AlphaStressFaultInjectionTolerance.Default);

        var act = () => profile.Validate();

        act.Should().Throw<ArgumentOutOfRangeException>();
    }
}
