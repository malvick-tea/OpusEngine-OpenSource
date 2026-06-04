using System;
using FluentAssertions;
using Opus.Engine.Diagnostics.Reports;
using Opus.Foundation;
using Xunit;

namespace Opus.Engine.Diagnostics.Tests.Reports;

public sealed class FailureReportClassifierTests
{
    [Fact]
    public void Null_exception_is_a_crash()
    {
        FailureReportClassifier.Classify(null).Should().Be(FailureReportKind.Crash);
    }

    [Fact]
    public void Unrecognised_exception_is_a_crash()
    {
        FailureReportClassifier.Classify(new InvalidOperationException("boom"))
            .Should().Be(FailureReportKind.Crash);
    }

    [Fact]
    public void Direct_device_lost_is_device_lost()
    {
        FailureReportClassifier.Classify(new EngineDeviceLostException("gpu gone"))
            .Should().Be(FailureReportKind.DeviceLost);
    }

    [Fact]
    public void Direct_content_failure_is_content_failure()
    {
        FailureReportClassifier.Classify(new EngineContentException("bad glb"))
            .Should().Be(FailureReportKind.ContentFailure);
    }

    [Fact]
    public void Device_lost_wrapped_as_inner_is_device_lost()
    {
        var wrapped = new InvalidOperationException(
            "frame failed", new EngineDeviceLostException("gpu gone"));

        FailureReportClassifier.Classify(wrapped).Should().Be(FailureReportKind.DeviceLost);
    }

    [Fact]
    public void Content_failure_wrapped_as_inner_is_content_failure()
    {
        var wrapped = new InvalidOperationException(
            "load failed", new EngineContentException("bad glb", "tank.glb"));

        FailureReportClassifier.Classify(wrapped).Should().Be(FailureReportKind.ContentFailure);
    }

    [Fact]
    public void Aggregate_containing_device_lost_is_device_lost()
    {
        var aggregate = new AggregateException(
            new InvalidOperationException("sibling"),
            new EngineDeviceLostException("gpu gone"));

        FailureReportClassifier.Classify(aggregate).Should().Be(FailureReportKind.DeviceLost);
    }

    [Fact]
    public void Aggregate_containing_content_failure_is_content_failure()
    {
        var aggregate = new AggregateException(
            new InvalidOperationException("sibling"),
            new EngineContentException("bad asset"));

        FailureReportClassifier.Classify(aggregate).Should().Be(FailureReportKind.ContentFailure);
    }

    [Fact]
    public void Device_lost_outranks_content_failure_when_both_present()
    {
        var aggregate = new AggregateException(
            new EngineContentException("bad asset"),
            new EngineDeviceLostException("gpu gone"));

        FailureReportClassifier.Classify(aggregate).Should().Be(FailureReportKind.DeviceLost);
    }

    [Fact]
    public void Content_wrapping_a_device_lost_inner_still_reports_device_lost()
    {
        var outer = new EngineContentException(
            "content load tripped a device loss",
            "tank.glb",
            new EngineDeviceLostException("gpu gone"));

        FailureReportClassifier.Classify(outer).Should().Be(FailureReportKind.DeviceLost);
    }

    [Fact]
    public void Failure_buried_below_the_chain_depth_cap_is_not_reached()
    {
        Exception current = new EngineDeviceLostException("very deep");
        for (var i = 0; i < FailureReportExceptionInfo.MaxChainDepth + 5; i++)
        {
            current = new InvalidOperationException("layer-" + i, current);
        }

        FailureReportClassifier.Classify(current).Should().Be(FailureReportKind.Crash);
    }

    [Fact]
    public void Failure_within_the_chain_depth_cap_is_found()
    {
        Exception current = new EngineContentException("shallow");
        for (var i = 0; i < 5; i++)
        {
            current = new InvalidOperationException("layer-" + i, current);
        }

        FailureReportClassifier.Classify(current).Should().Be(FailureReportKind.ContentFailure);
    }
}
