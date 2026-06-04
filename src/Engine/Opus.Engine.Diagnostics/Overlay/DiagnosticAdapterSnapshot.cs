using System;

namespace Opus.Engine.Diagnostics.Overlay;

/// <summary>Renderer adapter and render-target dimensions exposed to diagnostics UI.</summary>
public sealed record DiagnosticAdapterSnapshot(
    string AdapterName,
    int BackBufferWidth,
    int BackBufferHeight,
    int SceneViewportWidth,
    int SceneViewportHeight)
{
    /// <summary>Adapter hardware identity (vendor, ids, VRAM, class). Defaults to
    /// <see cref="DiagnosticAdapterHardware.Unknown"/> so callers that only have the
    /// adapter name and dimensions stay source-compatible; the live host populates it.</summary>
    public DiagnosticAdapterHardware Hardware { get; init; } = DiagnosticAdapterHardware.Unknown;

    /// <summary>Creates a validated adapter snapshot with no hardware identity.</summary>
    public static DiagnosticAdapterSnapshot Create(
        string adapterName,
        int backBufferWidth,
        int backBufferHeight,
        int sceneViewportWidth,
        int sceneViewportHeight)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(adapterName);
        if (backBufferWidth <= 0 || backBufferHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(backBufferWidth), "Back buffer dimensions must be positive.");
        }

        if (sceneViewportWidth <= 0 || sceneViewportHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sceneViewportWidth), "Scene viewport dimensions must be positive.");
        }

        return new DiagnosticAdapterSnapshot(
            adapterName,
            backBufferWidth,
            backBufferHeight,
            sceneViewportWidth,
            sceneViewportHeight);
    }

    /// <summary>Creates a validated adapter snapshot carrying the live adapter hardware
    /// identity.</summary>
    public static DiagnosticAdapterSnapshot Create(
        string adapterName,
        int backBufferWidth,
        int backBufferHeight,
        int sceneViewportWidth,
        int sceneViewportHeight,
        DiagnosticAdapterHardware hardware)
    {
        ArgumentNullException.ThrowIfNull(hardware);
        return Create(adapterName, backBufferWidth, backBufferHeight, sceneViewportWidth, sceneViewportHeight)
            with { Hardware = hardware };
    }
}
