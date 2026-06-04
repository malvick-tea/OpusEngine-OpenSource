using System;
using FluentAssertions;
using Opus.Engine.Net.Session;
using Opus.Engine.Net.Tests.Support;
using Xunit;

namespace Opus.Engine.Net.Tests.Session;

public sealed class NetSessionStatisticsRttTests
{
    [Fact]
    public void Default_session_exposes_empty_rtt_summary()
    {
        using var factory = new LoopbackClientTransportFactory();
        using var session = NetSession.Client(
            new NetSessionOptions(NetSessionRole.Client, "client-A", NetReconnectPolicy.Disabled),
            factory);

        session.Statistics.Rtt.SampleCount.Should().Be(0);
        session.Statistics.Rtt.WindowCapacity.Should().Be(NetSessionStatistics.DefaultRttWindowCapacity);
        session.Statistics.Rtt.Mean.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void RecordRtt_feeds_rolling_window_surfaced_in_snapshot()
    {
        using var factory = new LoopbackClientTransportFactory();
        using var session = NetSession.Client(
            new NetSessionOptions(NetSessionRole.Client, "client-A", NetReconnectPolicy.Disabled),
            factory);

        session.RecordRtt(TimeSpan.FromMilliseconds(15));
        session.RecordRtt(TimeSpan.FromMilliseconds(25));
        session.RecordRtt(TimeSpan.FromMilliseconds(50));

        var summary = session.Statistics.Rtt;
        summary.SampleCount.Should().Be(3);
        summary.Minimum.Should().Be(TimeSpan.FromMilliseconds(15));
        summary.Maximum.Should().Be(TimeSpan.FromMilliseconds(50));
        summary.Mean.TotalMilliseconds.Should().BeApproximately(30.0, 0.01);
    }

    [Fact]
    public void RecordRtt_rejects_negative_observation()
    {
        using var factory = new LoopbackClientTransportFactory();
        using var session = NetSession.Client(
            new NetSessionOptions(NetSessionRole.Client, "client-A", NetReconnectPolicy.Disabled),
            factory);

        var act = () => session.RecordRtt(TimeSpan.FromMilliseconds(-1));

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    [Fact]
    public void Empty_snapshot_helper_exposes_window_capacity_constant()
    {
        var capturedAt = new DateTimeOffset(2026, 5, 28, 12, 0, 0, TimeSpan.Zero);

        var snapshot = NetSessionStatisticsSnapshot.Empty(capturedAt);

        snapshot.Rtt.WindowCapacity.Should().Be(NetSessionStatistics.DefaultRttWindowCapacity);
        snapshot.Rate.Should().Be(NetSessionRateSnapshot.Empty);
    }
}
