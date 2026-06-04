using System.Numerics;

namespace Opus.Content.Meshes;

/// <summary>
/// A scene-tree node loaded from glTF. Holds the local-space transform, parent /
/// children links (parent index = -1 for root), and an optional mesh reference (index
/// into <see cref="GltfScene.Meshes"/>).
/// </summary>
public sealed record GltfNode(
    string Name,
    int ParentIndex,
    int[] ChildIndices,
    Matrix4x4 LocalTransform,
    int? MeshIndex);

/// <summary>
/// One renderable surface of a glTF mesh — a single index/vertex range with a single
/// material binding. glTF allows a mesh to carry multiple primitives so that a tank
/// hull (one mesh) can hold separate index ranges for body / turret / tracks, each
/// drawing with a different material. <see cref="MaterialIndex"/> is null when the
/// primitive declares no material (default material applies).
/// </summary>
public sealed record GltfMeshPrimitive(MeshData Geometry, int? MaterialIndex);

/// <summary>
/// A glTF mesh: name + one or more renderable primitives. Use
/// <see cref="Primitives"/> for full per-primitive rendering with material binding;
/// callers that only need the first geometry can read <c>mesh.Primitives[0].Geometry</c>.
/// </summary>
public sealed record GltfMesh(string Name, GltfMeshPrimitive[] Primitives);

/// <summary>
/// A glTF scene: the full set of meshes referenced by the file, plus a tree of nodes
/// that arrange them in space. Use <see cref="SceneTreeMath.ComputeWorldTransforms"/>
/// to flatten the hierarchy into per-node world matrices for drawing.
/// </summary>
public sealed record GltfScene(
    GltfNode[] Nodes,
    int[] RootNodes,
    GltfMesh[] Meshes);
