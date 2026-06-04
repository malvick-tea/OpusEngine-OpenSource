using System;
using Opus.Engine.FrameGraph;
using Opus.Engine.Rhi;

namespace Opus.Engine.Renderer;

/// <summary>
/// The top-level engine entry for rendering one frame. Owns the device, the frame graph,
/// and the canonical pass set. Game code typically goes through
/// <see cref="ISceneRenderer"/> rather than driving <see cref="IRenderer"/> directly —
/// IRenderer is the substrate.
///
/// Lifecycle:
/// <list type="number">
/// <item><description>One <see cref="IRenderer"/> per process at runtime (no multi-window today; the
///     architecture allows it via multi-viewport in a single renderer).</description></item>
/// <item><description><see cref="BeginFrame"/> captures cameras + lighting + post-FX state, opens a
///     fresh frame graph build, returns a <see cref="IFrameContext"/>.</description></item>
/// <item><description>Scene rendererовать(s) add their passes via the context.</description></item>
/// <item><description><see cref="EndFrame"/> compiles + executes + presents.</description></item>
/// </list>
///
/// Disposal flushes outstanding GPU work, releases device + frame graph + resources.
/// </summary>
public interface IRenderer : IDisposable
{
    IRhiDevice Device { get; }

    IFrameGraph FrameGraph { get; }

    IFrameContext BeginFrame(FrameCameraSet cameras, LightingSetup lighting, PostFxSetup postFx);

    void EndFrame(IFrameContext context);
}

/// <summary>Per-frame state handed back from <see cref="IRenderer.BeginFrame"/>.
/// Carries the captured camera / lighting / post-fx state so passes can read them
/// without going through the renderer again.</summary>
public interface IFrameContext
{
    FrameCameraSet Cameras { get; }

    LightingSetup Lighting { get; }

    PostFxSetup PostFx { get; }

    /// <summary>The current frame index since renderer creation. Useful for temporal
    /// effects (TAA jitter sequence, halton sampling) and per-frame Tracy markers.</summary>
    ulong FrameIndex { get; }
}
