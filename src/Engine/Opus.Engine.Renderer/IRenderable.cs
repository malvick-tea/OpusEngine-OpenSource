using System.Numerics;

namespace Opus.Engine.Renderer;

/// <summary>
/// One drawable item extracted from the scene per frame. Stable across the frame —
/// the extract pass builds the renderable list, the renderer iterates + draws, the list
/// is discarded at <c>EndFrame</c>. Per-frame allocation pressure is bounded by world
/// entity count.
///
/// Fields are deliberately minimal — the extract step lowers complex ECS-side state
/// (multi-mesh tanks with turret children, character with skeleton) into one record per
/// draw. A tank may produce 3-5 renderables (hull, turret, barrel, hatches); a
/// character produces 1 (skinned mesh) or many (with attached props).
/// </summary>
public readonly record struct Renderable(
    int MeshHandle,
    int MaterialHandle,
    MaterialPipeline Pipeline,
    Matrix4x4 WorldTransform,
    RenderableLayerMask Layers,
    int SkeletonHandle = 0);

/// <summary>Layer flags — filter passes by category. Examples: shadow casters,
/// transparent, debug-only, motion-blurred. Multi-flag for items that participate in
/// several passes.</summary>
[System.Flags]
public enum RenderableLayerMask : ushort
{
    None = 0,
    Opaque = 1 << 0,
    Translucent = 1 << 1,
    ShadowCaster = 1 << 2,
    DepthOnly = 1 << 3,
    Outline = 1 << 4,
    DebugOverlay = 1 << 5,
}

/// <summary>Extract-side source: the renderer asks for the current frame's
/// <see cref="Renderable"/> list, the extract step produces it from ECS state.</summary>
public interface IRenderableSource
{
    System.Collections.Generic.IReadOnlyList<Renderable> ExtractRenderables();
}
