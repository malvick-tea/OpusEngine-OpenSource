using System;
using System.Collections.Generic;
using System.Numerics;
using Opus.Editor.Core;

namespace Opus.Editor.Ui;

/// <summary>
/// Assembles the world-space line set the editor viewport draws: a ground grid on the Y=0 plane, each
/// placed node's shape — a primitive node's true wireframe (<see cref="PrimitiveWire"/>), a model node's
/// bounds box (reusing the same world bounds as picking, so what you see is what you click) — a small
/// star-and-aim-ray glyph for every scene light, and the selected element's lines promoted to the
/// Selection role. Pure — the D3D12 seam projects and colours these lines through the existing UI line
/// batch (one render path, ADR-0028 / ADR-0033).
/// </summary>
public static class ViewportSceneDrawList
{
    public const int DefaultGridHalfCount = 10;
    public const float DefaultGridSpacing = 1f;

    /// <summary>Single-selection convenience over the set overload — the shape every pre-multi-select
    /// caller used; <see cref="SceneElementRef.None"/> highlights nothing.</summary>
    public static IReadOnlyList<ViewportLine> Build(
        EditorScene scene,
        IModelBoundsSource bounds,
        SceneElementRef selection,
        int gridHalfCount = DefaultGridHalfCount,
        float gridSpacing = DefaultGridSpacing) =>
        Build(scene, bounds, selection.IsValid ? new[] { selection } : Array.Empty<SceneElementRef>(), gridHalfCount, gridSpacing);

    public static IReadOnlyList<ViewportLine> Build(
        EditorScene scene,
        IModelBoundsSource bounds,
        IReadOnlyList<SceneElementRef> selection,
        int gridHalfCount = DefaultGridHalfCount,
        float gridSpacing = DefaultGridSpacing)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(bounds);
        ArgumentNullException.ThrowIfNull(selection);
        var lines = new List<ViewportLine>();
        AppendGrid(lines, gridHalfCount, gridSpacing);
        foreach (var node in scene.Nodes)
        {
            if (node.Hidden)
            {
                // Hidden elements draw nothing; the outliner stays their management surface.
                continue;
            }

            var role = IsSelected(selection, SceneElementRef.Node(node.Id))
                ? ViewportLineRole.Selection
                : ViewportLineRole.NodeBounds;
            if (ScenePrimitive.TryParse(node.AssetRef) is { } primitive)
            {
                // The composed world matrix (the chain up its parents), so a parented primitive's wireframe
                // follows its parent — the same matrix the pick box uses, so drawn == clickable.
                PrimitiveWire.AppendDrawLines(
                    lines, primitive, SceneNodeTransforms.WorldMatrix(scene, node.Id), role);
            }
            else
            {
                WireBox.AppendEdges(lines, ScenePickList.WorldBoundsFor(scene, node, bounds), role);
            }
        }

        foreach (var light in scene.Lights)
        {
            if (light.Hidden)
            {
                continue;
            }

            var role = IsSelected(selection, SceneElementRef.Light(light.Id))
                ? ViewportLineRole.Selection
                : ViewportLineRole.Light;
            LightGizmo.AppendDrawLines(lines, light, role);
        }

        return lines;
    }

    /// <summary>Whether <paramref name="element"/> is a member of the selection set. A plain loop — the
    /// set is a handful of elements, and the composer calls this once per scene element per frame.</summary>
    private static bool IsSelected(IReadOnlyList<SceneElementRef> selection, SceneElementRef element)
    {
        for (int i = 0; i < selection.Count; i++)
        {
            if (selection[i] == element)
            {
                return true;
            }
        }

        return false;
    }

    private static void AppendGrid(ICollection<ViewportLine> sink, int halfCount, float spacing)
    {
        float extent = halfCount * spacing;
        for (int i = -halfCount; i <= halfCount; i++)
        {
            float offset = i * spacing;
            var role = i == 0 ? ViewportLineRole.GridAxis : ViewportLineRole.Grid;
            sink.Add(new ViewportLine(new Vector3(offset, 0f, -extent), new Vector3(offset, 0f, extent), role));
            sink.Add(new ViewportLine(new Vector3(-extent, 0f, offset), new Vector3(extent, 0f, offset), role));
        }
    }
}
