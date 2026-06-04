using System;
using Opus.Engine.Consumer.Lifecycle;

namespace Opus.Engine.Consumer.Scene;

/// <summary>Context delivered when a host asks a consumer scene source for a frame.</summary>
public sealed record ConsumerSceneFrameContext
{
    /// <summary>Creates a scene-frame context.</summary>
    public ConsumerSceneFrameContext(ConsumerFrameContext frame, ConsumerViewportSnapshot viewport)
    {
        Frame = frame ?? throw new ArgumentNullException(nameof(frame));
        Viewport = viewport;
    }

    /// <summary>Per-frame host timing.</summary>
    public ConsumerFrameContext Frame { get; }

    /// <summary>Current host viewport dimensions.</summary>
    public ConsumerViewportSnapshot Viewport { get; }
}
