using System.Numerics;

namespace Opus.Editor.Ui;

/// <summary>
/// Projects world-space points to viewport pixels through a row-vector view-projection matrix (the
/// engine's convention, matching the world-space text projector: y-down screen, perspective divide).
/// Returns false for points on or behind the camera plane. Pure; the D3D12 seam uses it to turn
/// world-space viewport lines into 2D draw calls.
/// </summary>
public static class WorldScreenProjector
{
    private const float MinClipW = 1e-6f;

    public static bool TryProject(
        Vector3 world, Matrix4x4 viewProjection, int width, int height, out Vector2 screen)
    {
        screen = Vector2.Zero;
        var clip = Vector4.Transform(new Vector4(world, 1f), viewProjection);
        if (clip.W <= MinClipW)
        {
            return false;
        }

        float ndcX = clip.X / clip.W;
        float ndcY = clip.Y / clip.W;
        screen = new Vector2(
            ((ndcX * 0.5f) + 0.5f) * width,
            (1f - ((ndcY * 0.5f) + 0.5f)) * height);
        return true;
    }
}
