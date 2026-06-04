using System;
using System.Numerics;

namespace Opus.Foundation.Geometry;

/// <summary>
/// Axis-aligned bounding box. <see cref="Min"/> ≤ <see cref="Max"/> on each axis.
/// Tagged "empty" via <see cref="IsEmpty"/> when no points have been added — useful for
/// progressive Union builds.
/// </summary>
public readonly record struct Aabb(Vector3 Min, Vector3 Max)
{
    public Vector3 Centre => (Min + Max) * 0.5f;

    public Vector3 Extents => (Max - Min) * 0.5f;

    public bool IsEmpty => Min.X > Max.X || Min.Y > Max.Y || Min.Z > Max.Z;

    /// <summary>Empty AABB sentinel — Min is +infinity, Max is -infinity, so any Union
    /// produces a sensible result.</summary>
    public static Aabb Empty => new(new Vector3(float.PositiveInfinity), new Vector3(float.NegativeInfinity));

    /// <summary>Computes the tight AABB of a set of points. Returns <see cref="Empty"/>
    /// for an empty input.</summary>
    public static Aabb FromPoints(ReadOnlySpan<Vector3> points)
    {
        if (points.Length == 0)
        {
            return Empty;
        }

        var min = points[0];
        var max = points[0];
        for (var i = 1; i < points.Length; i++)
        {
            min = Vector3.Min(min, points[i]);
            max = Vector3.Max(max, points[i]);
        }

        return new Aabb(min, max);
    }

    /// <summary>Union of two AABBs (smallest box covering both).</summary>
    public Aabb Union(Aabb other) => new(Vector3.Min(Min, other.Min), Vector3.Max(Max, other.Max));

    /// <summary>Transforms an AABB by a row-vector matrix, returning the AABB of the
    /// transformed 8 corners. Slightly looser than re-fitting the actual transformed
    /// geometry but cheap and conservative.</summary>
    public Aabb Transform(Matrix4x4 m)
    {
        Span<Vector3> corners = stackalloc Vector3[8]
        {
            new(Min.X, Min.Y, Min.Z),
            new(Max.X, Min.Y, Min.Z),
            new(Min.X, Max.Y, Min.Z),
            new(Max.X, Max.Y, Min.Z),
            new(Min.X, Min.Y, Max.Z),
            new(Max.X, Min.Y, Max.Z),
            new(Min.X, Max.Y, Max.Z),
            new(Max.X, Max.Y, Max.Z),
        };

        var newMin = new Vector3(float.PositiveInfinity);
        var newMax = new Vector3(float.NegativeInfinity);
        for (var i = 0; i < 8; i++)
        {
            var p = Vector3.Transform(corners[i], m);
            newMin = Vector3.Min(newMin, p);
            newMax = Vector3.Max(newMax, p);
        }

        return new Aabb(newMin, newMax);
    }
}
