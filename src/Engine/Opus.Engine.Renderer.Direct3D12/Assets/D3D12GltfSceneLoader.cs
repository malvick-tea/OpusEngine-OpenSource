using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Runtime.InteropServices;
using Opus.Content.Meshes;
using Opus.Engine.Rhi;
using Opus.Engine.Rhi.Direct3D12;
using Opus.Foundation.Geometry;

namespace Opus.Engine.Renderer.Direct3D12.Assets;

/// <summary>
/// Loads a glTF/GLB asset end-to-end onto a <see cref="D3D12RhiDevice"/>:
/// <list type="number">
/// <item><description>Parse the GLB into a <see cref="GltfScene"/> via
///     <see cref="GltfBinaryReader.ReadScene"/>.</description></item>
/// <item><description>Flatten the node graph into a per-node world-transform list and
///     filter to nodes that reference a mesh (<see cref="SceneNodeDraw"/>).</description></item>
/// <item><description>Upload every primitive of every mesh as its own VB + IB pair into a
///     flat <see cref="GpuScene"/>; mesh → slice map preserves the lookup for render-time
///     draw calls.</description></item>
/// <item><description>Compute a scene-wide AABB from the union of transformed primitive
///     bounding boxes — used by orbit-camera framing.</description></item>
/// </list>
/// Centralised so every Forward+/forward path producing tank-style renders uses the same
/// upload + slicing implementation. Changing vertex layout or indexing strategy is one
/// edit here; every consumer picks it up automatically.
/// </summary>
public static class D3D12GltfSceneLoader
{
    /// <summary>Loads a glTF asset from disk. Routes to the binary or split-file path
    /// based on the file extension: <c>.glb</c> → direct read; <c>.gltf</c> → pack with
    /// the sidecar <c>.bin</c> via <see cref="GltfFilePacker"/> first.</summary>
    public static GltfSceneGpuAssets Load(D3D12RhiDevice device, string assetPath, string gpuNamePrefix)
    {
        if (device is null)
        {
            throw new ArgumentNullException(nameof(device));
        }

        if (string.IsNullOrWhiteSpace(assetPath))
        {
            throw new ArgumentException("Asset path is empty.", nameof(assetPath));
        }

        var glbBytes = ReadAssetBytes(assetPath);
        var scene = GltfBinaryReader.ReadScene(glbBytes);
        var worldTransforms = SceneTreeMath.ComputeWorldTransforms(scene);
        var nodeDraws = BuildNodeDraws(scene, worldTransforms);
        var gpuScene = UploadAllPrimitives(device, scene.Meshes, gpuNamePrefix);
        var bounds = SceneBounds(scene, gpuScene.SlicesByMesh, nodeDraws);
        return new GltfSceneGpuAssets(glbBytes, scene, nodeDraws, gpuScene, bounds)
        {
            MeshLocalBounds = ComputeMeshLocalBounds(scene.Meshes),
        };
    }

    private static byte[] ReadAssetBytes(string assetPath)
    {
        if (!File.Exists(assetPath))
        {
            throw new FileNotFoundException($"glTF asset not found: {assetPath}", assetPath);
        }

        var ext = Path.GetExtension(assetPath);
        if (string.Equals(ext, ".gltf", StringComparison.OrdinalIgnoreCase))
        {
            return GltfFilePacker.PackToGlb(assetPath);
        }

        return File.ReadAllBytes(assetPath);
    }

    /// <summary>Uploads every primitive of every mesh as its own VB + IB pair. Public so
    /// callers building a scene from non-glTF sources (procedural geometry, native binary
    /// dumps) can reuse the same flat-primitives + slice-table representation without
    /// going through the GLB reader.</summary>
    public static GpuScene UploadAllPrimitives(D3D12RhiDevice device, GltfMesh[] meshes, string namePrefix)
    {
        if (device is null)
        {
            throw new ArgumentNullException(nameof(device));
        }

        if (meshes is null)
        {
            throw new ArgumentNullException(nameof(meshes));
        }

        var primitives = new List<GpuPrimitive>(meshes.Length);
        var slices = new GpuMeshSlice[meshes.Length];
        for (var m = 0; m < meshes.Length; m++)
        {
            var start = primitives.Count;
            var mesh = meshes[m];
            for (var p = 0; p < mesh.Primitives.Length; p++)
            {
                primitives.Add(UploadPrimitive(device, mesh.Primitives[p], $"{namePrefix}.mesh{m}.prim{p}"));
            }

            slices[m] = new GpuMeshSlice(start, primitives.Count - start);
        }

        return new GpuScene(primitives.ToArray(), slices);
    }

