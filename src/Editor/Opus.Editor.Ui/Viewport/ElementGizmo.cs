using System;
using System.Numerics;
using Opus.Editor.Core;

namespace Opus.Editor.Ui;

/// <summary>
/// Decides where the selected element's transform gizmo sits and whether the element supports the active
/// gizmo mode at all: nodes move / scale / rotate, every light moves, only aiming lights (directional /
/// spot) rotate, and no light scales. The frame composer (drawing) and the viewport controller (picking and
/// dragging) both route through this one rule, so the gizmo that is drawn is exactly the gizmo that
/// responds. Pure.
/// </summary>
public static class ElementGizmo
{
    /// <summary>The gizmo origin for the document's selected element in <paramref name="mode"/>, or null
    /// when no gizmo applies — nothing selected, the element is missing, or the element does not support
    /// the mode (scale on a light, rotate on a point light).</summary>
    public static Vector3? Origin(EditorDocument document, GizmoMode mode)
    {
        ArgumentNullException.ThrowIfNull(document);
        var element = document.SelectedElement;
        if (element.IsNode && document.Scene.Find(element.AsNode) is { } node)
        {
            // The composed world position (the chain up its parents), so the gizmo sits on the node box,
            // not at the node's parent-relative local position. World == local for a root node.
            return SceneNodeTransforms.WorldMatrix(document.Scene, node.Id).Translation;
        }

        if (element.IsLight && document.Scene.FindLight(element.AsLight) is { } light && LightSupports(light, mode))
        {
            return light.Position.ToVector3();
        }

        return null;
    }

    /// <summary>Whether a light edits in this gizmo mode: every light moves, lights that aim somewhere
    /// (directional / spot) rotate, and no light scales.</summary>
    public static bool LightSupports(SceneLight light, GizmoMode mode)
    {
        ArgumentNullException.ThrowIfNull(light);
        return mode switch
        {
            GizmoMode.Translate => true,
            GizmoMode.Rotate => light.Kind != SceneLightKind.Point,
            _ => false,
        };
    }
}
