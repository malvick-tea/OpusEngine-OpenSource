using System;
using System.Collections.Generic;
using System.Numerics;
using Opus.Editor.Core;
using Opus.Foundation.Geometry;

namespace Opus.Editor.Ui;

/// <summary>
/// Builds the true wireframe shape of a built-in primitive node (<see cref="ScenePrimitive"/>) for the
/// editor viewport: the unit shape's line set transformed by the node's world matrix, so rotation and
/// non-uniform scale read correctly — unlike the axis-aligned bounds box drawn for model nodes. Also owns
/// each primitive's local bounds, which the pick list shares so what the viewport draws is exactly what a
/// click hits. Pure — the lines draw through the existing UI line batch (one render path, ADR-0028).
/// </summary>
public static class PrimitiveWire
{
    /// <summary>Segments per full circle in the curved shapes — enough to read as round at editor zoom
    /// levels without flooding the line batch.</summary>
    public const int CircleSegments = 24;

    /// <summary>Half the pick-box thickness given to the flat plane primitive, so a click can still hit
    /// it edge-on despite the shape having no volume.</summary>
    public const float PlanePickHalfThickness = 0.05f;

    private const float HalfExtent = 0.5f;

    /// <summary>The primitive's local-space bounds (before the node transform), matching the wire shape's
    /// extents. The plane gets a thin slab rather than a zero-height box so ray picking stays robust.</summary>
    public static Aabb LocalBounds(ScenePrimitiveKind kind)
    {
        var extent = kind switch
        {
            ScenePrimitiveKind.Plane => new Vector3(HalfExtent, PlanePickHalfThickness, HalfExtent),
            _ => new Vector3(HalfExtent),
        };
        return new Aabb(-extent, extent);
    }

    /// <summary>Appends the shape's world-space lines for a node carrying <paramref name="kind"/>,
    /// transformed by <paramref name="world"/>, tagged with <paramref name="role"/>.</summary>
    public static void AppendDrawLines(
        ICollection<ViewportLine> sink, ScenePrimitiveKind kind, in Matrix4x4 world, ViewportLineRole role)
    {
        ArgumentNullException.ThrowIfNull(sink);
        switch (kind)
        {
            case ScenePrimitiveKind.Sphere:
                AppendSphere(sink, world, role);
                break;
            case ScenePrimitiveKind.Cylinder:
                AppendCylinder(sink, world, role);
                break;
            case ScenePrimitiveKind.Plane:
                AppendPlane(sink, world, role);
                break;
            case ScenePrimitiveKind.Cone:
                AppendCone(sink, world, role);
                break;
            default:
                AppendCube(sink, world, role);
                break;
        }
    }

    private static void AppendCube(ICollection<ViewportLine> sink, in Matrix4x4 world, ViewportLineRole role)
    {
        Span<Vector3> corner = stackalloc Vector3[8];
        for (int i = 0; i < 8; i++)
        {
            corner[i] = Vector3.Transform(
                new Vector3(
                    (i & 1) == 0 ? -HalfExtent : HalfExtent,
                    (i & 2) == 0 ? -HalfExtent : HalfExtent,
                    (i & 4) == 0 ? -HalfExtent : HalfExtent),
                world);
        }

        // Four edges along each local axis: X pairs differ in bit 0, Y in bit 1, Z in bit 2.
        for (int i = 0; i < 8; i++)
        {
            if ((i & 1) == 0)
            {
                sink.Add(new ViewportLine(corner[i], corner[i | 1], role));
            }

            if ((i & 2) == 0)
            {
                sink.Add(new ViewportLine(corner[i], corner[i | 2], role));
            }

            if ((i & 4) == 0)
            {
                sink.Add(new ViewportLine(corner[i], corner[i | 4], role));
            }
        }
    }

