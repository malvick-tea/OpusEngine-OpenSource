using System;
using System.Collections.Generic;
using System.Numerics;
using Opus.Editor.Core;
using Opus.Foundation.Geometry;

namespace Opus.Editor.Ui;

/// <summary>
/// Builds viewport pick candidates from a scene: each node's local bounds — a primitive node's shape
/// bounds (<see cref="PrimitiveWire.LocalBounds"/>), a model node's inspected bounds, or a small default
/// box when neither resolves — transformed into world space by the node's transform, and — for the
/// element-level pick — a box around each light's star glyph, so what the viewport draws is exactly what
/// a click hits. Pure.
/// </summary>
public static class ScenePickList
{
    public const float DefaultHalfExtent = 0.5f;

    public static IReadOnlyList<PickCandidate> Build(
        EditorScene scene, IModelBoundsSource bounds, float fallbackHalfExtent = DefaultHalfExtent)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(bounds);
        var candidates = new List<PickCandidate>(scene.Count);
        foreach (var node in scene.Nodes)
        {
            if (node.Hidden)
            {
                // What is not drawn must not be clickable; hidden elements select via the outliner.
                continue;
            }

            candidates.Add(new PickCandidate(node.Id, WorldBoundsFor(scene, node, bounds, fallbackHalfExtent)));
        }

        return candidates;
    }

    /// <summary>One node's world-space bounds: its local bounds transformed by its composed world matrix
    /// (the chain up its parents — <see cref="SceneNodeTransforms.WorldMatrix(EditorScene, SceneNodeId)"/>),
    /// so a parented node's box follows its parent. Shared by the pick candidates and the viewport draw list
    /// so picking and drawing can never disagree.</summary>
    public static Aabb WorldBoundsFor(
        EditorScene scene, SceneNode node, IModelBoundsSource bounds, float fallbackHalfExtent = DefaultHalfExtent)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(node);
        ArgumentNullException.ThrowIfNull(bounds);
        return LocalBoundsFor(node, bounds, fallbackHalfExtent)
            .Transform(SceneNodeTransforms.WorldMatrix(scene, node.Id));
    }

    /// <summary>Builds the mixed node + light candidate set the viewport click picks from: node boxes as
    /// <see cref="Build"/>, plus each light's glyph box (<see cref="LightGizmo.StarRadiusMeters"/> around
    /// its position).</summary>
    public static IReadOnlyList<ElementPickCandidate> BuildElements(
        EditorScene scene, IModelBoundsSource bounds, float fallbackHalfExtent = DefaultHalfExtent)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(bounds);
        var candidates = new List<ElementPickCandidate>(scene.Count + scene.LightCount);
        foreach (var node in Build(scene, bounds, fallbackHalfExtent))
        {
            candidates.Add(new ElementPickCandidate(SceneElementRef.Node(node.Id), node.WorldBounds));
        }

        var glyphExtent = new Vector3(LightGizmo.StarRadiusMeters);
        foreach (var light in scene.Lights)
        {
            if (light.Hidden)
            {
                continue;
            }

            var center = light.Position.ToVector3();
            candidates.Add(new ElementPickCandidate(
                SceneElementRef.Light(light.Id), new Aabb(center - glyphExtent, center + glyphExtent)));
        }

        return candidates;
    }

    private static Aabb LocalBoundsFor(SceneNode node, IModelBoundsSource bounds, float fallbackHalfExtent)
    {
        if (ScenePrimitive.TryParse(node.AssetRef) is { } primitive)
        {
            return PrimitiveWire.LocalBounds(primitive);
        }

        if (node.AssetRef is not null &&
            bounds.TryGetLocalBounds(node.AssetRef) is { IsEmpty: false } resolved)
        {
            return resolved;
        }

        var extent = new Vector3(fallbackHalfExtent);
        return new Aabb(-extent, extent);
    }
}
