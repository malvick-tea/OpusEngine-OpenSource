using System.Numerics;

namespace Opus.Content.Meshes;

/// <summary>
/// Loaded mesh in a renderer-agnostic CPU representation. Owns its arrays — caller is
/// free to discard the source bytes after construction.
///
/// <list type="bullet">
/// <item><description><see cref="Positions"/> — required, one Vector3 per vertex.</description></item>
/// <item><description><see cref="Normals"/> — required (filled with zeros if the source had none).</description></item>
/// <item><description><see cref="Tangents"/> — optional. glTF tangents are 4-component (xyz + handedness sign in w).</description></item>
/// <item><description><see cref="Uvs"/> — optional UV-0 channel.</description></item>
/// <item><description><see cref="Indices"/> — always populated; if the source mesh was non-indexed, this is the trivial 0,1,2,...n sequence.</description></item>
/// </list>
/// </summary>
public sealed record MeshData(
    string Name,
    Vector3[] Positions,
    Vector3[] Normals,
    Vector4[]? Tangents,
    Vector2[]? Uvs,
    uint[] Indices)
{
    public int VertexCount => Positions.Length;

    public int IndexCount => Indices.Length;

    public bool HasTangents => Tangents is not null;

    public bool HasUvs => Uvs is not null;
}
