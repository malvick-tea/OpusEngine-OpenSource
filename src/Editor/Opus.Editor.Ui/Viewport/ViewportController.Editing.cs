using System.Collections.Generic;
using System.Numerics;
using Opus.Editor.Core;

namespace Opus.Editor.Ui;

/// <summary>Editing actions of the <see cref="ViewportController"/>: undo / redo, create (node / light /
/// primitive at the camera target), delete, duplicate, nudge, hide, parent / unparent, group / ungroup,
/// shape / kind cycling, and the cross-scene clipboard. Each is one undoable document edit (or a UI-state
/// camera-target placement); the controller holds the clipboard because it outlives a scene load.</summary>
public sealed partial class ViewportController
{
    private readonly List<SceneNode> _clipboardNodes = new();
    private readonly List<SceneLight> _clipboardLights = new();
    private Vector3 _clipboardAnchor;

    /// <summary>Sets the gizmo mode. A mode change mid-drag does not affect the in-progress gesture, which
    /// keeps the mode it began with.</summary>
    public void SetGizmoMode(GizmoMode mode) => GizmoMode = mode;

    /// <summary>Undoes the last edit; false when there is nothing to undo.</summary>
    public bool Undo() => _document.Undo();

    /// <summary>Redoes the last undone edit; false when there is nothing to redo.</summary>
    public bool Redo() => _document.Redo();

    /// <summary>Deletes every selected element — nodes and lights — as one undoable edit; false when
    /// nothing is selected.</summary>
    public bool DeleteSelected() => _document.RemoveElements(_document.SelectedElements);

    /// <summary>Duplicates every selected element as one undoable edit — the offset copies become the new
    /// selection, ready for a group drag; false when nothing is selected.</summary>
    public bool DuplicateSelected() => _document.DuplicateElements(_document.SelectedElements);

    /// <summary>Places a new empty node at the camera target and selects it — the window's "+ Node" button
    /// and A key. The target is where the author is looking, so the node lands centred in the view.</summary>
    public SceneNodeId AddNodeAtTarget() =>
        _document.PlaceNewNode(EditorTransform.Identity with { Position = Float3.FromVector3(Camera.Target) });

    /// <summary>Adds a new point light at the camera target — the window's "+ Light" button and L key.</summary>
    public SceneLightId AddPointLightAtTarget() =>
        _document.AddNewPointLight(Float3.FromVector3(Camera.Target));

    /// <summary>Places a new primitive-shape object at the camera target and selects it — the window's
    /// primitive toolbar buttons and the 1–5 keys. The target is where the author is looking, so the new
    /// object lands centred in the view.</summary>
    public SceneNodeId AddPrimitiveAtTarget(ScenePrimitiveKind kind) =>
        _document.PlaceNewPrimitive(kind, EditorTransform.Identity with { Position = Float3.FromVector3(Camera.Target) });

    /// <summary>True when copied elements are waiting to be pasted. The clipboard lives on the controller —
    /// not the document — so it survives Ctrl+N / Ctrl+O scene switches: copy in one scene, paste in
    /// another. It is never serialised and never affects the dirty flag.</summary>
    public bool HasClipboard => _clipboardNodes.Count > 0 || _clipboardLights.Count > 0;

    /// <summary>Copies the whole selection — nodes and lights — onto the controller clipboard (Ctrl+C),
    /// anchored at the primary element's position so a paste can preserve the members' relative offsets.
    /// The scene's elements are immutable values, so the held copies are independent of later edits or
    /// deletion of the sources. False when nothing is selected; the previous clipboard is then kept.</summary>
    public bool CopySelected()
    {
        var primary = _document.SelectedElement;
        Vector3? anchor = primary.IsNode
            ? _document.Scene.Find(primary.AsNode)?.Transform.Position.ToVector3()
            : _document.Scene.FindLight(primary.AsLight)?.Position.ToVector3();
        if (anchor is not { } anchorPosition)
        {
            return false;
        }

        _clipboardNodes.Clear();
        _clipboardLights.Clear();
        foreach (var element in _document.SelectedElements)
        {
            if (element.IsNode && _document.Scene.Find(element.AsNode) is { } node)
            {
                _clipboardNodes.Add(node);
            }
            else if (element.IsLight && _document.Scene.FindLight(element.AsLight) is { } light)
            {
                _clipboardLights.Add(light);
            }
        }

        _clipboardAnchor = anchorPosition;
        return true;
    }

