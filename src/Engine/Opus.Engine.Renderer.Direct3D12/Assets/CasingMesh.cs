using System;
using System.Numerics;

namespace Opus.Engine.Renderer.Direct3D12.Assets;

/// <summary>Procedural casing geometry: a closed 12-segment cylinder approximating an
/// 88mm gun spent shell case (~8.8cm diameter × ~60cm length). Used by the Garage
/// demo's <c>CasingEjector</c> to render demo-side casing ejecta when a tank fires. Will
/// be replaced by a textured glTF asset once the casing model is authored.</summary>
/// <remarks>
/// Geometry is built around the local Y axis (cylinder axis vertical). Side normals are
/// radial — flat shading reads as a smooth tube. Top + bottom caps use separate ring
/// vertices with up/down normals so the cap shading doesn't smear into the side. Vertex
/// layout matches <see cref="GltfVertexPosNormalUv"/>; winding is CCW around the outward
/// normal of every face so the D3D12 raster default (front-face CW) culls back-faces.
/// </remarks>
public static class CasingMesh
{
    /// <summary>Radial segment count. 12 reads as cylindrical from orbit-camera distances
    /// and keeps the vertex count low (50 verts total, 144 indices).</summary>
    public const int SegmentCount = 12;

    /// <summary>Outer radius — historically accurate 88mm round case
    /// (radius = 0.044m, diameter 0.088m).</summary>
    public const float DefaultRadiusMeters = 0.044f;

    /// <summary>Half-length along Y. Total length = 2 × this = 0.6m, in line with the
    /// shoulder-to-base length of the gun case.</summary>
    public const float DefaultHalfLengthMeters = 0.3f;

    public static GltfVertexPosNormalUv[] BuildVertices(
        float radius = DefaultRadiusMeters,
        float halfLength = DefaultHalfLengthMeters)
    {
        var verts = new GltfVertexPosNormalUv[VertexCount];
        var writer = 0;

        for (var i = 0; i < SegmentCount; i++)
        {
            var (cos, sin) = SegmentDirection(i);
            var x = radius * cos;
            var z = radius * sin;
            var radialNormal = new Vector3(cos, 0f, sin);
            var u = i / (float)SegmentCount;

            verts[writer++] = SideVertex(x, halfLength, z, radialNormal, u, v: 0f);
            verts[writer++] = SideVertex(x, -halfLength, z, radialNormal, u, v: 1f);
        }

        for (var i = 0; i < SegmentCount; i++)
        {
            var (cos, sin) = SegmentDirection(i);
            var x = radius * cos;
            var z = radius * sin;
            verts[writer++] = CapVertex(x, halfLength, z, Vector3.UnitY);
            verts[writer++] = CapVertex(x, -halfLength, z, -Vector3.UnitY);
        }

        verts[writer++] = CapVertex(0f, halfLength, 0f, Vector3.UnitY);
        verts[writer] = CapVertex(0f, -halfLength, 0f, -Vector3.UnitY);
        return verts;
    }

    public static uint[] BuildIndices()
    {
        var indices = new uint[IndexCount];
        var writer = 0;
        writer = EmitSideIndices(indices, writer);
        writer = EmitTopCapIndices(indices, writer);
        EmitBottomCapIndices(indices, writer);
        return indices;
    }

    /// <summary>Total vertex count = side ring (12 × 2 top+bot) + cap rings (12 × 2) +
    /// 2 cap centres = 50.</summary>
    public const int VertexCount = SegmentCount * 4 + 2;

    /// <summary>Total index count = 6 per side segment + 3 per top cap segment + 3 per
    /// bottom cap segment = 144.</summary>
    public const int IndexCount = SegmentCount * 12;

    private const int SideVertexStride = 2;
    private const int CapVertexStride = 2;
    private const int CapRingsBase = SegmentCount * SideVertexStride;
    private const int CapCentresBase = CapRingsBase + SegmentCount * CapVertexStride;
    private const int TopCentreIndex = CapCentresBase;
    private const int BottomCentreIndex = CapCentresBase + 1;

    private static (float Cos, float Sin) SegmentDirection(int segment)
    {
        var angle = MathF.Tau * segment / SegmentCount;
        return (MathF.Cos(angle), MathF.Sin(angle));
    }

    private static GltfVertexPosNormalUv SideVertex(float x, float y, float z, Vector3 normal, float u, float v) =>
        new() { Position = new Vector3(x, y, z), Normal = normal, Uv = new Vector2(u, v) };

    private static GltfVertexPosNormalUv CapVertex(float x, float y, float z, Vector3 normal) =>
        new() { Position = new Vector3(x, y, z), Normal = normal, Uv = Vector2.Zero };

    private static int EmitSideIndices(uint[] indices, int writer)
    {
        for (var i = 0; i < SegmentCount; i++)
        {
            var thisTop = (uint)(i * SideVertexStride);
            var thisBot = thisTop + 1;
            var nextTop = (uint)(((i + 1) % SegmentCount) * SideVertexStride);
            var nextBot = nextTop + 1;

            indices[writer++] = thisTop;
            indices[writer++] = thisBot;
            indices[writer++] = nextBot;

            indices[writer++] = thisTop;
            indices[writer++] = nextBot;
            indices[writer++] = nextTop;
        }

        return writer;
    }

    private static int EmitTopCapIndices(uint[] indices, int writer)
    {
        for (var i = 0; i < SegmentCount; i++)
        {
            var thisVert = (uint)(CapRingsBase + i * CapVertexStride);
            var nextVert = (uint)(CapRingsBase + ((i + 1) % SegmentCount) * CapVertexStride);
            indices[writer++] = (uint)TopCentreIndex;
            indices[writer++] = nextVert;
            indices[writer++] = thisVert;
        }

        return writer;
    }

    private static int EmitBottomCapIndices(uint[] indices, int writer)
    {
        for (var i = 0; i < SegmentCount; i++)
        {
            var thisVert = (uint)(CapRingsBase + i * CapVertexStride + 1);
            var nextVert = (uint)(CapRingsBase + ((i + 1) % SegmentCount) * CapVertexStride + 1);
            indices[writer++] = (uint)BottomCentreIndex;
            indices[writer++] = thisVert;
            indices[writer++] = nextVert;
        }

        return writer;
    }
}
