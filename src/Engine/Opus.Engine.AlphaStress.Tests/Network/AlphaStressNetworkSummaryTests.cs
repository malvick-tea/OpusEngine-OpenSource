using FluentAssertions;
using Opus.Engine.AlphaStress.Network;
using Xunit;

namespace Opus.Engine.AlphaStress.Tests.Network;

public sealed class AlphaStressNetworkSummaryTests
{
    [Fact]
    public void Empty_has_no_observations()
    {
        var summary = AlphaStressNetworkSummary.Empty;

        summary.HasObservations.Should().BeFalse();
        summary.IterationCount.Should().Be(0);
        summary.TotalClientSendAttempts.Should().Be(0);
        summary.DropFraction.Should().Be(0.0);
        summary.DelayedFraction.Should().Be(0.0);
    }

    [Fact]
    public void HasObservations_is_true_when_iteration_count_positive()
    {
        var summary = new AlphaStressNetworkSummary(
            IterationCount: 1,
            TotalClientSendAttempts: 10,
            TotalDroppedPackets: 1,
            TotalDelayedPackets: 0,
            TotalSoakIssueCount: 0,
            DropFraction: 0.1,
            DelayedFraction: 0.0);

        summary.HasObservations.Should().BeTrue();
    }
}
