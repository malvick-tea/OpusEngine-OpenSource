using System;
using System.Collections.Generic;
using System.Globalization;

namespace Opus.Editor.Core;

/// <summary>Node-hierarchy operations of the <see cref="EditorDocument"/> aggregate: re-parent one node,
/// re-parent many keeping world positions, group, and ungroup. The world-space matrix math lives in the pure
/// <see cref="SceneGroupingPlanner"/>; these methods own only id allocation, command execution, and
/// selection — the document's transactional spine.</summary>
public sealed partial class EditorDocument
{
    /// <summary>Re-parents <paramref name="child"/> onto <paramref name="parent"/> (null detaches it to a
    /// root) as one undoable edit. The child keeps its list position and local transform; only the parent
    /// link changes. False — and no edit — when the child is absent, the named parent is absent, the move
    /// would form a cycle (parenting a node onto itself or onto one of its own descendants), or the parent
    /// is already what it would be set to.</summary>
    public bool SetNodeParent(SceneNodeId child, SceneNodeId? parent)
    {
        var node = _scene.Find(child);
        if (node is null)
        {
            return false;
        }

        if (parent is { } parentId
            && (!_scene.Contains(parentId) || SceneHierarchy.WouldCreateCycle(_scene.Nodes, child, parentId)))
        {
            return false;
        }

        if (node.ParentId == parent)
        {
            return false;
        }

        _commands.Execute(new SetNodeParentCommand(child, parent));
        MarkChanged();
        return true;
    }

    /// <summary>Re-parents every listed node onto <paramref name="parent"/> (null detaches them to roots) as
    /// ONE undoable edit, preserving each node's world position: the new local position is the old world
    /// position expressed in the parent's frame, so a node never jumps when it is grouped. Local rotation and
    /// scale are kept, so under an unrotated, unit-scaled parent (the common grouping case) the node stays
    /// exactly in place. Nodes that are missing, already on that parent, or whose move would form a cycle are
    /// skipped (and duplicates dropped); false when nothing changed or the named parent is absent.</summary>
    public bool ReparentNodesKeepingWorld(IReadOnlyList<SceneNodeId> children, SceneNodeId? parent)
    {
        ArgumentNullException.ThrowIfNull(children);
        var commands = SceneGroupingPlanner.PlanReparentKeepingWorld(_scene, children, parent);
        if (commands.Count == 0)
        {
            return false;
        }

        _commands.Execute(Unwrap(commands));
        MarkChanged();
        return true;
    }

    /// <summary>Groups the given nodes under a new empty parent at their centroid as ONE undoable edit, the
    /// canonical "group selection" (Ctrl+G): a fresh "group N" node is placed at the average world position
    /// of the selection's top-level nodes, and each of those nodes is re-parented under it preserving its
    /// world position. Only the top-level selected nodes re-parent — a selected node whose ancestor is also
    /// selected stays under that ancestor, so an existing internal hierarchy is kept intact. The new group
    /// becomes the selection. Returns the group's id, or <see cref="SceneNodeId.None"/> when no listed node
    /// is in the scene.</summary>
    public SceneNodeId GroupNodes(IReadOnlyList<SceneNodeId> nodeIds)
    {
        ArgumentNullException.ThrowIfNull(nodeIds);
        var topLevel = SceneGroupingPlanner.TopLevelToGroup(_scene, nodeIds);
        if (topLevel.Count == 0)
        {
            return SceneNodeId.None;
        }

        var groupId = _scene.AllocateId();
        string name = string.Create(CultureInfo.InvariantCulture, $"group {groupId.Value}");
        _commands.Execute(Unwrap(SceneGroupingPlanner.PlanGroup(_scene, topLevel, groupId, name)));
        _selection.SetNode(groupId);
        MarkChanged();
        return groupId;
    }

    /// <summary>Ungroups the given nodes as ONE undoable edit (Ctrl+Shift+G): each group's children are
    /// promoted to the nearest surviving ancestor (the group's parent, or a root — skipping over other groups
    /// being dissolved in the same edit) preserving world positions, and an empty container node is then
    /// removed. A node carrying an asset keeps its place, only shedding its children. Missing or childless
    /// groups are skipped, and the promoted children become the selection (a child that is itself dissolved is
    /// removed, not selected). False when nothing was ungrouped. See
    /// <see cref="SceneGroupingPlanner.PlanUngroup"/> for the world-space math.</summary>
    public bool UngroupNodes(IReadOnlyList<SceneNodeId> groupIds)
    {
        ArgumentNullException.ThrowIfNull(groupIds);
        var plan = SceneGroupingPlanner.PlanUngroup(_scene, groupIds);
        if (plan.Commands.Count == 0)
        {
            return false;
        }

        _commands.Execute(Unwrap(plan.Commands));
        _selection.SetAll(plan.PromotedChildren);
        MarkChanged();
        return true;
    }
}
