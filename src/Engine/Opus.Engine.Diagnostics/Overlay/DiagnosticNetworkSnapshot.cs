using System;

namespace Opus.Engine.Diagnostics.Overlay;

/// <summary>Network-status hook for diagnostics overlays without depending on Net assemblies.</summary>
public sealed record DiagnosticNetworkSnapshot(DiagnosticNetworkState State, string Detail)
{
    /// <summary>Default network state for hosts that have not wired a runtime network hook.</summary>
    public static DiagnosticNetworkSnapshot NotConfigured { get; } = new(
        DiagnosticNetworkState.Unavailable,
        "not configured");

    /// <summary>Creates a validated network snapshot.</summary>
    public static DiagnosticNetworkSnapshot Create(DiagnosticNetworkState state, string detail)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(detail);
        return new DiagnosticNetworkSnapshot(state, detail);
    }
}
