using System.Numerics;

namespace Opus.Engine.Renderer.Direct3D12.Assets;

/// <summary>Procedural ground-plane geometry: one large XZ quad centred on the origin,
/// normal facing +Y. Lives entirely on the CPU side — the caller uploads it to D3D12 via
/// the standard buffer-upload path. Vertex format matches <see cref="GltfVertexPosNormalUv"/>
/// so the floor renders through the same forward PSO the loaded tank meshes use.</summary>
/// <remarks>
/// Winding is CCW when viewed from -Y so the front face faces +Y (the camera in a top-down
/// orbit). Matches the default D3D12 raster state (<c>FrontCounterClockwise = false</c>,
/// CW front) — the same convention every glTF mesh in the project relies on.
/// </remarks>
public static class FloorMesh
{
    /// <summary>Default extent (metres from the origin to each edge) for the procedural
    /// floor. Picks 100 m so the camera framing radius for a tank (≤ 10 m) never reaches
    /// the floor edge.</summary>
    public const float DefaultHalfExtentMeters = 100f;

    public static GltfVertexPosNormalUv[] BuildVertices(float halfExtent = DefaultHalfExtentMeters)
    {
        var normal = Vector3.UnitY;
        return new[]
        {
            new GltfVertexPosNormalUv { Position = new Vector3(-halfExtent, 0f, -halfExtent), Normal = normal, Uv = new Vector2(0f, 0f) },
            new GltfVertexPosNormalUv { Position = new Vector3(+halfExtent, 0f, -halfExtent), Normal = normal, Uv = new Vector2(1f, 0f) },
            new GltfVertexPosNormalUv { Position = new Vector3(+halfExtent, 0f, +halfExtent), Normal = normal, Uv = new Vector2(1f, 1f) },
            new GltfVertexPosNormalUv { Position = new Vector3(-halfExtent, 0f, +halfExtent), Normal = normal, Uv = new Vector2(0f, 1f) },
        };
    }

    public static uint[] BuildIndices() => new uint[] { 0u, 1u, 2u, 0u, 2u, 3u };
}
