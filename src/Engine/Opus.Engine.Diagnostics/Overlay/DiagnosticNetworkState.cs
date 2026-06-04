namespace Opus.Engine.Diagnostics.Overlay;

/// <summary>Renderer-neutral network state exposed to diagnostic overlays.</summary>
public enum DiagnosticNetworkState
{
    Unavailable = 0,
    Disconnected = 1,
    Connecting = 2,
    Connected = 3,
    Degraded = 4,
}
