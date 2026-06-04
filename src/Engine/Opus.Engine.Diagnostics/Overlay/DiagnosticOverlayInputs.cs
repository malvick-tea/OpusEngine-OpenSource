using System;
using System.Collections.Generic;
using System.Linq;
using Opus.Foundation;

namespace Opus.Engine.Diagnostics.Overlay;

/// <summary>Complete renderer-neutral input bundle used to compose an overlay snapshot.</summary>
public sealed record DiagnosticOverlayInputs(
    BuildInfo Build,
    DiagnosticFrameMetrics FrameMetrics,
    DiagnosticAdapterSnapshot Adapter,
    DiagnosticContentSnapshot Content,
    DiagnosticNetworkSnapshot Network,
    string? LastScreenshotPath,
    DateTimeOffset CapturedAtUtc)
{
    /// <summary>Extra renderer-neutral panels supplied by a consumer integration. The
    /// composer appends them after the engine panels at the full overlay level. Defaults to
    /// empty and is never null.</summary>
    public IReadOnlyList<DiagnosticPanel> ConsumerPanels { get; init; } = Array.Empty<DiagnosticPanel>();

    /// <summary>Creates a validated input bundle.</summary>
    public static DiagnosticOverlayInputs Create(
        BuildInfo build,
        DiagnosticFrameMetrics frameMetrics,
        DiagnosticAdapterSnapshot adapter,
        DiagnosticContentSnapshot content,
        DiagnosticNetworkSnapshot network,
        string? lastScreenshotPath,
        DateTimeOffset capturedAtUtc,
        IReadOnlyList<DiagnosticPanel>? consumerPanels = null)
    {
        ArgumentNullException.ThrowIfNull(build);
        ArgumentNullException.ThrowIfNull(frameMetrics);
        ArgumentNullException.ThrowIfNull(adapter);
        ArgumentNullException.ThrowIfNull(content);
        ArgumentNullException.ThrowIfNull(network);
        return new DiagnosticOverlayInputs(
            build,
            frameMetrics,
            adapter,
            content,
            network,
            lastScreenshotPath,
            capturedAtUtc.ToUniversalTime())
        {
            ConsumerPanels = consumerPanels is null
                ? Array.Empty<DiagnosticPanel>()
                : consumerPanels.Where(static panel => panel is not null).ToArray(),
        };
    }
}
