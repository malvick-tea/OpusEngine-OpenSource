using System;
using System.IO;
using FluentAssertions;
using Opus.Engine.Diagnostics.Reports;
using Opus.Foundation;
using Xunit;

namespace Opus.Engine.Diagnostics.Tests.Reports;

public sealed class FailureReportNetworkSnapshotTests
{
    [Fact]
    public void Failure_report_default_network_is_null()
    {
        var report = NewReport(network: null);

        report.Network.Should().BeNull();
    }

    [Fact]
    public void Failure_report_carries_supplied_network_snapshot()
    {
        var network = BuildSnapshot();

        var report = NewReport(network);

        report.Network.Should().BeSameAs(network);
    }

    [Fact]
    public void Writer_emits_none_label_when_network_is_null()
    {
        using var temp = TempDirectory.Create();
        var writer = new FailureReportWriter(new FailureReportWriterOptions(temp.Path));

        var result = writer.Write(NewReport(network: null));
        var text = File.ReadAllText(result.TextPath!);

        text.Should().Contain("network:");
        text.Should().MatchRegex(@"network:\s*\r?\n\s+none\b");
    }

    [Fact]
    public void Writer_emits_network_block_when_snapshot_populated()
    {
        using var temp = TempDirectory.Create();
        var writer = new FailureReportWriter(new FailureReportWriterOptions(temp.Path));
        var network = BuildSnapshot();

        var result = writer.Write(NewReport(network));
        var text = File.ReadAllText(result.TextPath!);

        text.Should().Contain("session: opus-client");
        text.Should().Contain("role: client");
        text.Should().Contain("state: connected");
        text.Should().Contain("connectedPeers: 4");
        text.Should().Contain("packetsIn: 120");
        text.Should().Contain("rttMeanMs: 12.50");
        text.Should().Contain("packetsInPerSec: 30.00");
        text.Should().Contain("rateWindowMs: 2000.00");
    }

    [Fact]
    public void Writer_emits_transport_guard_rows_for_populated_snapshot()
    {
        using var temp = TempDirectory.Create();
        var writer = new FailureReportWriter(new FailureReportWriterOptions(temp.Path));

        var result = writer.Write(NewReport(BuildSnapshot()));
        var text = File.ReadAllText(result.TextPath!);

        // The DoS-guard counters are unconditional rows (unlike the fault lines), so a tester always
        // sees whether the peer cap, inbound-queue cap, or per-peer rate limiter shed anything.
        text.Should().Contain("rejectedConnections: 3");
        text.Should().Contain("droppedInbound: 5");
        text.Should().Contain("rateLimited: 7");
    }

    [Fact]
    public void Writer_omits_fault_lines_when_no_fault_present()
    {
        using var temp = TempDirectory.Create();
        var writer = new FailureReportWriter(new FailureReportWriterOptions(temp.Path));

        var result = writer.Write(NewReport(BuildSnapshot()));
        var text = File.ReadAllText(result.TextPath!);

        text.Should().NotContain("lastFaultCode");
        text.Should().NotContain("lastFaultDetail");
    }

    [Fact]
    public void Writer_emits_fault_lines_when_snapshot_records_fault()
    {
        using var temp = TempDirectory.Create();
        var writer = new FailureReportWriter(new FailureReportWriterOptions(temp.Path));
        var network = BuildSnapshot() with
        {
            LastFaultCode = "TransportException",
            LastFaultDetail = "Transport.Send threw.",
        };

        var result = writer.Write(NewReport(network));
        var text = File.ReadAllText(result.TextPath!);

        text.Should().Contain("lastFaultCode: TransportException");
        text.Should().Contain("lastFaultDetail: Transport.Send threw.");
    }

    private static FailureReportNetworkSnapshot BuildSnapshot() => new(
        DisplayName: "opus-client",
        Role: "client",
        State: "connected",
        ConnectedPeerCount: 4,
        PacketsReceived: 120,
        PacketsSent: 80,
        PacketsSendDropped: 2,
        BytesReceived: 6400,
        BytesSent: 4800,
        ReconnectAttempts: 1,
        QueuedPayloadsDropped: 0,
        RejectedConnections: 3,
        DroppedInboundPayloads: 5,
        RateLimitedInboundPayloads: 7,
        RttSampleCount: 12,
        RttMean: TimeSpan.FromMilliseconds(12.5),
        RttP95: TimeSpan.FromMilliseconds(25.0),
        PacketsReceivedPerSecond: 30.0,
        PacketsSentPerSecond: 20.0,
        BytesReceivedPerSecond: 1600.0,
        BytesSentPerSecond: 1200.0,
        RateWindow: TimeSpan.FromSeconds(2),
        LastFaultCode: null,
        LastFaultDetail: null);

    private static FailureReport NewReport(FailureReportNetworkSnapshot? network) => FailureReport.Capture(
        FailureReportKind.Crash,
        new DateTimeOffset(2026, 5, 28, 12, 0, 0, TimeSpan.Zero),
        BuildInfo.Current,
        FailureReportAdapterSnapshot.Unavailable,
        new[] { "last line" },
        screenshotPath: null,
        exception: new InvalidOperationException("boom"),
        network: network);

    private sealed class TempDirectory : IDisposable
    {
        private TempDirectory(string path)
        {
            Path = path;
        }

        public string Path { get; }

        public static TempDirectory Create()
        {
            var path = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "opus-failure-network-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return new TempDirectory(path);
        }

        public void Dispose()
        {
            if (Directory.Exists(Path))
            {
                Directory.Delete(Path, recursive: true);
            }
        }
    }
}
