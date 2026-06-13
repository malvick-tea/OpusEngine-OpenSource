using System;
using System.Collections.Generic;
using System.Numerics;

namespace Opus.Editor.Core;

/// <summary>The result of planning an ungroup: the ordered commands to execute as one undoable edit, and
/// the child nodes promoted out of the dissolved groups (the new selection). Every promoted node survives
/// the operation — a child that is itself a dissolved group is removed, never promoted.</summary>
/// <param name="Commands">The scene commands to execute, in order, as a single history entry.</param>
/// <param name="PromotedChildren">The children lifted out of the groups, to become the selection.</param>
public readonly record struct SceneUngroupPlan(
    IReadOnlyList<ISceneCommand> Commands,
    IReadOnlyList<SceneElementRef> PromotedChildren);

/// <summary>
/// Pure planning of the hierarchy grouping operations — group, world-preserving re-parent, and ungroup — as
/// ordered <see cref="ISceneCommand"/> lists the document executes atomically. All of the world-space matrix
/// math (centroid placement, re-expressing a world position in a parent's frame, surviving-ancestor
/// promotion) lives here, off <see cref="EditorDocument"/>, so it is unit-tested without a document and the
/// document stays a thin orchestrator. Engine-neutral and GPU-free; the scene is read, never mutated — the
/// document owns id allocation and command execution.
/// </summary>
public static class SceneGroupingPlanner
{
    /// <summary>The selected nodes that re-parent under a new group, in document-call order: a node whose
    /// ancestor is also selected stays under that ancestor (keeping an existing internal hierarchy), and a
    /// node not in the scene is dropped. Empty when no listed node is in the scene — the caller then groups
    /// nothing and allocates no id.</summary>
    public static IReadOnlyList<SceneNodeId> TopLevelToGroup(EditorScene scene, IReadOnlyList<SceneNodeId> nodeIds)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(nodeIds);
        var valid = new HashSet<SceneNodeId>();
        foreach (var id in nodeIds)
        {
            if (scene.Contains(id))
            {
                valid.Add(id);
            }
        }

        var topLevel = new List<SceneNodeId>();
        foreach (var id in nodeIds)
        {
            if (!valid.Contains(id) || topLevel.Contains(id))
            {
                continue;
            }

            // A node whose parent is also being grouped stays under that parent (keep the inner hierarchy).
            var node = scene.Find(id)!;
            if (node.ParentId is not { } parent || !valid.Contains(parent))
            {
                topLevel.Add(id);
            }
        }

