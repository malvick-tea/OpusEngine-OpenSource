using System;
using System.Collections.Generic;
using System.Globalization;

namespace Opus.Editor.Core;

/// <summary>Node authoring operations of the <see cref="EditorDocument"/> aggregate: place, remove,
/// transform, retune, and duplicate a single node. Each builds and executes one undoable
/// <see cref="ISceneCommand"/> (or a coalesced pair for a drag) through the document's shared command stack
/// and selection.</summary>
public sealed partial class EditorDocument
{
    /// <summary>Places a new node carrying <paramref name="assetRef"/> at <paramref name="transform"/>,
    /// selects it, and returns its freshly allocated id.</summary>
    public SceneNodeId PlaceNode(string name, string? assetRef, EditorTransform transform)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        var id = _scene.AllocateId();
        _commands.Execute(new PlaceNodeCommand(new SceneNode(id, name, assetRef, transform)));
        _selection.SetNode(id);
        MarkChanged();
        return id;
    }

    /// <summary>Places a new empty (asset-less) node at <paramref name="transform"/>, named "node N" from
    /// its freshly allocated id so a window-created node never needs a name prompt, selects it, and returns
    /// the id. One undoable edit — the window's "+ Node" button and A key land here.</summary>
    public SceneNodeId PlaceNewNode(EditorTransform transform)
    {
        var id = _scene.AllocateId();
        string name = string.Create(CultureInfo.InvariantCulture, $"node {id.Value}");
        _commands.Execute(new PlaceNodeCommand(new SceneNode(id, name, null, transform)));
        _selection.SetNode(id);
        MarkChanged();
        return id;
    }

    /// <summary>Places a new primitive-shape node (<see cref="ScenePrimitive"/>) at
    /// <paramref name="transform"/>, named "cube N" (sphere / cylinder / plane / cone) from its freshly
    /// allocated id so a window-created object never needs a name prompt, selects it, and returns the id.
    /// One undoable edit — the window's primitive buttons and the 1–5 keys land here.</summary>
    public SceneNodeId PlaceNewPrimitive(ScenePrimitiveKind kind, EditorTransform transform)
    {
        var id = _scene.AllocateId();
        string name = string.Create(
            CultureInfo.InvariantCulture, $"{ScenePrimitive.DefaultName(kind)} {id.Value}");
        _commands.Execute(new PlaceNodeCommand(new SceneNode(id, name, ScenePrimitive.AssetRef(kind), transform)));
        _selection.SetNode(id);
        MarkChanged();
        return id;
    }

    /// <summary>Removes a node and its whole subtree (the node plus every descendant) as ONE undoable edit,
    /// so deleting a grouping node never leaves its children orphaned with a dangling parent reference. A
    /// childless node removes as the plain single command, so its history label is unchanged. False when the
    /// node is absent.</summary>
    public bool RemoveNode(SceneNodeId id)
    {
        if (!_scene.Contains(id))
        {
            return false;
        }

        var commands = new List<ISceneCommand>();
        foreach (var nodeId in SubtreeIds(id))
        {
            commands.Add(new RemoveNodeCommand(nodeId));
            _selection.Remove(SceneElementRef.Node(nodeId));
        }

        _commands.Execute(Unwrap(commands));
        MarkChanged();
        return true;
    }

    public bool TransformNode(SceneNodeId id, EditorTransform transform)
    {
        if (!_scene.Contains(id))
        {
            return false;
        }

        _commands.Execute(new TransformNodeCommand(id, transform));
        MarkChanged();
        return true;
    }

    /// <summary>Sets a node's transform directly for a live drag preview — marks the document dirty and
    /// raises <see cref="Changed"/>, but records NO undo step. Pair with <see cref="CommitNodeTransform"/>
    /// on drag end so the whole gesture collapses to one reversible edit. No-op (false) for a missing
    /// node.</summary>
    public bool PreviewNodeTransform(SceneNodeId id, EditorTransform transform)
    {
        var node = _scene.Find(id);
        if (node is null)
        {
            return false;
        }

        _scene.Replace(node.WithTransform(transform));
        MarkChanged();
        return true;
    }

    /// <summary>Commits a coalesced drag as a single reversible edit: one command whose undo restores
    /// <paramref name="from"/> and whose redo reapplies <paramref name="to"/>, regardless of how many
    /// preview frames the drag spanned. No-op (false) for a missing node.</summary>
    public bool CommitNodeTransform(SceneNodeId id, EditorTransform from, EditorTransform to)
    {
        if (!_scene.Contains(id))
        {
            return false;
        }

        _commands.Execute(new TransformNodeCommand(id, from, to));
        MarkChanged();
        return true;
    }

    /// <summary>Replaces a node's asset reference as one undoable edit — null clears the node back to an
    /// empty grouping node. No-op (false) for a missing node or an unchanged reference.</summary>
    public bool SetNodeAsset(SceneNodeId id, string? assetRef)
    {
        var node = _scene.Find(id);
        if (node is null || string.Equals(node.AssetRef, assetRef, StringComparison.Ordinal))
        {
            return false;
        }

        _commands.Execute(new SetNodeAssetCommand(id, assetRef));
        MarkChanged();
        return true;
    }

    /// <summary>Sets a node's editor visibility as one undoable edit. The selection is untouched — a
    /// hidden node stays selected (the outliner still lists it). False when the node is absent or already
    /// in that state.</summary>
    public bool SetNodeHidden(SceneNodeId id, bool hidden)
    {
        var node = _scene.Find(id);
        if (node is null || node.Hidden == hidden)
        {
            return false;
        }

        _commands.Execute(new SetNodeHiddenCommand(id, hidden));
        MarkChanged();
        return true;
    }

    public bool RenameNode(SceneNodeId id, string name)
    {
        ArgumentException.ThrowIfNullOrEmpty(name);
        if (!_scene.Contains(id))
        {
            return false;
        }

        _commands.Execute(new RenameNodeCommand(id, name));
        MarkChanged();
        return true;
    }

    /// <summary>Clones <paramref name="id"/> as a new selected node — same asset, rotation, and scale, the
    /// name suffixed with " copy", and the position either <paramref name="atPosition"/> or offset one metre
    /// along X so the copy does not sit exactly on the original. One undoable edit (a placement). Returns the
    /// new node's id, or <see cref="SceneNodeId.None"/> when <paramref name="id"/> is not in the scene. Shared
    /// by the <c>scene-duplicate</c> CLI and the in-window duplicate action so both clone identically.</summary>
    public SceneNodeId DuplicateNode(SceneNodeId id, Float3? atPosition = null)
    {
        var source = _scene.Find(id);
        if (source is null)
        {
            return SceneNodeId.None;
        }

        var position = atPosition ?? OffsetForCopy(source.Transform.Position);
        return PlaceNode(source.Name + DuplicateNameSuffix, source.AssetRef, source.Transform with { Position = position });
    }
}
