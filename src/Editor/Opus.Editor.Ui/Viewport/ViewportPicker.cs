using System;
using System.Collections.Generic;
using Opus.Editor.Core;
using Opus.Foundation.Geometry;

namespace Opus.Editor.Ui;

/// <summary>A pickable scene node: its id and its world-space bounding box.</summary>
/// <param name="Id">The node to select on a hit.</param>
/// <param name="WorldBounds">The node's world-space AABB.</param>
public readonly record struct PickCandidate(SceneNodeId Id, Aabb WorldBounds);

/// <summary>A pickable scene element of either kind: its reference and its world-space bounding box.</summary>
/// <param name="Element">The element to select on a hit.</param>
/// <param name="WorldBounds">The element's world-space AABB (a node's model box, a light's glyph box).</param>
public readonly record struct ElementPickCandidate(SceneElementRef Element, Aabb WorldBounds);

/// <summary>The outcome of an element pick: whether anything was hit, which element, and how far.</summary>
/// <param name="Hit">True when a candidate was hit.</param>
/// <param name="Element">The hit element, or <see cref="SceneElementRef.None"/> on a miss.</param>
/// <param name="Distance">Parametric ray distance to the nearest hit, or 0 on a miss.</param>
public readonly record struct ElementPickResult(bool Hit, SceneElementRef Element, float Distance)
{
    public static readonly ElementPickResult Miss = new(false, SceneElementRef.None, 0f);
}

/// <summary>The outcome of a viewport pick: whether anything was hit, which node, and how far.</summary>
/// <param name="Hit">True when a candidate was hit.</param>
/// <param name="Id">The hit node, or <see cref="SceneNodeId.None"/> on a miss.</param>
/// <param name="Distance">Parametric ray distance to the nearest hit, or 0 on a miss.</param>
public readonly record struct PickResult(bool Hit, SceneNodeId Id, float Distance)
{
    public static readonly PickResult Miss = new(false, SceneNodeId.None, 0f);
}

/// <summary>
/// Selects the nearest scene element under a viewport pick ray by testing the ray against each candidate's
/// world-space AABB. Pure; the host supplies the ray (from <see cref="OrbitCamera.PickRay"/>) and the
/// candidate bounds (a node's model bounds transformed into the world, a light's glyph box).
/// </summary>
public static class ViewportPicker
{
    public static PickResult Pick(Ray ray, IReadOnlyList<PickCandidate> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        var best = PickResult.Miss;
        foreach (var candidate in candidates)
        {
            if (RayAabb.Intersects(ray, candidate.WorldBounds, out float distance) &&
                (!best.Hit || distance < best.Distance))
            {
                best = new PickResult(true, candidate.Id, distance);
            }
        }

        return best;
    }

    /// <summary>Picks the nearest element of any kind — the same nearest-AABB rule as <see cref="Pick"/>,
    /// over the mixed node + light candidate set.</summary>
    public static ElementPickResult PickElement(Ray ray, IReadOnlyList<ElementPickCandidate> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        var best = ElementPickResult.Miss;
        foreach (var candidate in candidates)
        {
            if (RayAabb.Intersects(ray, candidate.WorldBounds, out float distance) &&
                (!best.Hit || distance < best.Distance))
            {
                best = new ElementPickResult(true, candidate.Element, distance);
            }
        }

        return best;
    }
}
