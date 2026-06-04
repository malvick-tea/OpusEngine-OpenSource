using System;

namespace Opus.Engine.Consumer.Telemetry;

/// <summary>Context delivered when a host asks a consumer for overlay or report telemetry.</summary>
public sealed record ConsumerTelemetryContext
{
    /// <summary>Creates a telemetry context.</summary>
    public ConsumerTelemetryContext(DateTimeOffset capturedAtUtc)
    {
        CapturedAtUtc = capturedAtUtc.ToUniversalTime();
    }

    /// <summary>UTC timestamp when telemetry was requested.</summary>
    public DateTimeOffset CapturedAtUtc { get; }
}
