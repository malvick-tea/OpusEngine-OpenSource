using System;
using FluentAssertions;
using Opus.Engine.Diagnostics.Reports;
using Opus.Foundation;
using Xunit;

namespace Opus.Engine.Diagnostics.Tests.Reports;

public sealed class FailureReportTests
{
    [Fact]
    public void Capture_flattens_exception_chain_and_preserves_evidence()
    {
        var exception = new InvalidOperationException(
            "outer",
            new FormatException("inner"));

        var report = FailureReport.Capture(
            FailureReportKind.Crash,
            new DateTimeOffset(2026, 5, 26, 12, 30, 0, TimeSpan.Zero),
            BuildInfo.Current,
            FailureReportAdapterSnapshot.Create("Test Adapter", 1280, 720),
            new[] { "first", "second" },
            "capture.png",
            exception);

        report.Kind.Should().Be(FailureReportKind.Crash);
        report.Adapter.AdapterName.Should().Be("Test Adapter");
        report.LastLogLines.Should().Equal("first", "second");
        report.ScreenshotPath.Should().Be("capture.png");
        report.ExceptionChain.Should().HaveCount(2);
        report.ExceptionChain[0].Message.Should().Be("outer");
        report.ExceptionChain[1].Message.Should().Be("inner");
    }

    [Fact]
    public void Capture_uses_explicit_unavailable_adapter_for_pre_renderer_failures()
    {
        var report = FailureReport.Capture(
            FailureReportKind.StartupFailure,
            DateTimeOffset.UtcNow,
            BuildInfo.Current,
            FailureReportAdapterSnapshot.Unavailable,
            Array.Empty<string>(),
            screenshotPath: null,
            exception: null);

        report.Adapter.Should().BeSameAs(FailureReportAdapterSnapshot.Unavailable);
        report.ExceptionChain.Should().BeEmpty();
        report.ScreenshotPath.Should().BeNull();
    }

    [Fact]
    public void Capture_rejects_null_adapter()
    {
        var act = () => FailureReport.Capture(
            FailureReportKind.StartupFailure,
            DateTimeOffset.UtcNow,
            BuildInfo.Current,
            adapter: null!,
            Array.Empty<string>(),
            screenshotPath: null,
            exception: null);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void Capture_normalises_whitespace_screenshot_path_to_null()
    {
        var report = FailureReport.Capture(
            FailureReportKind.StartupFailure,
            DateTimeOffset.UtcNow,
            BuildInfo.Current,
            FailureReportAdapterSnapshot.Unavailable,
            Array.Empty<string>(),
            screenshotPath: "   ",
            exception: null);

        report.ScreenshotPath.Should().BeNull();
    }

    [Fact]
    public void Capture_flattens_aggregate_exception_inner_branches()
    {
        var aggregate = new AggregateException(
            "two faults",
            new InvalidOperationException("first"),
            new FormatException("second"));

        var report = FailureReport.Capture(
            FailureReportKind.Crash,
            DateTimeOffset.UtcNow,
            BuildInfo.Current,
            FailureReportAdapterSnapshot.Unavailable,
            Array.Empty<string>(),
            screenshotPath: null,
            exception: aggregate);

        report.ExceptionChain.Should().HaveCountGreaterThanOrEqualTo(3);
        report.ExceptionChain[0].Type.Should().Contain("AggregateException");
        report.ExceptionChain.Should().Contain(entry => entry.Message == "first");
        report.ExceptionChain.Should().Contain(entry => entry.Message == "second");
    }

    [Fact]
    public void Capture_caps_chain_depth_against_pathological_input()
    {
        var deepest = new InvalidOperationException("leaf");
        Exception current = deepest;
        for (var i = 0; i < FailureReportExceptionInfo.MaxChainDepth + 50; i++)
        {
            current = new InvalidOperationException("wrap-" + i, current);
        }

        var report = FailureReport.Capture(
            FailureReportKind.Crash,
            DateTimeOffset.UtcNow,
            BuildInfo.Current,
            FailureReportAdapterSnapshot.Unavailable,
            Array.Empty<string>(),
            screenshotPath: null,
            exception: current);

        report.ExceptionChain.Count.Should().BeLessThanOrEqualTo(FailureReportExceptionInfo.MaxChainDepth);
    }

    [Fact]
    public void Capture_filters_null_log_lines()
    {
        var report = FailureReport.Capture(
            FailureReportKind.Crash,
            DateTimeOffset.UtcNow,
            BuildInfo.Current,
            FailureReportAdapterSnapshot.Unavailable,
            new[] { "first", null!, "second" },
            screenshotPath: null,
            exception: null);

        report.LastLogLines.Should().Equal("first", "second");
    }

    [Fact]
    public void Capture_defaults_consumer_lines_to_empty_not_null()
    {
        var report = FailureReport.Capture(
            FailureReportKind.Crash,
            DateTimeOffset.UtcNow,
            BuildInfo.Current,
            FailureReportAdapterSnapshot.Unavailable,
            Array.Empty<string>(),
            screenshotPath: null,
            exception: null);

        report.ConsumerLines.Should().NotBeNull();
        report.ConsumerLines.Should().BeEmpty();
    }

    [Fact]
    public void Capture_carries_consumer_lines()
    {
        var report = FailureReport.Capture(
            FailureReportKind.Crash,
            DateTimeOffset.UtcNow,
            BuildInfo.Current,
            FailureReportAdapterSnapshot.Unavailable,
            Array.Empty<string>(),
            screenshotPath: null,
            exception: null,
            consumerLines: new[] { "match=skirmish-7", "phase=briefing" });

        report.ConsumerLines.Should().Equal("match=skirmish-7", "phase=briefing");
    }

    [Fact]
    public void Capture_filters_null_consumer_lines()
    {
        var report = FailureReport.Capture(
            FailureReportKind.Crash,
            DateTimeOffset.UtcNow,
            BuildInfo.Current,
            FailureReportAdapterSnapshot.Unavailable,
            Array.Empty<string>(),
            screenshotPath: null,
            exception: null,
            consumerLines: new[] { "kept-1", null!, "kept-2" });

        report.ConsumerLines.Should().Equal("kept-1", "kept-2");
    }
}
