using System;
using System.Text;
using FluentAssertions;
using Opus.Engine.Net.Session;
using Opus.Engine.Net.Tests.Support;
using Xunit;

namespace Opus.Engine.Net.Tests.Session;

public sealed class NetSessionStatisticsRateTests
{
    [Fact]
    public void First_statistics_read_returns_empty_rate()
    {
        using var factory = new LoopbackClientTransportFactory();
        using var session = NetSession.Client(
            new NetSessionOptions(NetSessionRole.Client, "client-A", NetReconnectPolicy.Disabled),
            factory);

        var snapshot = session.Statistics;

        snapshot.Rate.Should().Be(NetSessionRateSnapshot.Empty);
    }

    [Fact]
    public void Successive_reads_compute_per_second_rate_from_send_traffic()
    {
        var time = new StubTime(new DateTimeOffset(2026, 5, 28, 12, 0, 0, TimeSpan.Zero));
        using var factory = new LoopbackClientTransportFactory();
        using var session = NetSession.Client(
            new NetSessionOptions(NetSessionRole.Client, "client-A", NetReconnectPolicy.Disabled),
            factory,
            time);
        session.Tick(TimeSpan.Zero);
        var first = session.Statistics;
        var link = factory.LastLink!;
        var payload = Encoding.UTF8.GetBytes("rate-probe");
        session.Send(link.ServerPeerId, payload);

        time.Advance(TimeSpan.FromSeconds(2));
        var second = session.Statistics;

        first.Rate.Should().Be(
            NetSessionRateSnapshot.Empty,
            "the first statistics read after Start establishes the baseline.");
        second.Rate.WindowDuration.Should().Be(TimeSpan.FromSeconds(2));
        second.Rate.PacketsSentPerSecond.Should().BeGreaterThan(0.0);
        second.Rate.BytesSentPerSecond.Should().Be(payload.Length / 2.0);
    }

    private sealed class StubTime : TimeProvider
    {
        private DateTimeOffset _now;

        public StubTime(DateTimeOffset start)
        {
            _now = start;
        }

        public void Advance(TimeSpan delta) => _now += delta;

        public override DateTimeOffset GetUtcNow() => _now;

        public override TimeZoneInfo LocalTimeZone => TimeZoneInfo.Utc;
    }
}
