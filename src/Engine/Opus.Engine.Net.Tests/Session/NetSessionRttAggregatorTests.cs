using System;
using FluentAssertions;
using Opus.Engine.Net.Session;
using Xunit;

namespace Opus.Engine.Net.Tests.Session;

public sealed class NetSessionRttAggregatorTests
{
    [Fact]
    public void Empty_aggregator_yields_zero_summary()
    {
        var aggregator = new NetSessionRttAggregator(8);

        var summary = aggregator.BuildSummary();

        summary.SampleCount.Should().Be(0);
        summary.WindowCapacity.Should().Be(8);
        summary.Mean.Should().Be(TimeSpan.Zero);
        summary.Minimum.Should().Be(TimeSpan.Zero);
        summary.Maximum.Should().Be(TimeSpan.Zero);
        summary.Percentile95.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void Ctor_rejects_zero_or_negative_capacity()
    {
        var act = () => new NetSessionRttAggregator(0);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Record_rejects_negative_rtt()
    {
        var aggregator = new NetSessionRttAggregator(4);

        var act = () => aggregator.Record(TimeSpan.FromMilliseconds(-1));

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Summary_reports_mean_min_max_after_records()
    {
        var aggregator = new NetSessionRttAggregator(8);
        aggregator.Record(TimeSpan.FromMilliseconds(20));
        aggregator.Record(TimeSpan.FromMilliseconds(40));
        aggregator.Record(TimeSpan.FromMilliseconds(80));

        var summary = aggregator.BuildSummary();

        summary.SampleCount.Should().Be(3);
        summary.Minimum.Should().Be(TimeSpan.FromMilliseconds(20));
        summary.Maximum.Should().Be(TimeSpan.FromMilliseconds(80));
        summary.Mean.TotalMilliseconds.Should().BeApproximately(46.666, 0.01);
    }

    [Fact]
    public void Window_evicts_oldest_samples_past_capacity()
    {
        var aggregator = new NetSessionRttAggregator(3);
        aggregator.Record(TimeSpan.FromMilliseconds(10));
        aggregator.Record(TimeSpan.FromMilliseconds(20));
        aggregator.Record(TimeSpan.FromMilliseconds(30));
        aggregator.Record(TimeSpan.FromMilliseconds(100));

        var summary = aggregator.BuildSummary();

        summary.SampleCount.Should().Be(3);
        summary.Minimum.Should().Be(
            TimeSpan.FromMilliseconds(20),
            "the 10 ms sample should have aged out behind the ring-buffer write head.");
        summary.Maximum.Should().Be(TimeSpan.FromMilliseconds(100));
    }

    [Fact]
    public void Percentile95_returns_nearest_rank_sample()
    {
        var aggregator = new NetSessionRttAggregator(20);
        for (var i = 1; i <= 20; i++)
        {
            aggregator.Record(TimeSpan.FromMilliseconds(i));
        }

        var summary = aggregator.BuildSummary();

        summary.Percentile95.Should().Be(
            TimeSpan.FromMilliseconds(19),
            "nearest-rank P95 of {1..20} maps to ceil(0.95 * 20) - 1 = 18 → sample 19 ms.");
    }

    [Fact]
    public void Clear_resets_state_to_empty()
    {
        var aggregator = new NetSessionRttAggregator(4);
        aggregator.Record(TimeSpan.FromMilliseconds(10));
        aggregator.Record(TimeSpan.FromMilliseconds(20));

        aggregator.Clear();
        var summary = aggregator.BuildSummary();

        summary.SampleCount.Should().Be(0);
    }
}