    /// <summary>Pastes the clipboard at the camera target as one undoable edit (Ctrl+V): the copied
    /// primary lands on the target and every other member keeps its offset from it, so a copied group
    /// arrives intact — like every other creation action, centred where the author looks. Names, assets /
    /// kinds, and non-positional fields carry over; a paste is always visible, so copied hidden elements
    /// paste unhidden; the copies become the new selection. The clipboard is kept, so repeated pastes
    /// stamp repeated groups. False when the clipboard is empty.</summary>
    public bool PasteAtTarget()
    {
        if (!HasClipboard)
        {
            return false;
        }

        var delta = Camera.Target - _clipboardAnchor;
        var nodes = new List<SceneNode>(_clipboardNodes.Count);
        foreach (var node in _clipboardNodes)
        {
            nodes.Add(node.WithTransform(node.Transform with
            {
                Position = Float3.FromVector3(node.Transform.Position.ToVector3() + delta),
            }));
        }

        var lights = new List<SceneLight>(_clipboardLights.Count);
        foreach (var light in _clipboardLights)
        {
            lights.Add(light with { Position = Float3.FromVector3(light.Position.ToVector3() + delta) });
        }

        return _document.PasteElements(nodes, lights);
    }

    /// <summary>Nudges the whole selection by <paramref name="delta"/> world metres as one undoable edit —
    /// the arrow keys' grid-step move, group-aware like the drags. No preview phase: each key press is its
    /// own committed (and so individually undoable) step. False when nothing is selected.</summary>
    public bool NudgeSelection(Vector3 delta)
    {
        var nodes = new List<NodeMove>();
        var lights = new List<LightMove>();
        foreach (var element in _document.SelectedElements)
        {
            if (element.IsNode && _document.Scene.Find(element.AsNode) is { } node)
            {
                var to = node.Transform with
                {
                    Position = Float3.FromVector3(node.Transform.Position.ToVector3() + delta),
                };
                nodes.Add(new NodeMove(node.Id, node.Transform, to));
            }
            else if (element.IsLight && _document.Scene.FindLight(element.AsLight) is { } light)
            {
                lights.Add(new LightMove(
                    light, light with { Position = Float3.FromVector3(light.Position.ToVector3() + delta) }));
            }
        }

        return _document.CommitGroupTransform(nodes, lights);
    }

    /// <summary>Toggles the selection's viewport visibility as one undoable edit (the V key). The group
    /// rule: if any selected element is visible the whole selection hides; an all-hidden selection shows.
    /// A hidden element draws nothing and is not click-pickable, but stays selected and listed in the
    /// outliner — which is also where it is selected again to unhide. False when nothing is selected.</summary>
    public bool ToggleSelectedHidden()
    {
        bool anyVisible = false;
        foreach (var element in _document.SelectedElements)
        {
            bool hidden = element.IsNode
                ? _document.Scene.Find(element.AsNode) is { Hidden: true }
                : _document.Scene.FindLight(element.AsLight) is { Hidden: true };
            if (!hidden)
            {
                anyVisible = true;
                break;
            }
        }

        return _document.SetElementsHidden(_document.SelectedElements, anyVisible);
    }

    /// <summary>Parents every other selected node under the primary (last-selected) node, preserving world
    /// positions, as one undoable edit — the in-window "group these under that" gesture (the P key). False
    /// when the primary is not a node or nothing else is selected.</summary>
    public bool ParentSelectionToPrimary()
    {
        var primary = _document.SelectedElement;
        if (!primary.IsNode)
        {
            return false;
        }

        var parent = primary.AsNode;
        var children = new List<SceneNodeId>();
        foreach (var element in _document.SelectedElements)
        {
            if (element.IsNode && element.AsNode != parent)
            {
                children.Add(element.AsNode);
            }
        }

        return children.Count > 0 && _document.ReparentNodesKeepingWorld(children, parent);
    }

    /// <summary>Detaches every selected node to a root, preserving world positions, as one undoable edit (the
    /// Shift+P key). False when no node is selected or none was parented.</summary>
    public bool UnparentSelection()
    {
        var children = new List<SceneNodeId>();
        foreach (var element in _document.SelectedElements)
        {
            if (element.IsNode)
            {
                children.Add(element.AsNode);
            }
        }

        return children.Count > 0 && _document.ReparentNodesKeepingWorld(children, null);
    }

