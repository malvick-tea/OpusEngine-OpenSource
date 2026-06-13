using System;
using System.Collections.Generic;

namespace Opus.Editor.Core;

/// <summary>Multi-element group operations of the <see cref="EditorDocument"/> aggregate: remove, hide,
/// duplicate, paste, and commit a coalesced drag over a whole selection (nodes and lights together). Each
/// collapses to ONE undoable history entry — a single surviving command executes plainly (so single-selection
/// history is byte-identical), several wrap into a <see cref="CompositeSceneCommand"/>.</summary>
public sealed partial class EditorDocument
{
    /// <summary>Removes every listed element — nodes and lights — as ONE undoable edit (the group delete
    /// over a multi-selection). Refs not in the scene and duplicates are skipped; false when nothing was
    /// removed. A single removal executes as the plain command, so single-selection behaviour and its
    /// history label are unchanged.</summary>
    public bool RemoveElements(IReadOnlyList<SceneElementRef> elements)
    {
        ArgumentNullException.ThrowIfNull(elements);
        var commands = new List<ISceneCommand>();
        var removedNodes = new HashSet<SceneNodeId>();
        foreach (var element in Distinct(elements))
        {
            if (element.IsNode && _scene.Contains(element.AsNode))
            {
                // Expand each selected node to its subtree so a deleted parent takes its children with it;
                // the dedup set keeps a parent-and-child selection from removing the child twice.
                foreach (var nodeId in SubtreeIds(element.AsNode))
                {
                    if (removedNodes.Add(nodeId))
                    {
                        commands.Add(new RemoveNodeCommand(nodeId));
                    }
                }
            }
            else if (element.IsLight && _scene.ContainsLight(element.AsLight))
            {
                commands.Add(new RemoveLightCommand(element.AsLight));
            }
        }

        if (commands.Count == 0)
        {
            return false;
        }

        _commands.Execute(Unwrap(commands));
        _selection.Clamp();
        MarkChanged();
        return true;
    }

    /// <summary>Sets every listed element's editor visibility to <paramref name="hidden"/> as ONE undoable
    /// edit (the group V toggle). Elements already in that state contribute nothing; false when no element
    /// changed. The selection is untouched, mirroring <see cref="SetNodeHidden"/>.</summary>
    public bool SetElementsHidden(IReadOnlyList<SceneElementRef> elements, bool hidden)
    {
        ArgumentNullException.ThrowIfNull(elements);
        var commands = new List<ISceneCommand>();
        foreach (var element in Distinct(elements))
        {
            if (element.IsNode && _scene.Find(element.AsNode) is { } node && node.Hidden != hidden)
            {
                commands.Add(new SetNodeHiddenCommand(node.Id, hidden));
            }
            else if (element.IsLight && _scene.FindLight(element.AsLight) is { } light && light.Hidden != hidden)
            {
                commands.Add(new SetLightCommand(light.WithHidden(hidden)));
            }
        }

        if (commands.Count == 0)
        {
            return false;
        }

        _commands.Execute(Unwrap(commands));
        MarkChanged();
        return true;
    }

    /// <summary>Clones every listed element as ONE undoable edit — each copy follows the
    /// <see cref="DuplicateNode"/> convention (name + " copy", one metre along X) — and the copies become
    /// the new selection, ready for a group drag. Refs not in the scene are skipped; false when nothing
    /// was cloned.</summary>
    public bool DuplicateElements(IReadOnlyList<SceneElementRef> elements)
    {
        ArgumentNullException.ThrowIfNull(elements);
        var commands = new List<ISceneCommand>();
        var copies = new List<SceneElementRef>();
        foreach (var element in Distinct(elements))
        {
            if (element.IsNode && _scene.Find(element.AsNode) is { } node)
            {
                var id = _scene.AllocateId();
                commands.Add(new PlaceNodeCommand(new SceneNode(
                    id, node.Name + DuplicateNameSuffix, node.AssetRef,
                    node.Transform with { Position = OffsetForCopy(node.Transform.Position) })));
                copies.Add(SceneElementRef.Node(id));
            }
            else if (element.IsLight && _scene.FindLight(element.AsLight) is { } light)
            {
                var id = _scene.AllocateLightId();
                commands.Add(new AddLightCommand(
                    light.WithId(id).WithName(light.Name + DuplicateNameSuffix) with
                    {
                        Position = OffsetForCopy(light.Position),
                    }));
                copies.Add(SceneElementRef.Light(id));
            }
        }

        if (commands.Count == 0)
        {
            return false;
        }

        _commands.Execute(Unwrap(commands));
        _selection.SetAll(copies);
        MarkChanged();
        return true;
    }

    /// <summary>Places copies of the given element values as ONE undoable edit — the paste half of group
    /// copy / paste. Every spec gets a freshly allocated id (the spec's own id is ignored) and pastes
    /// visible regardless of the source's hidden flag (the author pastes to see it); the copies become the
    /// new selection, ready for a group drag. False when both lists are empty.</summary>
    public bool PasteElements(IReadOnlyList<SceneNode> nodes, IReadOnlyList<SceneLight> lights)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(lights);
        var commands = new List<ISceneCommand>();
        var copies = new List<SceneElementRef>();
        foreach (var spec in nodes)
        {
            ArgumentException.ThrowIfNullOrEmpty(spec.Name);
            var id = _scene.AllocateId();
            commands.Add(new PlaceNodeCommand(new SceneNode(id, spec.Name, spec.AssetRef, spec.Transform)));
            copies.Add(SceneElementRef.Node(id));
        }

        foreach (var spec in lights)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(spec.Name);
            var id = _scene.AllocateLightId();
            commands.Add(new AddLightCommand(spec.WithId(id).WithHidden(false)));
            copies.Add(SceneElementRef.Light(id));
        }

        if (commands.Count == 0)
        {
            return false;
        }

        _commands.Execute(Unwrap(commands));
        _selection.SetAll(copies);
        MarkChanged();
        return true;
    }

    /// <summary>Commits a coalesced multi-selection drag as ONE reversible edit: each entry's undo
    /// restores its <c>From</c> and redo reapplies its <c>To</c>, regardless of how many preview frames
    /// the drag spanned. Entries that did not change or name a missing element are skipped; false when
    /// nothing changed. A single surviving entry executes as the plain command, so a single-selection
    /// drag's history is unchanged (the group twin of <see cref="CommitNodeTransform"/> /
    /// <see cref="CommitLight"/>).</summary>
    public bool CommitGroupTransform(IReadOnlyList<NodeMove> nodes, IReadOnlyList<LightMove> lights)
    {
        ArgumentNullException.ThrowIfNull(nodes);
        ArgumentNullException.ThrowIfNull(lights);
        var commands = new List<ISceneCommand>();
        foreach (var move in nodes)
        {
            if (move.From != move.To && _scene.Contains(move.Id))
            {
                commands.Add(new TransformNodeCommand(move.Id, move.From, move.To));
            }
        }

        foreach (var move in lights)
        {
            if (move.From != move.To && _scene.ContainsLight(move.To.Id))
            {
                commands.Add(new SetLightCommand(move.From, move.To));
            }
        }

        if (commands.Count == 0)
        {
            return false;
        }

        _commands.Execute(Unwrap(commands));
        MarkChanged();
        return true;
    }
}
