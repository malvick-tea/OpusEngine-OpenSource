using System;
using FluentAssertions;
using Opus.Engine.Net.Session;
using Xunit;

namespace Opus.Engine.Net.Tests.Session;

public sealed class NetSessionRateAggregatorTests
{
    private static readonly DateTimeOffset T0 = new(2026, 5, 28, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void First_sample_returns_empty_rate()
    {
        var aggregator = new NetSessionRateAggregator();

        var rate = aggregator.Sample(T0, 0, 0, 0, 0);

        rate.Should().Be(NetSessionRateSnapshot.Empty);
    }

    [Fact]
    public void Second_sample_returns_rate_over_elapsed_window()
    {
        var aggregator = new NetSessionRateAggregator();
        aggregator.Sample(T0, 0, 0, 0, 0);

        var rate = aggregator.Sample(T0.AddSeconds(2), 60, 30, 6000, 3000);

        rate.WindowDuration.Should().Be(TimeSpan.FromSeconds(2));
        rate.PacketsReceivedPerSecond.Should().Be(30.0);
        rate.PacketsSentPerSecond.Should().Be(15.0);
        rate.BytesReceivedPerSecond.Should().Be(3000.0);
        rate.BytesSentPerSecond.Should().Be(1500.0);
    }

    [Fact]
    public void Sample_at_same_instant_returns_empty_rate()
    {
        var aggregator = new NetSessionRateAggregator();
        aggregator.Sample(T0, 0, 0, 0, 0);

        var rate = aggregator.Sample(T0, 100, 100, 100, 100);

        rate.Should().Be(NetSessionRateSnapshot.Empty);
    }

    [Fact]
    public void Counter_regression_clamps_to_zero_delta_rate()
    {
        var aggregator = new NetSessionRateAggregator();
        aggregator.Sample(T0, 100, 100, 100, 100);

        var rate = aggregator.Sample(T0.AddSeconds(1), 50, 50, 50, 50);

        rate.PacketsReceivedPerSecond.Should().Be(
            0.0,
            "a counter that goes backwards (impossible in production but defensive here) clamps to zero rate.");
        rate.PacketsSentPerSecond.Should().Be(0.0);
        rate.BytesReceivedPerSecond.Should().Be(0.0);
        rate.BytesSentPerSecond.Should().Be(0.0);
    }

    [Fact]
    public void Successive_samples_diff_against_latest_baseline()
    {
        var aggregator = new NetSessionRateAggregator();
        aggregator.Sample(T0, 0, 0, 0, 0);
        aggregator.Sample(T0.AddSeconds(1), 10, 10, 100, 100);

        var rate = aggregator.Sample(T0.AddSeconds(3), 30, 30, 300, 300);

        rate.WindowDuration.Should().Be(
            TimeSpan.FromSeconds(2),
            "the second-to-third call should diff against the second baseline, not the first.");
        rate.PacketsReceivedPerSecond.Should().Be(10.0);
    }

    [Fact]
    public void Reset_drops_baseline_so_next_sample_returns_empty()
    {
        var aggregator = new NetSessionRateAggregator();
        aggregator.Sample(T0, 0, 0, 0, 0);
        aggregator.Sample(T0.AddSeconds(1), 10, 10, 10, 10);

        aggregator.Reset();
        var rate = aggregator.Sample(T0.AddSeconds(2), 100, 100, 100, 100);

        rate.Should().Be(
            NetSessionRateSnapshot.Empty,
            "after Reset the next sample becomes the new baseline and returns empty.");
    }
}
