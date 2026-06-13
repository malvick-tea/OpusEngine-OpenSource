using System;
using System.Collections.Generic;
using Opus.Editor.Core;

namespace Opus.Editor.Content;

/// <summary>
/// Builds a <see cref="SceneContentReport"/> for a scene by inspecting each distinct asset once and
/// multiplying its geometry by its instance count. Pure: the caller supplies an inspect delegate (which
/// owns any file IO and returns null for a missing / unreadable asset), so the aggregation logic stays
/// headless and testable.
/// </summary>
public static class SceneContentReporter
{
    public static SceneContentReport Build(EditorScene scene, Func<string, ModelInspection?> inspect)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(inspect);

        var instanceCounts = CountInstances(scene);
        var usages = new List<SceneAssetUsage>(instanceCounts.Count);
        int resolved = 0;
        int missing = 0;
        long totalVertices = 0;
        long totalTriangles = 0;
        foreach (var (assetRef, instances) in instanceCounts)
        {
            var inspection = inspect(assetRef);
            int vertices = inspection?.VertexCount ?? 0;
            int triangles = inspection?.TriangleCount ?? 0;
            usages.Add(new SceneAssetUsage(assetRef, instances, vertices, triangles, inspection is not null));
            if (inspection is not null)
            {
                resolved++;
            }
            else
            {
                missing++;
            }

            totalVertices += (long)vertices * instances;
            totalTriangles += (long)triangles * instances;
        }

        usages.Sort(static (a, b) => string.CompareOrdinal(a.AssetRef, b.AssetRef));
        return new SceneContentReport(
            scene.Count, instanceCounts.Count, resolved, missing, totalVertices, totalTriangles, usages,
            TallyLights(scene));
    }

    private static SceneLightTally TallyLights(EditorScene scene)
    {
        int directional = 0;
        int point = 0;
        int spot = 0;
        foreach (var light in scene.Lights)
        {
            switch (light.Kind)
            {
                case SceneLightKind.Point:
                    point++;
                    break;
                case SceneLightKind.Spot:
                    spot++;
                    break;
                default:
                    directional++;
                    break;
            }
        }

        return new SceneLightTally(directional, point, spot);
    }

    private static Dictionary<string, int> CountInstances(EditorScene scene)
    {
        var instanceCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var node in scene.Nodes)
        {
            if (node.AssetRef is { } assetRef)
            {
                instanceCounts.TryGetValue(assetRef, out int count);
                instanceCounts[assetRef] = count + 1;
            }
        }

        return instanceCounts;
    }
}
