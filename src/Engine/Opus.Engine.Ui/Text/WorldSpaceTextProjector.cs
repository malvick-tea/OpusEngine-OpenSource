using System;
using System.Numerics;

namespace Opus.Engine.Ui.Text;

/// <summary>
/// Projects a 3D world position into a 2D <see cref="WorldSpaceTextAnchor"/> for placing a
/// world-space label, so the label renders through the same <see cref="IDrawSurface.DrawText"/>
/// glyph path as screen text — no new render pass, shader, or geometry (ADR-0028 one-render-path).
/// Pure and renderer-agnostic: it takes the row-vector view-projection matrix the scene already
/// builds and the surface viewport size, so it is driven without a GPU. Clip space follows the D3D
/// convention (z in <c>[0,1]</c>); a point at or behind the camera, or outside the view volume,
/// projects to <see cref="WorldSpaceTextAnchor.Hidden"/>.
/// </summary>
public static class WorldSpaceTextProjector
{
    // Clip w at or below this puts the point on or behind the camera plane: not projectable
    // (and division would blow up toward infinity), so the label is culled.
    private const float MinimumClipW = 1e-4f;

    /// <summary>Projects <paramref name="worldPosition"/> through <paramref name="viewProjection"/>
    /// (row-vector, <c>view * projection</c>) into surface pixels for an
    /// <paramref name="viewportWidth"/> × <paramref name="viewportHeight"/> surface.</summary>
    public static WorldSpaceTextAnchor Project(
        Vector3 worldPosition, Matrix4x4 viewProjection, int viewportWidth, int viewportHeight)
    {
        if (viewportWidth <= 0 || viewportHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(viewportWidth), "Viewport dimensions must be > 0.");
        }

        var clip = Vector4.Transform(new Vector4(worldPosition, 1f), viewProjection);
        if (clip.W <= MinimumClipW)
        {
            return WorldSpaceTextAnchor.Hidden;
        }

        var inverseW = 1f / clip.W;
        var ndcX = clip.X * inverseW;
        var ndcY = clip.Y * inverseW;
        var ndcZ = clip.Z * inverseW;
        if (ndcX < -1f || ndcX > 1f || ndcY < -1f || ndcY > 1f || ndcZ < 0f || ndcZ > 1f)
        {
            return WorldSpaceTextAnchor.Hidden;
        }

        var screenX = (ndcX + 1f) * 0.5f * viewportWidth;
        var screenY = (1f - ndcY) * 0.5f * viewportHeight;
        return new WorldSpaceTextAnchor(true, screenX, screenY, ndcZ);
    }
}