    /// <summary>Groups the selected nodes under a new empty parent at their centroid as one undoable edit
    /// (Ctrl+G), the new group selected. False when no node is selected.</summary>
    public bool GroupSelection()
    {
        var nodes = SelectedNodeIds();
        return nodes.Count > 0 && _document.GroupNodes(nodes).IsValid;
    }

    /// <summary>Ungroups the selected group nodes as one undoable edit (Ctrl+Shift+G): each group's children
    /// are promoted to the group's parent (or a root), preserving world positions; an empty group node is
    /// removed while a node carrying an asset keeps its place and only releases its children. The promoted
    /// children become the selection. False when no node is selected or nothing was ungrouped.</summary>
    public bool UngroupSelection()
    {
        var nodes = SelectedNodeIds();
        return nodes.Count > 0 && _document.UngroupNodes(nodes);
    }

    /// <summary>The selected elements that are nodes, in selection order — the input to the group / ungroup
    /// hierarchy ops, which act on nodes only (lights are never parented).</summary>
    private List<SceneNodeId> SelectedNodeIds()
    {
        var nodes = new List<SceneNodeId>();
        foreach (var element in _document.SelectedElements)
        {
            if (element.IsNode)
            {
                nodes.Add(element.AsNode);
            }
        }

        return nodes;
    }

    /// <summary>Cycles the selected node's shape — empty, cube, sphere, cylinder, plane, cone, empty — as
    /// one undoable edit (the inspector's asset-row click). Only empty and primitive nodes cycle: a node
    /// carrying a real content reference (a model path) never loses it to a stray click. False when the
    /// selection is not such a node.</summary>
    public bool CycleSelectedNodeShape()
    {
        var element = _document.SelectedElement;
        if (!element.IsNode || _document.Scene.Find(element.AsNode) is not { } node)
        {
            return false;
        }

        string? next;
        if (node.AssetRef is null)
        {
            next = ScenePrimitive.AssetRef(ScenePrimitiveKind.Cube);
        }
        else if (ScenePrimitive.TryParse(node.AssetRef) is { } kind)
        {
            next = kind switch
            {
                ScenePrimitiveKind.Cube => ScenePrimitive.AssetRef(ScenePrimitiveKind.Sphere),
                ScenePrimitiveKind.Sphere => ScenePrimitive.AssetRef(ScenePrimitiveKind.Cylinder),
                ScenePrimitiveKind.Cylinder => ScenePrimitive.AssetRef(ScenePrimitiveKind.Plane),
                ScenePrimitiveKind.Plane => ScenePrimitive.AssetRef(ScenePrimitiveKind.Cone),
                _ => null,
            };
        }
        else
        {
            return false;
        }

        return _document.SetNodeAsset(node.Id, next);
    }

    /// <summary>Cycles the selected light's kind — point, spot, directional, point — as one undoable edit
    /// (the inspector's kind-row click). Parameters a kind needs but the light never had are seeded with
    /// the scene defaults (a fresh point light cycled to spot gets a real cone, not a zero-degree one);
    /// authored values are never overwritten. False when the selection is not a light.</summary>
    public bool CycleSelectedLightKind()
    {
        var element = _document.SelectedElement;
        if (!element.IsLight || _document.Scene.FindLight(element.AsLight) is not { } light)
        {
            return false;
        }

        var next = light.Kind switch
        {
            SceneLightKind.Point => SceneLightKind.Spot,
            SceneLightKind.Spot => SceneLightKind.Directional,
            _ => SceneLightKind.Point,
        };

        var changed = light with { Kind = next };
        if (next == SceneLightKind.Spot && changed.SpotOuterAngleDegrees <= 0f)
        {
            changed = changed with
            {
                SpotInnerAngleDegrees = SceneLight.DefaultSpotInnerAngleDegrees,
                SpotOuterAngleDegrees = SceneLight.DefaultSpotOuterAngleDegrees,
            };
        }

        if (next != SceneLightKind.Point && changed.Direction == Float3.Zero)
        {
            changed = changed with { Direction = SceneLight.DefaultDirection };
        }

        if (next != SceneLightKind.Directional && changed.Range <= 0f)
        {
            changed = changed with { Range = SceneLight.DefaultRangeMeters };
        }

        return _document.SetLight(changed);
    }
}
