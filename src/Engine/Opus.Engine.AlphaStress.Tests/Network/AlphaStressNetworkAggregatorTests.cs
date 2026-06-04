using System;
using FluentAssertions;
using Opus.Engine.AlphaStress.Network;
using Xunit;

namespace Opus.Engine.AlphaStress.Tests.Network;

public sealed class AlphaStressNetworkAggregatorTests
{
    [Fact]
    public void BuildSummary_with_no_observations_returns_empty()
    {
        var aggregator = new AlphaStressNetworkAggregator();

        var summary = aggregator.BuildSummary();

        summary.Should().Be(AlphaStressNetworkSummary.Empty);
        summary.HasObservations.Should().BeFalse();
    }

    [Fact]
    public void BuildSummary_sums_counters_and_derives_fractions()
    {
        var aggregator = new AlphaStressNetworkAggregator();
        aggregator.Record(BuildObservation(iterationIndex: 0, sends: 100, drops: 10, delays: 80, soakIssues: 0));
        aggregator.Record(BuildObservation(iterationIndex: 1, sends: 100, drops: 30, delays: 50, soakIssues: 1));

        var summary = aggregator.BuildSummary();

        summary.IterationCount.Should().Be(2);
        summary.TotalClientSendAttempts.Should().Be(200);
        summary.TotalDroppedPackets.Should().Be(40);
        summary.TotalDelayedPackets.Should().Be(130);
        summary.TotalSoakIssueCount.Should().Be(1);
        summary.DropFraction.Should().BeApproximately(40.0 / 200.0, 1e-9);
        summary.DelayedFraction.Should().BeApproximately(130.0 / 200.0, 1e-9);
        summary.HasObservations.Should().BeTrue();
    }

    [Fact]
    public void BuildSummary_with_zero_attempts_yields_zero_fractions()
    {
        var aggregator = new AlphaStressNetworkAggregator();
        aggregator.Record(BuildObservation(iterationIndex: 0, sends: 0, drops: 0, delays: 0, soakIssues: 1));

        var summary = aggregator.BuildSummary();

        summary.DropFraction.Should().Be(0.0);
        summary.DelayedFraction.Should().Be(0.0);
        summary.TotalSoakIssueCount.Should().Be(1);
    }

    [Fact]
    public void Record_rejects_null_observation()
    {
        var aggregator = new AlphaStressNetworkAggregator();

        var act = () => aggregator.Record(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Record_rejects_invalid_observation()
    {
        var aggregator = new AlphaStressNetworkAggregator();
        var bad = new AlphaStressNetworkObservation(-1, DateTimeOffset.UtcNow, 0, 0, 0, 0);

        var act = () => aggregator.Record(bad);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Record_rejects_non_monotonic_iteration_index()
    {
        var aggregator = new AlphaStressNetworkAggregator();
        aggregator.Record(BuildObservation(iterationIndex: 1, sends: 0, drops: 0, delays: 0, soakIssues: 0));

        var act = () => aggregator.Record(BuildObservation(iterationIndex: 1, sends: 0, drops: 0, delays: 0, soakIssues: 0));

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void BuildSummary_sums_inbound_counters_and_derives_inbound_fractions()
    {
        var aggregator = new AlphaStressNetworkAggregator();
        aggregator.Record(BuildObservation(iterationIndex: 0, sends: 100, drops: 0, delays: 0, soakIssues: 0) with
        {
            InboundAttempts = 80,
            InboundDroppedPackets = 8,
            InboundDelayedPackets = 40,
        });
        aggregator.Record(BuildObservation(iterationIndex: 1, sends: 100, drops: 0, delays: 0, soakIssues: 0) with
        {
            InboundAttempts = 120,
            InboundDroppedPackets = 12,
            InboundDelayedPackets = 60,
        });

        var summary = aggregator.BuildSummary();

        summary.TotalInboundAttempts.Should().Be(200);
        summary.TotalInboundDroppedPackets.Should().Be(20);
        summary.TotalInboundDelayedPackets.Should().Be(100);
        summary.InboundDropFraction.Should().BeApproximately(20.0 / 200.0, 1e-9);
        summary.InboundDelayedFraction.Should().BeApproximately(100.0 / 200.0, 1e-9);
    }

    [Fact]
    public void BuildSummary_with_zero_inbound_attempts_yields_zero_inbound_fractions()
    {
        var aggregator = new AlphaStressNetworkAggregator();
        aggregator.Record(BuildObservation(iterationIndex: 0, sends: 10, drops: 1, delays: 0, soakIssues: 0));

        var summary = aggregator.BuildSummary();

        summary.InboundDropFraction.Should().Be(0.0);
        summary.InboundDelayedFraction.Should().Be(0.0);
    }

    private static AlphaStressNetworkObservation BuildObservation(
        int iterationIndex,
        long sends,
        long drops,
        long delays,
        int soakIssues) => new(
            IterationIndex: iterationIndex,
            ObservedAtUtc: DateTimeOffset.UtcNow,
            ClientSendAttempts: sends,
            DroppedPackets: drops,
            DelayedPackets: delays,
            SoakIssueCount: soakIssues);
}
