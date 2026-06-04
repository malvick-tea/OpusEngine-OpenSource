using System.Numerics;

namespace Opus.Engine.Renderer.Direct3D12.Assets;

/// <summary>One node in a loaded scene: the mesh it references plus the world-space
/// transform that places it, plus optional per-instance shading data and the source node
/// index when the draw came from glTF. Produced
/// by <see cref="D3D12GltfSceneLoader"/> when it flattens a glTF node graph; consumed
/// by render passes that walk the draw list and issue one (or more, for multi-primitive
/// meshes) GPU draws per node.</summary>
/// <remarks>
/// <para>
/// <see cref="TintFactor"/> multiplies the resolved material's <c>BaseColorFactor</c>
/// in the per-object root constants, so the same loaded glTF asset can render with
/// different visual tints per instance (e.g. per-faction camo: olive, grey, winter white)
/// without uploading separate albedo textures.
/// <c>Vector4.One</c> = no tint (identity multiply).
/// </para>
/// <para>
/// The 2-argument constructor preserves the pre-M4.ee call shape — every call site that
/// doesn't care about tinting (procedural test geometry, the floor primitive, projectiles)
/// stays unchanged and renders with identity tint.
/// </para>
/// </remarks>
public readonly record struct SceneNodeDraw(
    int MeshIndex,
    Matrix4x4 World,
    Vector4 TintFactor,
    Vector2 UvOffset,
    int NodeIndex)
{
    public const int NoNodeIndex = -1;

    /// <summary>Construct a draw with identity tint (<c>Vector4.One</c>) — equivalent to
    /// the pre-M4.ee shape. Most callers that don't need per-instance tinting keep using
    /// this overload. Parameter names match the record's positional members so callers
    /// using named-argument syntax (<c>new SceneNodeDraw(MeshIndex: 0, ...)</c>) keep
    /// working unchanged.</summary>
    public SceneNodeDraw(int MeshIndex, Matrix4x4 World)
        : this(MeshIndex, World, Vector4.One, Vector2.Zero, NoNodeIndex)
    {
    }

    public SceneNodeDraw(int MeshIndex, Matrix4x4 World, Vector4 TintFactor)
        : this(MeshIndex, World, TintFactor, Vector2.Zero, NoNodeIndex)
    {
    }

    public SceneNodeDraw(int MeshIndex, Matrix4x4 World, Vector4 TintFactor, Vector2 UvOffset)
        : this(MeshIndex, World, TintFactor, UvOffset, NoNodeIndex)
    {
    }
}