    /// <summary>Conservative scene-wide AABB: union of every primitive's
    /// vertex-position AABB after applying the owning node's world transform. Cheap
    /// (8-corner refit per primitive); accurate enough for orbit-camera framing.</summary>
    public static Aabb SceneBounds(GltfScene scene, GpuMeshSlice[] slicesByMesh, IReadOnlyList<SceneNodeDraw> draws)
    {
        var box = default(Aabb);
        var hasBox = false;
        foreach (var draw in draws)
        {
            var slice = slicesByMesh[draw.MeshIndex];
            for (var p = 0; p < slice.Count; p++)
            {
                var prim = scene.Meshes[draw.MeshIndex].Primitives[p];
                var primBox = Aabb.FromPoints(prim.Geometry.Positions).Transform(draw.World);
                box = hasBox ? box.Union(primBox) : primBox;
                hasBox = true;
            }
        }

        return hasBox ? box : new Aabb(new Vector3(-1f), new Vector3(1f));
    }

    /// <summary>Per-mesh local-space AABB: the union of every primitive's vertex-position
    /// AABB in mesh-local space (no node transform). Indexed by glTF mesh index to match
    /// <see cref="GpuScene.SlicesByMesh"/>. Frustum culling transforms one of these by a
    /// node's world matrix to bound the node cheaply.</summary>
    public static Aabb[] ComputeMeshLocalBounds(GltfMesh[] meshes)
    {
        ArgumentNullException.ThrowIfNull(meshes);
        var bounds = new Aabb[meshes.Length];
        for (var m = 0; m < meshes.Length; m++)
        {
            var box = Aabb.Empty;
            var primitives = meshes[m].Primitives;
            for (var p = 0; p < primitives.Length; p++)
            {
                box = box.Union(Aabb.FromPoints(primitives[p].Geometry.Positions));
            }

            bounds[m] = box;
        }

        return bounds;
    }

    private static IReadOnlyList<SceneNodeDraw> BuildNodeDraws(GltfScene scene, Matrix4x4[] worldTransforms)
    {
        var draws = new List<SceneNodeDraw>(scene.Nodes.Length);
        for (var i = 0; i < scene.Nodes.Length; i++)
        {
            var node = scene.Nodes[i];
            if (node.MeshIndex is not int meshIdx)
            {
                continue;
            }

            draws.Add(new SceneNodeDraw(meshIdx, worldTransforms[i], Vector4.One, Vector2.Zero, i));
        }

        return draws;
    }

    private static GpuPrimitive UploadPrimitive(D3D12RhiDevice device, GltfMeshPrimitive primitive, string debugName)
    {
        var mesh = primitive.Geometry;
        var verts = PackVertices(mesh);
        var vb = device.CreateGraphicsBuffer(new RhiBufferDescription(
            $"{debugName}.verts",
            verts.Length * Marshal.SizeOf<GltfVertexPosNormalUv>(),
            RhiBufferUsage.Vertex));
        BufferUploadHelper.WriteStructs(vb, verts);

        var ib = device.CreateGraphicsBuffer(new RhiBufferDescription(
            $"{debugName}.indices",
            mesh.Indices.Length * sizeof(uint),
            RhiBufferUsage.Index));
        BufferUploadHelper.WriteStructs(ib, mesh.Indices);

        return new GpuPrimitive(vb, ib, (uint)mesh.Indices.Length, primitive.MaterialIndex);
    }

    private static GltfVertexPosNormalUv[] PackVertices(MeshData mesh)
    {
        var verts = new GltfVertexPosNormalUv[mesh.VertexCount];
        var uvs = mesh.Uvs;
        for (var i = 0; i < verts.Length; i++)
        {
            verts[i] = new GltfVertexPosNormalUv
            {
                Position = mesh.Positions[i],
                Normal = mesh.Normals[i],
                Uv = uvs is null ? Vector2.Zero : uvs[i],
            };
        }

        return verts;
    }
}
