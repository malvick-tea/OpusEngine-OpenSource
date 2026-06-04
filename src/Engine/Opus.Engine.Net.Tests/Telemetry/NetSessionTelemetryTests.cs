using System;
using FluentAssertions;
using Opus.Engine.Net.Session;
using Opus.Engine.Net.Telemetry;
using Opus.Engine.Net.Tests.Support;
using Xunit;

namespace Opus.Engine.Net.Tests.Telemetry;

/// <summary>Telemetry snapshot capture + formatting helpers.</summary>
public sealed class NetSessionTelemetryTests
{
    [Fact]
    public void Unconfigured_telemetry_is_Idle_and_zero()
    {
        var telemetry = NetSessionTelemetry.Unconfigured("ghost", DateTimeOffset.UtcNow);

        telemetry.State.Should().Be(NetSessionState.Idle);
        telemetry.Statistics.ConnectedPeerCount.Should().Be(0);
        telemetry.LastFault.Should().BeNull();
    }

    [Fact]
    public void Capture_reads_live_session_state()
    {
        using var factory = new LoopbackClientTransportFactory();
        using var session = NetSession.Client(
            new NetSessionOptions(NetSessionRole.Client, "ok", NetReconnectPolicy.Disabled),
            factory);
        session.Tick(TimeSpan.Zero);

        var telemetry = NetSessionTelemetry.Capture(session);

        telemetry.State.Should().Be(NetSessionState.Connected);
        telemetry.DisplayName.Should().Be("ok");
        telemetry.Statistics.ConnectedPeerCount.Should().Be(1);
    }

    [Fact]
    public void FormatStatusLine_contains_state_and_counters()
    {
        var telemetry = NetSessionTelemetry.Unconfigured("ghost", DateTimeOffset.UtcNow);

        var line = NetSessionTelemetryFormatter.FormatStatusLine(telemetry);

        line.Should().Contain("idle");
        line.Should().Contain("0 peers");
        line.Should().Contain("0 in / 0 out");
    }

    [Fact]
    public void FormatDetailLine_surfaces_fault_when_session_is_faulted()
    {
        var faulted = new NetSessionTelemetry(
            DisplayName: "broken",
            Role: NetSessionRole.Client,
            State: NetSessionState.Faulted,
            Statistics: NetSessionStatisticsSnapshot.Empty(DateTimeOffset.UtcNow),
            LastFault: NetSessionFault.FromDetail(
                NetSessionFaultCode.ReconnectBudgetExhausted,
                "no more retries",
                DateTimeOffset.UtcNow));

        var line = NetSessionTelemetryFormatter.FormatDetailLine(faulted);

        line.Should().Contain("ReconnectBudgetExhausted");
        line.Should().Contain("no more retries");
    }

    [Fact]
    public void FormatRttLine_reports_no_samples_when_window_empty()
    {
        var telemetry = NetSessionTelemetry.Unconfigured("ghost", DateTimeOffset.UtcNow);

        var line = NetSessionTelemetryFormatter.FormatRttLine(telemetry);

        line.Should().Be("rtt n=0");
    }

    [Fact]
    public void FormatRttLine_includes_mean_min_max_p95_when_samples_present()
    {
        using var factory = new LoopbackClientTransportFactory();
        using var session = NetSession.Client(
            new NetSessionOptions(NetSessionRole.Client, "ok", NetReconnectPolicy.Disabled),
            factory);
        session.RecordRtt(TimeSpan.FromMilliseconds(10));
        session.RecordRtt(TimeSpan.FromMilliseconds(40));
        var telemetry = NetSessionTelemetry.Capture(session);

        var line = NetSessionTelemetryFormatter.FormatRttLine(telemetry);

        line.Should().StartWith("rtt n=2");
        line.Should().Contain("mean=");
        line.Should().Contain("p95=");
        line.Should().Contain("min=10.0ms");
        line.Should().Contain("max=40.0ms");
    }

    [Fact]
    public void FormatRateLine_reports_zero_window_when_baseline_missing()
    {
        var telemetry = NetSessionTelemetry.Unconfigured("ghost", DateTimeOffset.UtcNow);

        var line = NetSessionTelemetryFormatter.FormatRateLine(telemetry);

        line.Should().Be("rate window=0.00s");
    }

    [Fact]
    public void FormatGuardLine_reports_zero_for_an_unconfigured_session()
    {
        var telemetry = NetSessionTelemetry.Unconfigured("ghost", DateTimeOffset.UtcNow);

        var line = NetSessionTelemetryFormatter.FormatGuardLine(telemetry);

        line.Should().Be("guards rejectedConn=0 droppedInbound=0 rateLimited=0");
    }

    [Fact]
    public void FormatGuardLine_reports_the_three_transport_guard_counters()
    {
        var snapshot = NetSessionStatisticsSnapshot.Empty(DateTimeOffset.UtcNow)
            with { TransportGuards = new NetTransportGuardCounts(3, 5, 7) };
        var telemetry = new NetSessionTelemetry(
            DisplayName: "srv",
            Role: NetSessionRole.Server,
            State: NetSessionState.Connected,
            Statistics: snapshot,
            LastFault: null);

        var line = NetSessionTelemetryFormatter.FormatGuardLine(telemetry);

        line.Should().Be("guards rejectedConn=3 droppedInbound=5 rateLimited=7");
    }

    [Fact]
    public void FormatEventLine_uses_diagnostic_code_when_present()
    {
        var sessionEvent = NetSessionEvent.ForPeer(
            NetSessionEventKind.PayloadReceived,
            new Opus.Net.Transport.ConnectionId(7UL),
            connectedPeerCount: 1,
            DateTimeOffset.UtcNow,
            payloadByteCount: 64);

        var line = NetSessionTelemetryFormatter.FormatEventLine(sessionEvent);

        line.Should().StartWith("OPDX-NET-???");
        line.Should().Contain("PayloadReceived");
        line.Should().Contain("bytes=64");
    }
}
