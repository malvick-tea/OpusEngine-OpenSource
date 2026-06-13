namespace Opus.Editor.Content;

/// <summary>
/// How one asset is used in a scene report: the asset reference, how many nodes instance it, its
/// per-instance vertex / triangle counts, and whether the asset resolved (false = a missing or
/// unreadable file, surfaced so a developer can spot broken references).
/// </summary>
/// <param name="AssetRef">The package-relative asset reference.</param>
/// <param name="InstanceCount">Number of nodes referencing this asset.</param>
/// <param name="VertexCount">Vertices per instance (0 when unresolved).</param>
/// <param name="TriangleCount">Triangles per instance (0 when unresolved).</param>
/// <param name="Resolved">True when the asset was found and inspected.</param>
public sealed record SceneAssetUsage(
    string AssetRef, int InstanceCount, int VertexCount, int TriangleCount, bool Resolved);
