using Opus.Engine.Rhi;

namespace Opus.Engine.FrameGraph;

/// <summary>
/// A single render pass in the frame graph. Two-phase shape:
/// <list type="number">
/// <item><description><see cref="Setup"/> — declare resource reads / writes through the builder. Pure;
///     called once per recompile, must be deterministic.</description></item>
/// <item><description><see cref="Execute"/> — record GPU commands against the command list. Called once
///     per frame after the graph schedules the pass.</description></item>
/// </list>
///
/// Canonical passes shipped by the engine include <c>DepthPrePass</c>, <c>GBufferPass</c>
/// (in deferred variants — Opus uses Forward+, ADR-0018), <c>ShadowPass</c>,
/// <c>LightCullPass</c>, <c>OpaquePass</c>, <c>TranslucentPass</c>, <c>BloomPass</c>,
/// <c>TonemapPass</c>, <c>UiPass</c>. Game-side passes can implement this directly for
/// custom effects.
/// </summary>
public interface IRenderPass
{
    string Name { get; }

    void Setup(IFrameGraphBuilder builder);

    void Execute(IRenderPassContext context);
}

/// <summary>Per-pass execution context handed to <see cref="IRenderPass.Execute"/>.
/// Carries the resolved RHI handles for the resources the pass declared in <c>Setup</c>
/// plus the command list to record into.</summary>
public interface IRenderPassContext
{
    IRhiCommandList CommandList { get; }

    IRhiTexture ResolveTexture(FrameGraphResource handle);

    IRhiBuffer ResolveBuffer(FrameGraphResource handle);
}
