using System;
using FluentAssertions;
using Opus.Engine.AlphaStress.Network;
using Xunit;

namespace Opus.Engine.AlphaStress.Tests.Network;

public sealed class AlphaStressFaultInjectionToleranceTests
{
    [Fact]
    public void Default_pins_documented_constants()
    {
        var tolerance = AlphaStressFaultInjectionTolerance.Default;

        tolerance.MaxDropRate.Should().Be(0.25);
        tolerance.MaxObservedSoakIssues.Should().Be(0);
    }

    [Theory]
    [InlineData(0.0)]
    [InlineData(0.5)]
    [InlineData(1.0)]
    public void Validate_accepts_in_range_drop_rate(double rate)
    {
        var tolerance = new AlphaStressFaultInjectionTolerance(rate, MaxObservedSoakIssues: 0);

        var act = () => tolerance.Validate();

        act.Should().NotThrow();
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    [InlineData(double.NaN)]
    public void Validate_rejects_out_of_range_drop_rate(double rate)
    {
        var tolerance = new AlphaStressFaultInjectionTolerance(rate, MaxObservedSoakIssues: 0);

        var act = () => tolerance.Validate();

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Validate_rejects_negative_soak_issue_limit()
    {
        var tolerance = new AlphaStressFaultInjectionTolerance(MaxDropRate: 0.5, MaxObservedSoakIssues: -1);

        var act = () => tolerance.Validate();

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Default_inbound_drop_rate_is_disabled()
    {
        var tolerance = AlphaStressFaultInjectionTolerance.Default;

        tolerance.MaxInboundDropRate.Should().Be(1.0);
    }

    [Theory]
    [InlineData(-0.01)]
    [InlineData(1.01)]
    [InlineData(double.NaN)]
    public void Validate_rejects_out_of_range_inbound_drop_rate(double rate)
    {
        var tolerance = new AlphaStressFaultInjectionTolerance(MaxDropRate: 0.25, MaxObservedSoakIssues: 0)
        {
            MaxInboundDropRate = rate,
        };

        var act = () => tolerance.Validate();

        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("MaxInboundDropRate");
    }
}
