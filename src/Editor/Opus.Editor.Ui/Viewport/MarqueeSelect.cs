using System;
using System.Collections.Generic;
using System.Numerics;
using Opus.Editor.Core;

namespace Opus.Editor.Ui;

/// <summary>
/// The box-select containment rule: an element is inside the marquee when its anchor point — a node's
/// position, a light's position — projects inside the dragged screen rectangle. Anchor containment
/// (rather than bounds overlap) keeps the gesture predictable: the box takes exactly what is anchored in
/// it, and a sliver of a large object brushing the box edge never joins the selection. Hidden elements
/// are never collected (what is not drawn is not selectable, the visibility rule), and an anchor on or
/// behind the camera plane never projects. Pure — the controller feeds the camera matrix and the
/// pixel rectangle, so the rule is unit-tested without a window.
/// </summary>
public static class MarqueeSelect
{
    /// <summary>Collects every visible element whose anchor projects inside the rectangle
    /// (<paramref name="rectMin"/> / <paramref name="rectMax"/>, in viewport pixels), nodes first then
    /// lights, each kind in scene order.</summary>
    public static List<SceneElementRef> Collect(
        EditorScene scene,
        Matrix4x4 viewProjection,
        int viewportWidth,
        int viewportHeight,
        Vector2 rectMin,
        Vector2 rectMax)
    {
        ArgumentNullException.ThrowIfNull(scene);
        var collected = new List<SceneElementRef>();
        foreach (var node in scene.Nodes)
        {
            if (!node.Hidden && Inside(
                node.Transform.Position.ToVector3(), viewProjection, viewportWidth, viewportHeight, rectMin, rectMax))
            {
                collected.Add(SceneElementRef.Node(node.Id));
            }
        }

        foreach (var light in scene.Lights)
        {
            if (!light.Hidden && Inside(
                light.Position.ToVector3(), viewProjection, viewportWidth, viewportHeight, rectMin, rectMax))
            {
                collected.Add(SceneElementRef.Light(light.Id));
            }
        }

        return collected;
    }

    private static bool Inside(
        Vector3 world, Matrix4x4 viewProjection, int width, int height, Vector2 min, Vector2 max) =>
        WorldScreenProjector.TryProject(world, viewProjection, width, height, out var screen) &&
        screen.X >= min.X && screen.X <= max.X && screen.Y >= min.Y && screen.Y <= max.Y;
}
