using System;
using System.Collections.Generic;
using Opus.Engine.Consumer.Lifecycle;
using Opus.Engine.Consumer.Scene;
using Opus.Engine.Consumer.Telemetry;
using Opus.Engine.Diagnostics.Overlay;
using Opus.Engine.Host.Windows.Direct3D12.Diagnostics;
using Opus.Engine.Net.Telemetry;
using Opus.Foundation;

namespace Opus.Engine.Host.Windows.Direct3D12;

public sealed unsafe partial class D3D12OpusApplication
{
    private ConsumerTelemetrySnapshot CaptureConsumerTelemetry()
        => _consumer.CaptureTelemetry(new ConsumerTelemetryContext(DateTimeOffset.UtcNow));

    /// <summary>Captures the consumer's current failure-report lines on demand, for the host
    /// to fold into a failure report at failure time. Returns an empty list when no consumer
    /// telemetry provider is registered; the bridge absorbs consumer-thrown exceptions, so
    /// this is safe to call from a crash/teardown path.</summary>
    public IReadOnlyList<string> CaptureConsumerFailureReportLines()
        => CaptureConsumerTelemetry().FailureReportLines;

    private DiagnosticNetworkSnapshot ResolveNetworkSnapshot(ConsumerTelemetrySnapshot consumerTelemetry)
    {
        if (consumerTelemetry.Network is not null)
        {
            // The full consumer telemetry snapshot is now consumed: network here, overlay
            // panels through BuildOverlayInputs (M11.10), and failure-report lines through
            // CaptureConsumerFailureReportLines (M11.11). The original M10 seam is closed.
            return D3D12NetTelemetryAdapter.ToNetworkSnapshot(consumerTelemetry.Network);
        }

        var provider = _options.NetTelemetryProvider;
        if (provider is null)
        {
            return D3D12NetTelemetryAdapter.NotConfigured;
        }

        NetSessionTelemetry? telemetry;
        try
        {
            telemetry = provider();
        }
        catch (Exception ex)
        {
            _log.Error("Network telemetry provider threw; falling back to NotConfigured.", ex);
            return D3D12NetTelemetryAdapter.NotConfigured;
        }

        return telemetry is null
            ? D3D12NetTelemetryAdapter.NotConfigured
            : D3D12NetTelemetryAdapter.ToNetworkSnapshot(telemetry);
    }

    private ConsumerSceneFrame? CaptureConsumerScene(ConsumerFrameContext frameContext)
    {
        if (!_consumer.HasSceneSource)
        {
            return null;
        }

        var plan = _rig.Plan;
        var viewport = new ConsumerViewportSnapshot(
            _session.SwapChain.Width,
            _session.SwapChain.Height,
            plan.SceneViewport.Width,
            plan.SceneViewport.Height);
        return _consumer.DescribeScene(new ConsumerSceneFrameContext(frameContext, viewport));
    }
}
