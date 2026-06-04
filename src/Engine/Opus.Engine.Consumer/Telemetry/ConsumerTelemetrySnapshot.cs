using System;
using Opus.Engine.Diagnostics.Overlay;
using Opus.Engine.Net.Telemetry;

namespace Opus.Engine.Consumer.Telemetry;

/// <summary>
/// Engine-neutral telemetry snapshot supplied by a consumer. Hosts may use the network
/// telemetry immediately and can fold extra diagnostic panels or report lines into
/// richer evidence bundles as the alpha surface hardens.
/// </summary>
public sealed record ConsumerTelemetrySnapshot
{
    /// <summary>Creates a telemetry snapshot.</summary>
    public ConsumerTelemetrySnapshot(
        NetSessionTelemetry? network,
        IReadOnlyList<DiagnosticPanel> overlayPanels,
        IReadOnlyList<string> failureReportLines)
    {
        Network = network;
        OverlayPanels = ConsumerContractValidation.CopyRequiredList(overlayPanels, nameof(overlayPanels));
        FailureReportLines = ConsumerContractValidation.CopyRequiredStringList(failureReportLines, nameof(failureReportLines));
    }

    /// <summary>Empty telemetry snapshot.</summary>
    public static ConsumerTelemetrySnapshot Empty { get; } = new(
        network: null,
        overlayPanels: Array.Empty<DiagnosticPanel>(),
        failureReportLines: Array.Empty<string>());

    /// <summary>Optional M8 network telemetry extended into the consumer slot.</summary>
    public NetSessionTelemetry? Network { get; }

    /// <summary>Optional renderer-neutral overlay panels reserved for host composers.</summary>
    public IReadOnlyList<DiagnosticPanel> OverlayPanels { get; }

    /// <summary>Optional text lines that future failure reports can attach.</summary>
    public IReadOnlyList<string> FailureReportLines { get; }
}
