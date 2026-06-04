using System.Numerics;

namespace Opus.Engine.Renderer.Direct3D12.Assets;

/// <summary>Procedural projectile geometry: a small axis-aligned cube placeholder visible
/// from orbit-camera distances (≤ ~30 m). Used by the M4.h Garage demo and by future
/// match-screen renderers until per-round visuals (tracer streaks, HE sparks) ship.</summary>
/// <remarks>
/// Vertex format matches <see cref="GltfVertexPosNormalUv"/>, winding CCW (matching the
/// glTF default + D3D12 raster default <c>FrontCounterClockwise = false</c> → CW front so
/// every face's outward normal points away from the cube centre and front-faces the
/// viewer for the visible direction).
/// </remarks>
public static class ProjectileMesh
{
    /// <summary>Half-edge length of the placeholder cube. ~0.2 m gives a 40 cm box —
    /// small enough to read as "a round" but big enough to spot from 15 m away.</summary>
    public const float DefaultHalfExtentMeters = 0.2f;

    public static GltfVertexPosNormalUv[] BuildVertices(float halfExtent = DefaultHalfExtentMeters)
    {
        var h = halfExtent;
        var verts = new GltfVertexPosNormalUv[24];
        // Six faces, 4 vertices each. Per-face normal so flat shading reads as a cube.
        var px = new Vector3(1, 0, 0);
        var nx = new Vector3(-1, 0, 0);
        var py = new Vector3(0, 1, 0);
        var ny = new Vector3(0, -1, 0);
        var pz = new Vector3(0, 0, 1);
        var nz = new Vector3(0, 0, -1);

        var uv00 = new Vector2(0, 0);
        var uv10 = new Vector2(1, 0);
        var uv11 = new Vector2(1, 1);
        var uv01 = new Vector2(0, 1);

        // +X face
        verts[0] = new GltfVertexPosNormalUv { Position = new Vector3(h, -h, -h), Normal = px, Uv = uv00 };
        verts[1] = new GltfVertexPosNormalUv { Position = new Vector3(h, h, -h), Normal = px, Uv = uv01 };
        verts[2] = new GltfVertexPosNormalUv { Position = new Vector3(h, h, h), Normal = px, Uv = uv11 };
        verts[3] = new GltfVertexPosNormalUv { Position = new Vector3(h, -h, h), Normal = px, Uv = uv10 };

        // -X face
        verts[4] = new GltfVertexPosNormalUv { Position = new Vector3(-h, -h, h), Normal = nx, Uv = uv00 };
        verts[5] = new GltfVertexPosNormalUv { Position = new Vector3(-h, h, h), Normal = nx, Uv = uv01 };
        verts[6] = new GltfVertexPosNormalUv { Position = new Vector3(-h, h, -h), Normal = nx, Uv = uv11 };
        verts[7] = new GltfVertexPosNormalUv { Position = new Vector3(-h, -h, -h), Normal = nx, Uv = uv10 };

        // +Y face
        verts[8] = new GltfVertexPosNormalUv { Position = new Vector3(-h, h, -h), Normal = py, Uv = uv00 };
        verts[9] = new GltfVertexPosNormalUv { Position = new Vector3(-h, h, h), Normal = py, Uv = uv01 };
        verts[10] = new GltfVertexPosNormalUv { Position = new Vector3(h, h, h), Normal = py, Uv = uv11 };
        verts[11] = new GltfVertexPosNormalUv { Position = new Vector3(h, h, -h), Normal = py, Uv = uv10 };

        // -Y face
        verts[12] = new GltfVertexPosNormalUv { Position = new Vector3(-h, -h, h), Normal = ny, Uv = uv00 };
        verts[13] = new GltfVertexPosNormalUv { Position = new Vector3(-h, -h, -h), Normal = ny, Uv = uv01 };
        verts[14] = new GltfVertexPosNormalUv { Position = new Vector3(h, -h, -h), Normal = ny, Uv = uv11 };
        verts[15] = new GltfVertexPosNormalUv { Position = new Vector3(h, -h, h), Normal = ny, Uv = uv10 };

        // +Z face
        verts[16] = new GltfVertexPosNormalUv { Position = new Vector3(h, -h, h), Normal = pz, Uv = uv00 };
        verts[17] = new GltfVertexPosNormalUv { Position = new Vector3(h, h, h), Normal = pz, Uv = uv01 };
        verts[18] = new GltfVertexPosNormalUv { Position = new Vector3(-h, h, h), Normal = pz, Uv = uv11 };
        verts[19] = new GltfVertexPosNormalUv { Position = new Vector3(-h, -h, h), Normal = pz, Uv = uv10 };

        // -Z face
        verts[20] = new GltfVertexPosNormalUv { Position = new Vector3(-h, -h, -h), Normal = nz, Uv = uv00 };
        verts[21] = new GltfVertexPosNormalUv { Position = new Vector3(-h, h, -h), Normal = nz, Uv = uv01 };
        verts[22] = new GltfVertexPosNormalUv { Position = new Vector3(h, h, -h), Normal = nz, Uv = uv11 };
        verts[23] = new GltfVertexPosNormalUv { Position = new Vector3(h, -h, -h), Normal = nz, Uv = uv10 };

        return verts;
    }

    public static uint[] BuildIndices()
    {
        var indices = new uint[36];
        for (var face = 0; face < 6; face++)
        {
            var baseIdx = face * 4;
            var i = face * 6;
            indices[i + 0] = (uint)(baseIdx + 0);
            indices[i + 1] = (uint)(baseIdx + 1);
            indices[i + 2] = (uint)(baseIdx + 2);
            indices[i + 3] = (uint)(baseIdx + 0);
            indices[i + 4] = (uint)(baseIdx + 2);
            indices[i + 5] = (uint)(baseIdx + 3);
        }

        return indices;
    }
}
