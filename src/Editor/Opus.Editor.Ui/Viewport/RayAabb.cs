using System;
using Opus.Foundation.Geometry;

namespace Opus.Editor.Ui;

/// <summary>
/// Ray versus axis-aligned bounding-box intersection (the slab method), for viewport picking. Pure.
/// </summary>
public static class RayAabb
{
    private const float ParallelEpsilon = 1e-8f;

    /// <summary>Tests <paramref name="ray"/> against <paramref name="box"/>. On a hit,
    /// <paramref name="distance"/> is the nearest non-negative parametric distance (0 when the ray
    /// starts inside the box). Returns false for an empty box or a box entirely behind the origin.</summary>
    public static bool Intersects(Ray ray, Aabb box, out float distance)
    {
        distance = 0f;
        if (box.IsEmpty)
        {
            return false;
        }

        float tMin = float.NegativeInfinity;
        float tMax = float.PositiveInfinity;
        if (!Slab(ray.Origin.X, ray.Direction.X, box.Min.X, box.Max.X, ref tMin, ref tMax) ||
            !Slab(ray.Origin.Y, ray.Direction.Y, box.Min.Y, box.Max.Y, ref tMin, ref tMax) ||
            !Slab(ray.Origin.Z, ray.Direction.Z, box.Min.Z, box.Max.Z, ref tMin, ref tMax))
        {
            return false;
        }

        if (tMax < 0f)
        {
            return false;
        }

        distance = tMin >= 0f ? tMin : 0f;
        return true;
    }

    private static bool Slab(float origin, float direction, float min, float max, ref float tMin, ref float tMax)
    {
        if (MathF.Abs(direction) < ParallelEpsilon)
        {
            return origin >= min && origin <= max;
        }

        float inverse = 1f / direction;
        float t1 = (min - origin) * inverse;
        float t2 = (max - origin) * inverse;
        if (t1 > t2)
        {
            (t1, t2) = (t2, t1);
        }

        if (t1 > tMin)
        {
            tMin = t1;
        }

        if (t2 < tMax)
        {
            tMax = t2;
        }

        return tMin <= tMax;
    }
}
