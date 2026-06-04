namespace Opus.Engine.Consumer.Telemetry;

/// <summary>
/// Engine-neutral telemetry provider for external consumers. This extends the M8
/// network telemetry hook without making the engine own a consumer network session.
/// </summary>
public interface IConsumerTelemetryProvider
{
    /// <summary>Captures the latest consumer telemetry for overlay and report surfaces.</summary>
    ConsumerTelemetrySnapshot CaptureTelemetry(ConsumerTelemetryContext context);
}
