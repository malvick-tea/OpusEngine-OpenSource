using System;
using FluentAssertions;
using Opus.Engine.Net.Transport;
using Xunit;

namespace Opus.Engine.Net.Tests.Transport;

public sealed class LatencyLossInjectionProfileTests
{
    [Fact]
    public void None_validates_cleanly()
    {
        var profile = LatencyLossInjectionProfile.None;

        var act = profile.Validate;

        act.Should().NotThrow();
        profile.LossRate.Should().Be(0.0);
        profile.AddedLatency.Should().Be(TimeSpan.Zero);
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    [InlineData(double.NaN)]
    public void Validate_rejects_loss_rate_outside_unit_interval(double rate)
    {
        var profile = LatencyLossInjectionProfile.None with { LossRate = rate };

        var act = profile.Validate;

        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("LossRate");
    }

    [Fact]
    public void Validate_accepts_boundary_loss_rates()
    {
        var zero = LatencyLossInjectionProfile.None with { LossRate = 0.0 };
        var one = LatencyLossInjectionProfile.None with { LossRate = 1.0 };

        zero.Validate();
        one.Validate();
    }

    [Fact]
    public void Validate_rejects_negative_latency()
    {
        var profile = LatencyLossInjectionProfile.None with { AddedLatency = TimeSpan.FromMilliseconds(-1) };

        var act = profile.Validate;

        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("AddedLatency");
    }

    [Fact]
    public void None_inbound_defaults_to_no_op_values()
    {
        var profile = LatencyLossInjectionProfile.None;

        profile.InboundLossRate.Should().Be(0.0);
        profile.InboundAddedLatency.Should().Be(TimeSpan.Zero);
        profile.InboundSeed.Should().Be(0);
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    [InlineData(double.NaN)]
    public void Validate_rejects_inbound_loss_rate_outside_unit_interval(double rate)
    {
        var profile = LatencyLossInjectionProfile.None with { InboundLossRate = rate };

        var act = profile.Validate;

        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("InboundLossRate");
    }

    [Fact]
    public void Validate_rejects_negative_inbound_latency()
    {
        var profile = LatencyLossInjectionProfile.None with { InboundAddedLatency = TimeSpan.FromMilliseconds(-1) };

        var act = profile.Validate;

        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("InboundAddedLatency");
    }

    [Fact]
    public void Validate_accepts_inbound_boundary_loss_rates()
    {
        var zero = LatencyLossInjectionProfile.None with { InboundLossRate = 0.0 };
        var one = LatencyLossInjectionProfile.None with { InboundLossRate = 1.0 };

        zero.Validate();
        one.Validate();
    }
}
