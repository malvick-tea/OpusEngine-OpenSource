namespace Opus.Engine.Renderer;

/// <summary>
/// The canonical scene renderer — given a frame context + renderable source, adds the
/// Forward+ pass set (per ADR-0018) to the frame graph and draws.
///
/// Pluggable: alternative renderers can satisfy the same interface for special
/// purposes — minimap top-down view, screenshot pipeline, asset-preview tool. The
/// canonical implementation is <c>ForwardPlusSceneRenderer</c> (lands in R-3).
/// </summary>
public interface ISceneRenderer
{
    /// <summary>Display name for diagnostics ("Forward+", "Minimap", "Screenshot").</summary>
    string Name { get; }

    /// <summary>Adds the renderer's passes to <paramref name="context"/>'s frame graph,
    /// drawing the supplied <paramref name="renderables"/>. Multi-view rendering: called
    /// once per camera in <see cref="FrameCameraSet"/>.</summary>
    void Render(IFrameContext context, IRenderableSource renderables);
}
