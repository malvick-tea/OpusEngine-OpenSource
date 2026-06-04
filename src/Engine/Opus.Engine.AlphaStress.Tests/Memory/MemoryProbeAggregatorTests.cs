using System;
using FluentAssertions;
using Opus.Engine.AlphaStress.Memory;
using Xunit;

namespace Opus.Engine.AlphaStress.Tests.Memory;

public sealed class MemoryProbeAggregatorTests
{
    [Fact]
    public void Empty_aggregator_returns_empty_summary()
    {
        var aggregator = new MemoryProbeAggregator();

        var summary = aggregator.BuildSummary();

        summary.SampleCount.Should().Be(0);
        summary.HasSamples.Should().BeFalse();
        summary.First.Should().BeNull();
        summary.Last.Should().BeNull();
        summary.ManagedHeapGrowthBytes.Should().Be(0);
        summary.WorkingSetGrowthBytes.Should().Be(0);
    }

    [Fact]
    public void Record_null_throws()
    {
        var aggregator = new MemoryProbeAggregator();

        var act = () => aggregator.Record(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Record_rejects_backwards_timestamps()
    {
        var aggregator = new MemoryProbeAggregator();
        var baseTime = DateTimeOffset.UtcNow;
        aggregator.Record(BuildSample(baseTime, 1000, 2000));

        var act = () => aggregator.Record(BuildSample(baseTime - TimeSpan.FromSeconds(1), 1000, 2000));

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Build_summary_computes_positive_growth_delta()
    {
        var aggregator = new MemoryProbeAggregator();
        var baseTime = DateTimeOffset.UtcNow;
        aggregator.Record(BuildSample(baseTime, managed: 1024, workingSet: 4096));
        aggregator.Record(BuildSample(baseTime.AddSeconds(1), managed: 4096, workingSet: 8192));

        var summary = aggregator.BuildSummary();

        summary.ManagedHeapGrowthBytes.Should().Be(3072);
        summary.WorkingSetGrowthBytes.Should().Be(4096);
        summary.PeakManagedHeapBytes.Should().Be(4096);
        summary.PeakWorkingSetBytes.Should().Be(8192);
        summary.SampleCount.Should().Be(2);
        summary.First!.ManagedHeapBytes.Should().Be(1024);
        summary.Last!.ManagedHeapBytes.Should().Be(4096);
    }

    [Fact]
    public void Build_summary_clamps_negative_growth_to_zero()
    {
        var aggregator = new MemoryProbeAggregator();
        var baseTime = DateTimeOffset.UtcNow;
        aggregator.Record(BuildSample(baseTime, managed: 8192, workingSet: 16384));
        aggregator.Record(BuildSample(baseTime.AddSeconds(1), managed: 2048, workingSet: 4096));

        var summary = aggregator.BuildSummary();

        summary.ManagedHeapGrowthBytes.Should().Be(0);
        summary.WorkingSetGrowthBytes.Should().Be(0);
    }

    [Fact]
    public void Build_summary_tracks_peak_across_full_history()
    {
        var aggregator = new MemoryProbeAggregator();
        var baseTime = DateTimeOffset.UtcNow;
        aggregator.Record(BuildSample(baseTime, managed: 1024, workingSet: 4096));
        aggregator.Record(BuildSample(baseTime.AddSeconds(1), managed: 16384, workingSet: 32768));
        aggregator.Record(BuildSample(baseTime.AddSeconds(2), managed: 2048, workingSet: 8192));

        var summary = aggregator.BuildSummary();

        summary.PeakManagedHeapBytes.Should().Be(16384);
        summary.PeakWorkingSetBytes.Should().Be(32768);
        summary.Last!.ManagedHeapBytes.Should().Be(2048);
    }

    [Fact]
    public void Build_summary_computes_gen2_delta()
    {
        var aggregator = new MemoryProbeAggregator();
        var baseTime = DateTimeOffset.UtcNow;
        aggregator.Record(BuildSample(baseTime, gen2: 5));
        aggregator.Record(BuildSample(baseTime.AddSeconds(1), gen2: 9));

        var summary = aggregator.BuildSummary();

        summary.Gen2CollectionsDelta.Should().Be(4);
    }

    private static MemoryProbeSample BuildSample(
        DateTimeOffset observedAtUtc,
        long managed = 1024,
        long workingSet = 4096,
        int gen2 = 0) => new(
        ObservedAtUtc: observedAtUtc,
        ManagedHeapBytes: managed,
        WorkingSetBytes: workingSet,
        Gen0Collections: 0,
        Gen1Collections: 0,
        Gen2Collections: gen2);
}
