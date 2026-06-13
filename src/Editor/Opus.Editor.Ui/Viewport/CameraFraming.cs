using System;
using System.Collections.Generic;
using System.Numerics;
using Opus.Editor.Core;
using Opus.Foundation.Geometry;

namespace Opus.Editor.Ui;

/// <summary>
/// The maths behind the F key: what world bounds to frame and how far back the orbit camera must sit so
/// they fit the view. Fitting encloses the bounds in a sphere and backs off until the sphere fills the
/// vertical field of view with a margin — the vertical axis is the tight one on the editor's landscape
/// viewport, so a single-angle fit is conservative without needing the aspect ratio. Pure.
/// </summary>
public static class CameraFraming
{
    /// <summary>Breathing room around the framed bounds (1 = the sphere exactly touches the view edges).</summary>
    public const float Margin = 1.25f;

    /// <summary>The closest a frame ever dollies in — framing a point-like element (an empty node, a light
    /// glyph) lands at a workable editing distance instead of inside it.</summary>
    public const float MinDistance = 2f;

    /// <summary>The orbit distance at which <paramref name="bounds"/> fits the view.</summary>
    public static float FitDistance(Aabb bounds, float fieldOfViewDegrees)
    {
        float radius = (bounds.Max - bounds.Min).Length() * 0.5f;
        float sinHalfFov = MathF.Sin(0.5f * fieldOfViewDegrees * (MathF.PI / 180f));
        return MathF.Max(MinDistance, radius * Margin / sinHalfFov);
    }

    /// <summary>The world box around a light's star glyph — what a frame of that light fits.</summary>
    public static Aabb LightGlyphBounds(SceneLight light)
    {
        ArgumentNullException.ThrowIfNull(light);
        var center = light.Position.ToVector3();
        var extent = new Vector3(LightGizmo.StarRadiusMeters);
        return new Aabb(center - extent, center + extent);
    }

    /// <summary>The union of every visible element's world bounds — what F frames with no selection — or
    /// null when the scene has no visible element to frame.</summary>
    public static Aabb? VisibleSceneBounds(EditorScene scene, IModelBoundsSource bounds)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(bounds);
        Aabb? union = null;
        foreach (var node in scene.Nodes)
        {
            if (!node.Hidden)
            {
                union = UnionWith(union, ScenePickList.WorldBoundsFor(scene, node, bounds));
            }
        }

        foreach (var light in scene.Lights)
        {
            if (!light.Hidden)
            {
                union = UnionWith(union, LightGlyphBounds(light));
            }
        }

        return union;
    }

    /// <summary>The union of the selected elements' world bounds — what F frames for a multi-selection —
    /// or null when no member resolves. Hidden members count: an explicitly selected element is framed
    /// deliberately, unlike the whole-scene frame which skips what is not drawn.</summary>
    public static Aabb? SelectionBounds(
        EditorScene scene, IModelBoundsSource bounds, IReadOnlyList<SceneElementRef> selection)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(bounds);
        ArgumentNullException.ThrowIfNull(selection);
        Aabb? union = null;
        foreach (var element in selection)
        {
            if (element.IsNode && scene.Find(element.AsNode) is { } node)
            {
                union = UnionWith(union, ScenePickList.WorldBoundsFor(scene, node, bounds));
            }
            else if (element.IsLight && scene.FindLight(element.AsLight) is { } light)
            {
                union = UnionWith(union, LightGlyphBounds(light));
            }
        }

        return union;
    }

    private static Aabb UnionWith(Aabb? union, Aabb next) => union is { } existing ? existing.Union(next) : next;
}
