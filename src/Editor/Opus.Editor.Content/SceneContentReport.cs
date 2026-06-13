using System.Collections.Generic;

namespace Opus.Editor.Content;

/// <summary>
/// The developer-facing cost report for a scene: how many nodes, how many distinct assets (resolved vs
/// missing), the total estimated geometry the scene instances, and the per-asset breakdown. Engine-neutral
/// read-only data — the "implementation information for developers" the editor surfaces so a scene's draw
/// cost and broken references are visible at a glance.
/// </summary>
/// <param name="NodeCount">Total scene nodes (including asset-less grouping nodes).</param>
/// <param name="DistinctAssetCount">Number of distinct asset references.</param>
/// <param name="ResolvedAssetCount">Distinct assets that were found and inspected.</param>
/// <param name="MissingAssetCount">Distinct assets that could not be resolved.</param>
/// <param name="TotalVertices">Sum of per-instance vertices across all instances.</param>
/// <param name="TotalTriangles">Sum of per-instance triangles across all instances.</param>
/// <param name="Assets">Per-asset usage, sorted by asset reference.</param>
/// <param name="Lights">The scene's light count, broken down by kind.</param>
public sealed record SceneContentReport(
    int NodeCount,
    int DistinctAssetCount,
    int ResolvedAssetCount,
    int MissingAssetCount,
    long TotalVertices,
    long TotalTriangles,
    IReadOnlyList<SceneAssetUsage> Assets,
    SceneLightTally Lights);
