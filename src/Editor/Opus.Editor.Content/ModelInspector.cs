using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using Opus.Content.Meshes;
using Opus.Editor.Core;
using Opus.Foundation;

namespace Opus.Editor.Content;

/// <summary>
/// Inspects an imported glTF/GLB model into a <see cref="ModelInspection"/> so the editor can experiment
/// with models without rendering: mesh / primitive / vertex / triangle / material / node counts, tangent
/// and UV presence, and local-space bounds. Pure over the loaded scene; <see cref="TryInspect"/> turns a
/// malformed file into a typed <see cref="ErrorCode.DataValidationFailed"/> result instead of throwing.
/// File reading is the host's job (this stays pure over bytes), matching the content module contract.
/// </summary>
public static class ModelInspector
{
    /// <summary>Parses and inspects a GLB byte payload. A malformed file returns
    /// <see cref="ErrorCode.DataValidationFailed"/> rather than throwing.</summary>
    public static Result<ModelInspection> TryInspect(byte[] glb, string assetPath)
    {
        ArgumentNullException.ThrowIfNull(glb);
        ArgumentException.ThrowIfNullOrEmpty(assetPath);
        try
        {
            return Result<ModelInspection>.Ok(Inspect(GltfBinaryReader.ReadScene(glb), assetPath));
        }
        catch (Exception ex) when (ex is InvalidDataException or NotSupportedException or ArgumentException)
        {
            return Result<ModelInspection>.Err(
                ErrorCode.DataValidationFailed, $"Cannot inspect model '{assetPath}': {ex.Message}");
        }
    }

    /// <summary>Summarises an already-loaded scene. Pure.</summary>
    public static ModelInspection Inspect(GltfScene scene, string assetPath)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentException.ThrowIfNullOrEmpty(assetPath);

        var meshes = new List<ModelMeshSummary>(scene.Meshes.Length);
        var materialIndices = new HashSet<int>();
        Totals totals = default;
        foreach (var mesh in scene.Meshes)
        {
            meshes.Add(SummariseMesh(mesh, materialIndices, ref totals));
        }

        return new ModelInspection(
            assetPath,
            scene.Meshes.Length,
            totals.Primitives,
            totals.Vertices,
            totals.Triangles,
            materialIndices.Count,
            scene.Nodes.Length,
            scene.RootNodes.Length,
            totals.HasTangents,
            totals.HasUvs,
            Float3.FromVector3(totals.BoundsSeeded ? totals.Min : Vector3.Zero),
            Float3.FromVector3(totals.BoundsSeeded ? totals.Max : Vector3.Zero),
            meshes);
    }

    private static ModelMeshSummary SummariseMesh(GltfMesh mesh, HashSet<int> materialIndices, ref Totals totals)
    {
        int meshVertices = 0;
        int meshTriangles = 0;
        foreach (var primitive in mesh.Primitives)
        {
            var geometry = primitive.Geometry;
            totals.Primitives++;
            meshVertices += geometry.VertexCount;
            meshTriangles += geometry.IndexCount / 3;
            totals.HasTangents |= geometry.HasTangents;
            totals.HasUvs |= geometry.HasUvs;
            if (primitive.MaterialIndex is int material)
            {
                materialIndices.Add(material);
            }

            totals.Accumulate(geometry.Positions);
        }

        totals.Vertices += meshVertices;
        totals.Triangles += meshTriangles;
        return new ModelMeshSummary(mesh.Name, mesh.Primitives.Length, meshVertices, meshTriangles);
    }

    private struct Totals
    {
        public int Primitives;
        public int Vertices;
        public int Triangles;
        public bool HasTangents;
        public bool HasUvs;
        public bool BoundsSeeded;
        public Vector3 Min;
        public Vector3 Max;

        public void Accumulate(Vector3[] positions)
        {
            foreach (var p in positions)
            {
                if (!BoundsSeeded)
                {
                    Min = p;
                    Max = p;
                    BoundsSeeded = true;
                    continue;
                }

                Min = Vector3.Min(Min, p);
                Max = Vector3.Max(Max, p);
            }
        }
    }
}
