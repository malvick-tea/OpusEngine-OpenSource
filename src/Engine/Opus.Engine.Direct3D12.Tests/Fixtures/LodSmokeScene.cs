using System;
using System.Collections.Generic;
using System.Numerics;
using Opus.Content.Meshes;
using Opus.Content.Textures;
using Opus.Engine.Renderer.Direct3D12.Assets;
using Opus.Engine.Renderer.Direct3D12.Scene;
using Opus.Engine.Rhi;
using Opus.Engine.Rhi.Direct3D12;
using Opus.Foundation.Geometry;

namespace Opus.Engine.Direct3D12.Tests.Fixtures;

/// <summary>Procedural two-mesh scene for the coarse-LOD smoke: a high-tessellation disc (mesh 0,
/// the fine LOD) and a low-tessellation disc of the same radius (mesh 1, the coarse LOD), spliced
/// into one <see cref="GpuScene"/> exactly as ADR-0028 splices procedural fixtures. Both meshes
/// are a single primitive, so a fine-to-coarse reselection lowers only the submitted index count,
/// not the draw-call or primitive-instance count — the cleanest isolation of the LOD win.
/// <para>
/// Owns the uploaded primitives + a 1×1 white albedo atlas; disposes both. The coarse meshes are
/// generated procedurally, never read from a consumer asset, per the engine-owns-pipeline rule.
/// </para></summary>
internal sealed class LodSmokeScene : IDisposable
{
    /// <summary>Camera distance (world units) past which mesh 0 reselects to the coarse mesh 1.
    /// The smoke places its near instances inside this and its far instances beyond it.</summary>
    public const float LodDistanceThreshold = 60f;

    private const int FineSegmentCount = 96;
    private const int CoarseSegmentCount = 8;
    private const float DiscRadiusMeters = 5f;

    private LodSmokeScene(
        GpuScene gpuScene,
        IMaterialAtlas atlas,
        IReadOnlyList<Aabb> meshLocalBounds,
        IReadOnlyList<SceneMeshLod> meshLods,
        int fineIndexCount,
        int coarseIndexCount)
    {
        GpuScene = gpuScene;
        Atlas = atlas;
        MeshLocalBounds = meshLocalBounds;
        MeshLods = meshLods;
        FineIndexCount = fineIndexCount;
        CoarseIndexCount = coarseIndexCount;
    }

    public GpuScene GpuScene { get; }

    public IMaterialAtlas Atlas { get; }

    /// <summary>Per-mesh local bounds for both discs (indexed by mesh). Feeds frustum culling and,
    /// reused, the LOD camera-distance measurement.</summary>
    public IReadOnlyList<Aabb> MeshLocalBounds { get; }

    /// <summary>LOD table indexed by mesh: mesh 0 reselects to mesh 1 past
    /// <see cref="LodDistanceThreshold"/>; mesh 1 has no chain of its own.</summary>
    public IReadOnlyList<SceneMeshLod> MeshLods { get; }

    /// <summary>Index count of the fine disc (mesh 0) — the per-instance triangle cost a near
    /// instance pays.</summary>
    public int FineIndexCount { get; }

    /// <summary>Index count of the coarse disc (mesh 1) — strictly fewer than
    /// <see cref="FineIndexCount"/>, so a demoted instance submits fewer indices.</summary>
    public int CoarseIndexCount { get; }

    /// <summary>The fine LOD mesh index a node carries before LOD reselection.</summary>
    public int FineMeshIndex => 0;

    public static LodSmokeScene Create(D3D12RhiDevice device, string namePrefix)
    {
        ArgumentNullException.ThrowIfNull(device);

        var fine = BuildDisc("lod.fine", FineSegmentCount, DiscRadiusMeters);
        var coarse = BuildDisc("lod.coarse", CoarseSegmentCount, DiscRadiusMeters);
        var meshes = new[]
        {
            new GltfMesh("lod.fine", new[] { new GltfMeshPrimitive(fine, MaterialIndex: 0) }),
            new GltfMesh("lod.coarse", new[] { new GltfMeshPrimitive(coarse, MaterialIndex: 0) }),
        };

        var gpuScene = D3D12GltfSceneLoader.UploadAllPrimitives(device, meshes, namePrefix);
        var bounds = D3D12GltfSceneLoader.ComputeMeshLocalBounds(meshes);
        var atlas = BuildWhiteAtlas(device, namePrefix);
        var lods = new[]
        {
            SceneMeshLod.Create(
                new SceneMeshLodLevel(MeshIndex: 0, MaxCameraDistance: LodDistanceThreshold),
                new SceneMeshLodLevel(MeshIndex: 1, MaxCameraDistance: float.PositiveInfinity)),
            SceneMeshLod.None,
        };

        return new LodSmokeScene(gpuScene, atlas, bounds, lods, fine.IndexCount, coarse.IndexCount);
    }

    public void Dispose()
    {
        foreach (var prim in GpuScene.Primitives)
        {
            prim.Vb.Dispose();
            prim.Ib.Dispose();
        }

        Atlas.Dispose();
    }

    /// <summary>Builds a flat triangle-fan disc in the XY plane (normal +Z) with
    /// <paramref name="segments"/> rim vertices — index count is <c>segments × 3</c>, so the fine
    /// and coarse variants differ purely in tessellation. Vertex layout matches
    /// <see cref="GltfVertexPosNormalUv"/> the way every procedural fixture mesh does.</summary>
    private static MeshData BuildDisc(string name, int segments, float radius)
    {
        var positions = new Vector3[segments + 1];
        var normals = new Vector3[segments + 1];
        var uvs = new Vector2[segments + 1];

        positions[0] = Vector3.Zero;
        normals[0] = Vector3.UnitZ;
        uvs[0] = new Vector2(0.5f, 0.5f);
        for (var i = 0; i < segments; i++)
        {
            var angle = MathF.Tau * i / segments;
            var cos = MathF.Cos(angle);
            var sin = MathF.Sin(angle);
            positions[i + 1] = new Vector3(radius * cos, radius * sin, 0f);
            normals[i + 1] = Vector3.UnitZ;
            uvs[i + 1] = new Vector2(0.5f + 0.5f * cos, 0.5f + 0.5f * sin);
        }

        var indices = new uint[segments * 3];
        var writer = 0;
        for (var i = 0; i < segments; i++)
        {
            indices[writer++] = 0u;
            indices[writer++] = (uint)(i + 1);
            indices[writer++] = (uint)(((i + 1) % segments) + 1);
        }

        return new MeshData(name, positions, normals, Tangents: null, uvs, indices);
    }

    private static IMaterialAtlas BuildWhiteAtlas(D3D12RhiDevice device, string namePrefix)
    {
        var decoded = new DecodedImage(1, 1, new byte[] { 255, 255, 255, 255 });
        var albedo = device.CreateGraphicsTexture(new RhiTextureDescription(
            $"{namePrefix}.albedo", decoded.Width, decoded.Height, 1,
            RhiTextureFormat.Rgba8Unorm, RhiTextureUsage.Sampled));
        using (var initCmd = device.CreateGraphicsCommandList($"{namePrefix}.albedo.init"))
        {
            initCmd.Begin(0);
            using var staging = device.ScheduleTextureUpload(albedo, decoded.Rgba, initCmd);
            initCmd.End();
            initCmd.ExecuteOn(device);
            device.WaitForIdle();
        }

        return new SingleTextureAtlas(device, albedo, Vector4.One);
    }
}
