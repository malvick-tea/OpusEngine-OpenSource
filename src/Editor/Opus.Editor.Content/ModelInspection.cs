using System.Collections.Generic;
using Opus.Editor.Core;

namespace Opus.Editor.Content;

/// <summary>
/// A read-only summary of an imported model: the counts a developer needs to judge a glTF/GLB at a glance
/// (meshes, primitives / submeshes, vertices, triangles, referenced materials, scene nodes), the presence
/// of tangents / UVs, the model's local-space bounds, and a per-mesh breakdown. Produced by
/// <see cref="ModelInspector"/>; engine-neutral.
/// </summary>
/// <param name="AssetPath">Source path the inspection was run against.</param>
/// <param name="MeshCount">Number of meshes in the scene.</param>
/// <param name="PrimitiveCount">Total renderable primitives across all meshes.</param>
/// <param name="VertexCount">Total vertices across all meshes.</param>
/// <param name="TriangleCount">Total triangles across all meshes.</param>
/// <param name="MaterialReferenceCount">Distinct material indices referenced by primitives.</param>
/// <param name="NodeCount">Number of scene-tree nodes.</param>
/// <param name="RootNodeCount">Number of root nodes in the default scene.</param>
/// <param name="HasTangents">True when any primitive supplies tangents.</param>
/// <param name="HasUvs">True when any primitive supplies a UV channel.</param>
/// <param name="BoundsMin">Local-space minimum corner over all mesh vertices.</param>
/// <param name="BoundsMax">Local-space maximum corner over all mesh vertices.</param>
/// <param name="Meshes">Per-mesh summaries, in file order.</param>
public sealed record ModelInspection(
    string AssetPath,
    int MeshCount,
    int PrimitiveCount,
    int VertexCount,
    int TriangleCount,
    int MaterialReferenceCount,
    int NodeCount,
    int RootNodeCount,
    bool HasTangents,
    bool HasUvs,
    Float3 BoundsMin,
    Float3 BoundsMax,
    IReadOnlyList<ModelMeshSummary> Meshes)
{
    /// <summary>The local-space bounding-box size (max - min per axis).</summary>
    public Float3 BoundsSize => new(
        BoundsMax.X - BoundsMin.X,
        BoundsMax.Y - BoundsMin.Y,
        BoundsMax.Z - BoundsMin.Z);
}
