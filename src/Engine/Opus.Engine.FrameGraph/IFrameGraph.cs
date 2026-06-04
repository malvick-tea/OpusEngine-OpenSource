using System;
using System.Collections.Generic;

namespace Opus.Engine.FrameGraph;

/// <summary>
/// Builds a graph of <see cref="IRenderPass"/>es with declared resource dependencies,
/// compiles it to a barrier-correct schedule, executes it.
///
/// Typical frame:
/// <code>
/// graph.BeginFrame();
/// graph.AddPass(new ShadowPass(...));
/// graph.AddPass(new LightCullPass(...));
/// graph.AddPass(new OpaquePass(...));
/// graph.AddPass(new TonemapPass(...));
/// graph.Compile();   // resolves dependencies + barriers + transient aliasing
/// graph.Execute();   // runs each pass against the device
/// graph.EndFrame();
/// </code>
///
/// Recompile cost: only when graph topology changes (pass list reorders, new pass
/// added). Per-frame parameter tweaks (resolution change, light count) don't trigger
/// recompile.
/// </summary>
public interface IFrameGraph : IDisposable
{
    void BeginFrame();

    void AddPass(IRenderPass pass);

    /// <summary>Resolves dependencies, inserts barriers, plans transient aliasing.
    /// Idempotent — calling twice on the same pass set does no extra work.</summary>
    void Compile();

    /// <summary>Executes the compiled schedule. Requires a prior <see cref="Compile"/>
    /// in the same frame; throws otherwise.</summary>
    void Execute();

    void EndFrame();

    /// <summary>Recorded passes in declaration order (post-Compile may reorder for
    /// execution — declaration order is for diagnostics).</summary>
    IReadOnlyList<IRenderPass> Passes { get; }
}

/// <summary>Builder handed to <see cref="IRenderPass.Setup"/> for declaring resource
/// reads / writes. The graph uses these declarations to schedule + insert barriers.</summary>
public interface IFrameGraphBuilder
{
    /// <summary>Allocate a transient texture for this frame. The graph may alias it
    /// onto another transient texture whose lifetime doesn't overlap.</summary>
    FrameGraphResource CreateTransientTexture(FrameGraphTextureDescription description);

    /// <summary>Allocate a transient buffer for this frame.</summary>
    FrameGraphResource CreateTransientBuffer(FrameGraphBufferDescription description);

    /// <summary>Declare that the pass reads from <paramref name="handle"/>.</summary>
    void Read(FrameGraphResource handle);

    /// <summary>Declare that the pass writes to <paramref name="handle"/>.</summary>
    void Write(FrameGraphResource handle);

    /// <summary>Declare that the pass uses <paramref name="handle"/> as a colour
    /// render target.</summary>
    void ColorTarget(FrameGraphResource handle);

    /// <summary>Declare that the pass uses <paramref name="handle"/> as a depth /
    /// stencil render target.</summary>
    void DepthTarget(FrameGraphResource handle);
}
