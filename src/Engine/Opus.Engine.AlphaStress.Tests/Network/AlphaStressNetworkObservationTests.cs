using System;
using FluentAssertions;
using Opus.Engine.AlphaStress.Network;
using Xunit;

namespace Opus.Engine.AlphaStress.Tests.Network;

public sealed class AlphaStressNetworkObservationTests
{
    [Fact]
    public void Validate_accepts_balanced_counters()
    {
        var observation = new AlphaStressNetworkObservation(
            IterationIndex: 0,
            ObservedAtUtc: DateTimeOffset.UtcNow,
            ClientSendAttempts: 100,
            DroppedPackets: 10,
            DelayedPackets: 20,
            SoakIssueCount: 0);

        var act = () => observation.Validate();

        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_rejects_negative_iteration_index()
    {
        var observation = new AlphaStressNetworkObservation(
            IterationIndex: -1,
            ObservedAtUtc: DateTimeOffset.UtcNow,
            ClientSendAttempts: 0,
            DroppedPackets: 0,
            DelayedPackets: 0,
            SoakIssueCount: 0);

        var act = () => observation.Validate();

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Validate_rejects_negative_send_attempts()
    {
        var observation = new AlphaStressNetworkObservation(
            IterationIndex: 0,
            ObservedAtUtc: DateTimeOffset.UtcNow,
            ClientSendAttempts: -1,
            DroppedPackets: 0,
            DelayedPackets: 0,
            SoakIssueCount: 0);

        var act = () => observation.Validate();

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Validate_rejects_negative_dropped_packets()
    {
        var observation = new AlphaStressNetworkObservation(
            IterationIndex: 0,
            ObservedAtUtc: DateTimeOffset.UtcNow,
            ClientSendAttempts: 10,
            DroppedPackets: -1,
            DelayedPackets: 0,
            SoakIssueCount: 0);

        var act = () => observation.Validate();

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Validate_rejects_negative_delayed_packets()
    {
        var observation = new AlphaStressNetworkObservation(
            IterationIndex: 0,
            ObservedAtUtc: DateTimeOffset.UtcNow,
            ClientSendAttempts: 10,
            DroppedPackets: 0,
            DelayedPackets: -1,
            SoakIssueCount: 0);

        var act = () => observation.Validate();

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Validate_rejects_negative_soak_issue_count()
    {
        var observation = new AlphaStressNetworkObservation(
            IterationIndex: 0,
            ObservedAtUtc: DateTimeOffset.UtcNow,
            ClientSendAttempts: 10,
            DroppedPackets: 0,
            DelayedPackets: 0,
            SoakIssueCount: -1);

        var act = () => observation.Validate();

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Validate_rejects_drops_plus_delays_exceeding_attempts()
    {
        var observation = new AlphaStressNetworkObservation(
            IterationIndex: 0,
            ObservedAtUtc: DateTimeOffset.UtcNow,
            ClientSendAttempts: 10,
            DroppedPackets: 6,
            DelayedPackets: 6,
            SoakIssueCount: 0);

        var act = () => observation.Validate();

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Validate_accepts_balanced_inbound_counters()
    {
        var observation = BuildBase() with
        {
            InboundAttempts = 50,
            InboundDroppedPackets = 5,
            InboundDelayedPackets = 30,
        };

        var act = () => observation.Validate();

        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_rejects_negative_inbound_attempts()
    {
        var observation = BuildBase() with { InboundAttempts = -1 };

        var act = () => observation.Validate();

        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("InboundAttempts");
    }

    [Fact]
    public void Validate_rejects_negative_inbound_dropped()
    {
        var observation = BuildBase() with { InboundAttempts = 0, InboundDroppedPackets = -1 };

        var act = () => observation.Validate();

        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("InboundDroppedPackets");
    }

    [Fact]
    public void Validate_rejects_inbound_drops_plus_delays_exceeding_attempts()
    {
        var observation = BuildBase() with
        {
            InboundAttempts = 4,
            InboundDroppedPackets = 3,
            InboundDelayedPackets = 3,
        };

        var act = () => observation.Validate();

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    private static AlphaStressNetworkObservation BuildBase() => new(
        IterationIndex: 0,
        ObservedAtUtc: DateTimeOffset.UtcNow,
        ClientSendAttempts: 100,
        DroppedPackets: 0,
        DelayedPackets: 0,
        SoakIssueCount: 0);
}