    private static void AppendSphere(ICollection<ViewportLine> sink, in Matrix4x4 world, ViewportLineRole role)
    {
        AppendCircle(sink, world, role, Vector3.UnitX, Vector3.UnitY, Vector3.Zero);
        AppendCircle(sink, world, role, Vector3.UnitY, Vector3.UnitZ, Vector3.Zero);
        AppendCircle(sink, world, role, Vector3.UnitZ, Vector3.UnitX, Vector3.Zero);
    }

    private static void AppendCylinder(ICollection<ViewportLine> sink, in Matrix4x4 world, ViewportLineRole role)
    {
        AppendCircle(sink, world, role, Vector3.UnitX, Vector3.UnitZ, new Vector3(0f, HalfExtent, 0f));
        AppendCircle(sink, world, role, Vector3.UnitX, Vector3.UnitZ, new Vector3(0f, -HalfExtent, 0f));
        foreach (var rim in RimPoints())
        {
            sink.Add(new ViewportLine(
                Vector3.Transform(rim with { Y = -HalfExtent }, world),
                Vector3.Transform(rim with { Y = HalfExtent }, world),
                role));
        }
    }

    private static void AppendPlane(ICollection<ViewportLine> sink, in Matrix4x4 world, ViewportLineRole role)
    {
        Span<Vector3> corner = stackalloc Vector3[4]
        {
            Vector3.Transform(new Vector3(-HalfExtent, 0f, -HalfExtent), world),
            Vector3.Transform(new Vector3(HalfExtent, 0f, -HalfExtent), world),
            Vector3.Transform(new Vector3(HalfExtent, 0f, HalfExtent), world),
            Vector3.Transform(new Vector3(-HalfExtent, 0f, HalfExtent), world),
        };
        for (int i = 0; i < 4; i++)
        {
            sink.Add(new ViewportLine(corner[i], corner[(i + 1) % 4], role));
        }

        // The diagonals make the quad read as a surface rather than an empty frame.
        sink.Add(new ViewportLine(corner[0], corner[2], role));
        sink.Add(new ViewportLine(corner[1], corner[3], role));
    }

    private static void AppendCone(ICollection<ViewportLine> sink, in Matrix4x4 world, ViewportLineRole role)
    {
        AppendCircle(sink, world, role, Vector3.UnitX, Vector3.UnitZ, new Vector3(0f, -HalfExtent, 0f));
        var apex = Vector3.Transform(new Vector3(0f, HalfExtent, 0f), world);
        foreach (var rim in RimPoints())
        {
            sink.Add(new ViewportLine(Vector3.Transform(rim with { Y = -HalfExtent }, world), apex, role));
        }
    }

    /// <summary>Appends one full circle of radius 0.5 in the plane spanned by two local axes, centred on
    /// <paramref name="center"/> in local space.</summary>
    private static void AppendCircle(
        ICollection<ViewportLine> sink,
        in Matrix4x4 world,
        ViewportLineRole role,
        Vector3 axisA,
        Vector3 axisB,
        Vector3 center)
    {
        var previous = Vector3.Transform(center + (axisA * HalfExtent), world);
        for (int i = 1; i <= CircleSegments; i++)
        {
            float angle = i * (MathF.Tau / CircleSegments);
            var local = center + (((axisA * MathF.Cos(angle)) + (axisB * MathF.Sin(angle))) * HalfExtent);
            var current = Vector3.Transform(local, world);
            sink.Add(new ViewportLine(previous, current, role));
            previous = current;
        }
    }

    /// <summary>The four rim anchor points (local +X, −X, +Z, −Z at radius 0.5, Y = 0) the cylinder struts
    /// and cone slant lines attach to.</summary>
    private static Vector3[] RimPoints() => new[]
    {
        new Vector3(HalfExtent, 0f, 0f),
        new Vector3(-HalfExtent, 0f, 0f),
        new Vector3(0f, 0f, HalfExtent),
        new Vector3(0f, 0f, -HalfExtent),
    };
}
