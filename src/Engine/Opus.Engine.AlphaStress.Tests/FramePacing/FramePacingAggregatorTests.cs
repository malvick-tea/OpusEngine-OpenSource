using System;
using FluentAssertions;
using Opus.Engine.AlphaStress.FramePacing;
using Xunit;

namespace Opus.Engine.AlphaStress.Tests.FramePacing;

public sealed class FramePacingAggregatorTests
{
    [Fact]
    public void Ctor_rejects_non_positive_hitch_threshold()
    {
        var act = () => new FramePacingAggregator(TimeSpan.Zero);

        act.Should().Throw<ArgumentOutOfRangeException>().WithParameterName("hitchThreshold");
    }

    [Fact]
    public void Empty_aggregator_returns_empty_summary()
    {
        var aggregator = new FramePacingAggregator(TimeSpan.FromMilliseconds(33));

        var summary = aggregator.BuildSummary();

        summary.HasSamples.Should().BeFalse();
        summary.SampleCount.Should().Be(0);
        summary.HitchThreshold.Should().Be(TimeSpan.FromMilliseconds(33));
    }

    [Fact]
    public void Record_rejects_non_increasing_frame_numbers()
    {
        var aggregator = new FramePacingAggregator(TimeSpan.FromMilliseconds(33));
        aggregator.Record(new FramePacingObservation(5, DateTimeOffset.UtcNow, TimeSpan.FromMilliseconds(10)));

        var act = () => aggregator.Record(new FramePacingObservation(5, DateTimeOffset.UtcNow, TimeSpan.FromMilliseconds(10)));

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Record_null_throws()
    {
        var aggregator = new FramePacingAggregator(TimeSpan.FromMilliseconds(33));

        var act = () => aggregator.Record(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Build_summary_computes_nearest_rank_percentiles()
    {
        var aggregator = new FramePacingAggregator(TimeSpan.FromMilliseconds(100));
        var baseTime = DateTimeOffset.UtcNow;
        for (var i = 1; i <= 100; i++)
        {
            aggregator.Record(new FramePacingObservation(i, baseTime, TimeSpan.FromMilliseconds(i)));
        }

        var summary = aggregator.BuildSummary();

        summary.SampleCount.Should().Be(100);
        summary.Median.Should().Be(TimeSpan.FromMilliseconds(50));
        summary.Percentile95.Should().Be(TimeSpan.FromMilliseconds(95));
        summary.Percentile99.Should().Be(TimeSpan.FromMilliseconds(99));
        summary.Max.Should().Be(TimeSpan.FromMilliseconds(100));
        summary.Mean.Should().BeCloseTo(TimeSpan.FromMilliseconds(50.5), TimeSpan.FromMilliseconds(1));
    }

    [Fact]
    public void Build_summary_counts_hitches_at_or_above_threshold()
    {
        var aggregator = new FramePacingAggregator(TimeSpan.FromMilliseconds(50));
        var baseTime = DateTimeOffset.UtcNow;
        aggregator.Record(new FramePacingObservation(1, baseTime, TimeSpan.FromMilliseconds(10)));
        aggregator.Record(new FramePacingObservation(2, baseTime, TimeSpan.FromMilliseconds(49)));
        aggregator.Record(new FramePacingObservation(3, baseTime, TimeSpan.FromMilliseconds(50)));
        aggregator.Record(new FramePacingObservation(4, baseTime, TimeSpan.FromMilliseconds(51)));
        aggregator.Record(new FramePacingObservation(5, baseTime, TimeSpan.FromMilliseconds(200)));

        var summary = aggregator.BuildSummary();

        summary.HitchCount.Should().Be(3);
    }

    [Fact]
    public void Build_summary_tracks_running_max()
    {
        var aggregator = new FramePacingAggregator(TimeSpan.FromMilliseconds(50));
        var baseTime = DateTimeOffset.UtcNow;
        aggregator.Record(new FramePacingObservation(1, baseTime, TimeSpan.FromMilliseconds(10)));
        aggregator.Record(new FramePacingObservation(2, baseTime, TimeSpan.FromMilliseconds(200)));
        aggregator.Record(new FramePacingObservation(3, baseTime, TimeSpan.FromMilliseconds(20)));

        var summary = aggregator.BuildSummary();

        summary.Max.Should().Be(TimeSpan.FromMilliseconds(200));
    }

    [Fact]
    public void Single_sample_summary_returns_that_sample_for_every_percentile()
    {
        var aggregator = new FramePacingAggregator(TimeSpan.FromMilliseconds(100));
        aggregator.Record(new FramePacingObservation(1, DateTimeOffset.UtcNow, TimeSpan.FromMilliseconds(42)));

        var summary = aggregator.BuildSummary();

        summary.Median.Should().Be(TimeSpan.FromMilliseconds(42));
        summary.Percentile95.Should().Be(TimeSpan.FromMilliseconds(42));
        summary.Percentile99.Should().Be(TimeSpan.FromMilliseconds(42));
        summary.Mean.Should().Be(TimeSpan.FromMilliseconds(42));
        summary.Max.Should().Be(TimeSpan.FromMilliseconds(42));
        summary.SampleCount.Should().Be(1);
    }
}