        return topLevel;
    }

    /// <summary>The commands that create the new empty group node at the centroid of
    /// <paramref name="topLevel"/>'s world positions and re-parent each of those nodes under it, preserving
    /// their world positions. The caller has allocated <paramref name="groupId"/> and named it.</summary>
    public static IReadOnlyList<ISceneCommand> PlanGroup(
        EditorScene scene, IReadOnlyList<SceneNodeId> topLevel, SceneNodeId groupId, string groupName)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(topLevel);
        ArgumentException.ThrowIfNullOrEmpty(groupName);
        var centroid = Centroid(scene, topLevel);
        var commands = new List<ISceneCommand>
        {
            new PlaceNodeCommand(new SceneNode(
                groupId, groupName, null, EditorTransform.Identity with { Position = Float3.FromVector3(centroid) })),
        };

        var groupInverse = Matrix4x4.CreateTranslation(-centroid);
        foreach (var id in topLevel)
        {
            AppendReparent(scene, commands, scene.Find(id)!, groupId, groupInverse);
        }

        return commands;
    }

    /// <summary>The commands that re-parent every listed node onto <paramref name="parent"/> (null detaches
    /// them to roots), preserving each node's world position by re-expressing it in the parent's frame. Nodes
    /// that are missing, already on that parent, or whose move would cycle are skipped (duplicates dropped).
    /// Empty when nothing changes or the named parent is absent.</summary>
    public static IReadOnlyList<ISceneCommand> PlanReparentKeepingWorld(
        EditorScene scene, IReadOnlyList<SceneNodeId> children, SceneNodeId? parent)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(children);
        if (parent is { } parentId && !scene.Contains(parentId))
        {
            return Array.Empty<ISceneCommand>();
        }

        var parentInverse = InverseWorld(scene, parent);
        var commands = new List<ISceneCommand>();
        var seen = new HashSet<SceneNodeId>();
        foreach (var child in children)
        {
            if (!seen.Add(child) || scene.Find(child) is not { } node || node.ParentId == parent)
            {
                continue;
            }

            if (parent is { } target && (target == child || SceneHierarchy.WouldCreateCycle(scene.Nodes, child, target)))
            {
                continue;
            }

            AppendReparent(scene, commands, node, parent, parentInverse);
        }

        return commands;
    }

    /// <summary>Plans the ungroup of <paramref name="groupIds"/>: each group's children are promoted to the
    /// nearest ancestor that survives the operation (skipping over other groups being dissolved, falling back
    /// to a root), preserving world positions; an empty group node is then removed, while a node carrying an
    /// asset keeps its place and only releases its children. Missing or childless groups contribute nothing.
    /// The promoted children — never a child that is itself dissolved — become the selection.</summary>
    public static SceneUngroupPlan PlanUngroup(EditorScene scene, IReadOnlyList<SceneNodeId> groupIds)
    {
        ArgumentNullException.ThrowIfNull(scene);
        ArgumentNullException.ThrowIfNull(groupIds);
        var dissolving = GroupsToDissolve(scene, groupIds);
        var commands = new List<ISceneCommand>();
        var promoted = new List<SceneElementRef>();
        var seen = new HashSet<SceneNodeId>();
        foreach (var groupId in groupIds)
        {
            if (!seen.Add(groupId) || scene.Find(groupId) is not { } group)
            {
                continue;
            }

            var target = SurvivingAncestor(scene, group.ParentId, dissolving);
            var targetInverse = InverseWorld(scene, target);
            foreach (var child in SceneHierarchy.ChildrenOf(scene.Nodes, groupId))
            {
                if (dissolving.Contains(child.Id))
                {
                    continue;  // a nested dissolved group vanishes; it is neither re-parented nor promoted.
                }

                AppendReparent(scene, commands, child, target, targetInverse);
                promoted.Add(SceneElementRef.Node(child.Id));
            }

            if (dissolving.Contains(groupId))
            {
                commands.Add(new RemoveNodeCommand(groupId));
            }
        }

        return new SceneUngroupPlan(commands, promoted);
    }

    /// <summary>The deduped groups that the ungroup will remove: an empty (asset-less) node that has at least
    /// one child. A childless node is left in place (nothing to ungroup) and an asset-bearing node survives,
    /// so the surviving-ancestor walk treats both as valid promotion targets.</summary>
    private static HashSet<SceneNodeId> GroupsToDissolve(EditorScene scene, IReadOnlyList<SceneNodeId> groupIds)
    {
        var dissolving = new HashSet<SceneNodeId>();
        var seen = new HashSet<SceneNodeId>();
        foreach (var groupId in groupIds)
        {
            if (seen.Add(groupId) && scene.Find(groupId) is { AssetRef: null } && HasChildren(scene, groupId))
            {
                dissolving.Add(groupId);
            }
        }

        return dissolving;
    }

    /// <summary>True when at least one node names <paramref name="id"/> as its parent.</summary>
    private static bool HasChildren(EditorScene scene, SceneNodeId id)
    {
        foreach (var child in SceneHierarchy.ChildrenOf(scene.Nodes, id))
        {
            return true;
        }

        return false;
    }

    /// <summary>Walks up from <paramref name="start"/> past every ancestor being dissolved, returning the
    /// first ancestor that survives (or null for a root). Cycle-safe via a visited set, so a malformed scene
    /// file never loops. The promoted children land here so they never reference a node that is about to be
    /// removed.</summary>
    private static SceneNodeId? SurvivingAncestor(
        EditorScene scene, SceneNodeId? start, HashSet<SceneNodeId> dissolving)
    {
        var current = start;
        var visited = new HashSet<SceneNodeId>();
        while (current is { } id && dissolving.Contains(id) && visited.Add(id))
        {
            current = scene.Find(id)?.ParentId;
        }

        return current;
    }

    /// <summary>The average of the listed nodes' world positions — where a new group node sits.</summary>
    private static Vector3 Centroid(EditorScene scene, IReadOnlyList<SceneNodeId> nodeIds)
    {
        if (nodeIds.Count == 0)
        {
            return Vector3.Zero;
        }

        var sum = Vector3.Zero;
        foreach (var id in nodeIds)
        {
            sum += SceneNodeTransforms.WorldMatrix(scene, id).Translation;
        }

        return sum / nodeIds.Count;
    }

    /// <summary>The inverse world matrix of <paramref name="id"/>, or null for a root (no parent) or a
    /// non-invertible (degenerate-scale) ancestor — in which case the world position is kept as the local.</summary>
    private static Matrix4x4? InverseWorld(EditorScene scene, SceneNodeId? id) =>
        id is { } nodeId && Matrix4x4.Invert(SceneNodeTransforms.WorldMatrix(scene, nodeId), out var inverse)
            ? inverse
            : null;

    /// <summary>Appends the re-parent of <paramref name="node"/> onto <paramref name="target"/> preserving its
    /// world position: the new local position is the node's world position re-expressed in the target's frame
    /// (or the world position itself when the target is a root). Local rotation and scale are kept, so under an
    /// unrotated, unit-scaled target the node stays exactly in place.</summary>
    private static void AppendReparent(
        EditorScene scene,
        List<ISceneCommand> commands,
        SceneNode node,
        SceneNodeId? target,
        Matrix4x4? targetInverse)
    {
        var world = SceneNodeTransforms.WorldMatrix(scene, node.Id).Translation;
        var local = targetInverse is { } inverse ? Vector3.Transform(world, inverse) : world;
        commands.Add(new SetNodeParentCommand(node.Id, target));
        commands.Add(new TransformNodeCommand(node.Id, node.Transform with { Position = Float3.FromVector3(local) }));
    }
}
