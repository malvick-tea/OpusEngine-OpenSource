using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;

namespace Opus.Content.Meshes;

/// <summary>
/// Minimal glTF 2.0 binary (GLB) reader. Decodes the first mesh's first primitive into a
/// <see cref="MeshData"/>. Supports the attribute set we care about for the engine: POSITION,
/// NORMAL, TANGENT, TEXCOORD_0; and 8-/16-/32-bit unsigned indices. Interleaved buffer
/// views are tolerated as long as each attribute lives in its own bufferView range starting
/// at the accessor's combined offset.
/// </summary>
/// <remarks>
/// Accessor-level byte readers (<c>ReadVec2/3/4</c>, <c>ReadIndices</c>, accessor →
/// offset resolution) live in the <c>GltfBinaryReader.Accessors.cs</c> partial alongside
/// the glTF component-type constants. This file owns the public API +
/// scene-tree / mesh / primitive parsing.
/// </remarks>
public static partial class GltfBinaryReader
{
    /// <summary>
    /// Reads a GLB byte stream, returns the first mesh's first primitive as a
    /// <see cref="MeshData"/>. Throws <see cref="InvalidDataException"/> if the header /
    /// chunks are malformed or the requested attributes have unsupported component types.
    /// Use <see cref="ReadScene"/> when you need the full multi-primitive scene tree.
    /// </summary>
    public static MeshData ReadFirstMesh(ReadOnlySpan<byte> glb)
    {
        var (doc, bin) = GlbChunkParser.Parse(glb);

        if (doc.Meshes is null || doc.Meshes.Count == 0)
        {
            throw new InvalidDataException("GLB has no meshes.");
        }

        return ReadMesh(doc, bin, 0).Primitives[0].Geometry;
    }

    /// <summary>
    /// Reads the full scene tree from a GLB. Returns every mesh referenced by the file
    /// plus the node graph with parent links (parent = -1 for roots). The selected
    /// scene is the file's default <c>scenes[scene]</c>, falling back to index 0 if
    /// unspecified.
    /// </summary>
    public static GltfScene ReadScene(ReadOnlySpan<byte> glb)
    {
        var (doc, bin) = GlbChunkParser.Parse(glb);

        var meshes = new GltfMesh[doc.Meshes?.Count ?? 0];
        for (var i = 0; i < meshes.Length; i++)
        {
            meshes[i] = ReadMesh(doc, bin, i);
        }

        var rawNodes = doc.Nodes ?? new List<GltfNodeRaw>();
        var parentOf = new int[rawNodes.Count];
        for (var i = 0; i < parentOf.Length; i++)
        {
            parentOf[i] = -1;
        }

        for (var i = 0; i < rawNodes.Count; i++)
        {
            var children = rawNodes[i].Children;
            if (children is null)
            {
                continue;
            }

            foreach (var child in children)
            {
                if (child < 0 || child >= rawNodes.Count)
                {
                    throw new InvalidDataException($"Node {i} references invalid child {child}.");
                }

                parentOf[child] = i;
            }
        }

        var nodes = new GltfNode[rawNodes.Count];
        for (var i = 0; i < rawNodes.Count; i++)
        {
            var raw = rawNodes[i];
            var local = ResolveLocalTransform(raw);
            var children = raw.Children?.ToArray() ?? Array.Empty<int>();
            nodes[i] = new GltfNode(
                raw.Name ?? $"node_{i}",
                parentOf[i],
                children,
                local,
                raw.Mesh);
        }

        var sceneIdx = doc.Scene ?? 0;
        var rootNodes = doc.Scenes is { Count: > 0 } scenes && sceneIdx < scenes.Count && scenes[sceneIdx].Nodes is not null
            ? scenes[sceneIdx].Nodes!.ToArray()
            : Array.Empty<int>();

        return new GltfScene(nodes, rootNodes, meshes);
    }

    private static GltfMesh ReadMesh(GltfDocument doc, byte[] bin, int meshIdx)
    {
        var mesh = doc.Meshes![meshIdx];
        if (mesh.Primitives is null || mesh.Primitives.Count == 0)
        {
            throw new InvalidDataException($"Mesh {meshIdx} has no primitives.");
        }

        var meshName = mesh.Name ?? $"mesh_{meshIdx}";
        var primitives = new GltfMeshPrimitive[mesh.Primitives.Count];
        for (var p = 0; p < mesh.Primitives.Count; p++)
        {
            primitives[p] = ReadPrimitive(doc, bin, meshIdx, p, mesh.Primitives[p], meshName);
        }

        return new GltfMesh(meshName, primitives);
    }

    private static GltfMeshPrimitive ReadPrimitive(GltfDocument doc, byte[] bin, int meshIdx, int primIdx, GltfPrimitive prim, string meshName)
    {
        if (prim.Attributes is null || !prim.Attributes.TryGetValue("POSITION", out var posIdx))
        {
            throw new InvalidDataException($"Mesh {meshIdx} primitive {primIdx} has no POSITION attribute.");
        }

        var positions = ReadVec3(doc, bin, posIdx);
        var normals = prim.Attributes.TryGetValue("NORMAL", out var nrmIdx)
            ? ReadVec3(doc, bin, nrmIdx)
            : new Vector3[positions.Length];

        Vector4[]? tangents = null;
        if (prim.Attributes.TryGetValue("TANGENT", out var tanIdx))
        {
            tangents = ReadVec4(doc, bin, tanIdx);
        }

        Vector2[]? uvs = null;
        if (prim.Attributes.TryGetValue("TEXCOORD_0", out var uvIdx))
        {
            uvs = ReadVec2(doc, bin, uvIdx);
        }

        var indices = prim.Indices is int idxAccessor
            ? ReadIndices(doc, bin, idxAccessor)
            : Enumerable.Range(0, positions.Length).Select(i => (uint)i).ToArray();

        var geometry = new MeshData(
            primIdx == 0 ? meshName : $"{meshName}#prim{primIdx}",
            positions,
            normals,
            tangents,
            uvs,
            indices);

        return new GltfMeshPrimitive(geometry, prim.Material);
    }

    private static Matrix4x4 ResolveLocalTransform(GltfNodeRaw raw)
    {
        if (raw.Matrix is { Length: 16 } m)
        {
            // glTF stores matrices column-major. Copying the 16 floats straight into a
            // System.Numerics row-major Matrix4x4 yields the same transformation expressed
            // for row-vector multiplication (which is what the engine uses). Equivalent to
            // applying a transpose in the column-vector → row-vector translation.
            return new Matrix4x4(
                m[0],
                m[1],
                m[2],
                m[3],
                m[4],
                m[5],
                m[6],
                m[7],
                m[8],
                m[9],
                m[10],
                m[11],
                m[12],
                m[13],
                m[14],
                m[15]);
        }

        var translation = raw.Translation is { Length: 3 } t
            ? new Vector3(t[0], t[1], t[2])
            : Vector3.Zero;
        var rotation = raw.Rotation is { Length: 4 } r
            ? new Quaternion(r[0], r[1], r[2], r[3])
            : Quaternion.Identity;
        var scale = raw.Scale is { Length: 3 } s
            ? new Vector3(s[0], s[1], s[2])
            : Vector3.One;

        return Matrix4x4.CreateScale(scale)
             * Matrix4x4.CreateFromQuaternion(rotation)
             * Matrix4x4.CreateTranslation(translation);
    }
}
