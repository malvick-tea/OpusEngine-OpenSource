using System;

namespace Opus.Engine.Consumer.Scene;

/// <summary>Host viewport dimensions exposed to engine-neutral consumer scene sources.</summary>
public readonly record struct ConsumerViewportSnapshot
{
    /// <summary>Creates a viewport snapshot.</summary>
    public ConsumerViewportSnapshot(
        int backBufferWidth,
        int backBufferHeight,
        int sceneViewportWidth,
        int sceneViewportHeight)
    {
        if (backBufferWidth <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(backBufferWidth), "Back-buffer width must be positive.");
        }

        if (backBufferHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(backBufferHeight), "Back-buffer height must be positive.");
        }

        if (sceneViewportWidth <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sceneViewportWidth), "Scene viewport width must be positive.");
        }

        if (sceneViewportHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sceneViewportHeight), "Scene viewport height must be positive.");
        }

        BackBufferWidth = backBufferWidth;
        BackBufferHeight = backBufferHeight;
        SceneViewportWidth = sceneViewportWidth;
        SceneViewportHeight = sceneViewportHeight;
    }

    /// <summary>Swap-chain back-buffer width in pixels.</summary>
    public int BackBufferWidth { get; }

    /// <summary>Swap-chain back-buffer height in pixels.</summary>
    public int BackBufferHeight { get; }

    /// <summary>Offscreen scene viewport width in pixels.</summary>
    public int SceneViewportWidth { get; }

    /// <summary>Offscreen scene viewport height in pixels.</summary>
    public int SceneViewportHeight { get; }
}
