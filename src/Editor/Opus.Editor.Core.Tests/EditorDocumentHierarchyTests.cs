using FluentAssertions;
using Xunit;

namespace Opus.Editor.Core.Tests;

public sealed class EditorDocumentHierarchyTests
{
    [Fact]
    public void Set_parent_links_the_child_and_is_one_undoable_edit()
    {
        var document = new EditorDocument("scene");
        var parent = document.PlaceNode("group", null, EditorTransform.Identity);
        var child = document.PlaceNode("box", null, EditorTransform.Identity);

        document.SetNodeParent(child, parent).Should().BeTrue();
        document.Scene.Find(child)!.ParentId.Should().Be(parent);
        document.IsDirty.Should().BeTrue();

        document.Undo().Should().BeTrue();
        document.Scene.Find(child)!.ParentId.Should().BeNull("undo restores the previous parent");
    }

    [Fact]
    public void Set_parent_to_null_detaches_a_child_to_a_root()
    {
        var document = new EditorDocument("scene");
        var parent = document.PlaceNode("group", null, EditorTransform.Identity);
        var child = document.PlaceNode("box", null, EditorTransform.Identity);
        document.SetNodeParent(child, parent);

        document.SetNodeParent(child, null).Should().BeTrue();

        document.Scene.Find(child)!.ParentId.Should().BeNull();
    }

    [Fact]
    public void Set_parent_rejects_missing_child_missing_parent_self_and_unchanged()
    {
        var document = new EditorDocument("scene");
        var a = document.PlaceNode("a", null, EditorTransform.Identity);

        document.SetNodeParent(new SceneNodeId(99), a).Should().BeFalse("child is missing");
        document.SetNodeParent(a, new SceneNodeId(99)).Should().BeFalse("parent is missing");
        document.SetNodeParent(a, a).Should().BeFalse("a node cannot parent onto itself");
        document.SetNodeParent(a, null).Should().BeFalse("already a root — nothing changes");
    }

    [Fact]
    public void Set_parent_rejects_a_move_that_would_form_a_cycle()
    {
        var document = new EditorDocument("scene");
        var a = document.PlaceNode("a", null, EditorTransform.Identity);
        var b = document.PlaceNode("b", null, EditorTransform.Identity);
        document.SetNodeParent(b, a);

        // a is b's parent; parenting a onto b would loop a -> b -> a.
        document.SetNodeParent(a, b).Should().BeFalse();
        document.Scene.Find(a)!.ParentId.Should().BeNull();
    }

    [Fact]
    public void Removing_a_parent_takes_its_whole_subtree_as_one_undo_step()
    {
        var document = new EditorDocument("scene");
        var root = document.PlaceNode("root", null, EditorTransform.Identity);
        var child = document.PlaceNode("child", null, EditorTransform.Identity);
        var grandchild = document.PlaceNode("grandchild", null, EditorTransform.Identity);
        document.SetNodeParent(child, root);
        document.SetNodeParent(grandchild, child);
        document.Scene.Count.Should().Be(3);

        document.RemoveNode(root).Should().BeTrue();
        document.Scene.Count.Should().Be(0, "the parent removal cascades to its descendants");

        document.Undo().Should().BeTrue();
        document.Scene.Count.Should().Be(3, "one undo restores the whole subtree");
        document.Scene.Find(grandchild)!.ParentId.Should().Be(child, "restored nodes keep their parent links");
    }

    [Fact]
    public void A_childless_node_still_removes_as_a_plain_single_edit()
    {
        var document = new EditorDocument("scene");
        var a = document.PlaceNode("a", null, EditorTransform.Identity);
        var b = document.PlaceNode("b", null, EditorTransform.Identity);

        document.RemoveNode(b).Should().BeTrue();
        document.Scene.Count.Should().Be(1);

        document.Undo().Should().BeTrue();
        document.Scene.Count.Should().Be(2, "only b came back");
        document.Scene.Find(a).Should().NotBeNull();
    }

    [Fact]
    public void Group_remove_of_a_parent_and_its_child_removes_the_subtree_once_without_throwing()
    {
        var document = new EditorDocument("scene");
        var parent = document.PlaceNode("parent", null, EditorTransform.Identity);
        var child = document.PlaceNode("child", null, EditorTransform.Identity);
        document.SetNodeParent(child, parent);

        var removed = document.RemoveElements(new[]
        {
            SceneElementRef.Node(parent),
            SceneElementRef.Node(child),
        });

        removed.Should().BeTrue();
        document.Scene.Count.Should().Be(0);
        document.Undo().Should().BeTrue();
        document.Scene.Count.Should().Be(2, "the deduped subtree restores in one step");
    }

    [Fact]
    public void Reparent_keeping_world_preserves_the_child_world_position()
    {
        var document = new EditorDocument("scene");
        var parent = document.PlaceNode(
            "group", null, EditorTransform.Identity with { Position = new Float3(5f, 0f, 0f) });
        var child = document.PlaceNode(
            "box", null, EditorTransform.Identity with { Position = new Float3(2f, 0f, 0f) });

        document.ReparentNodesKeepingWorld(new[] { child }, parent).Should().BeTrue();

        var childNode = document.Scene.Find(child)!;
        childNode.ParentId.Should().Be(parent);
        childNode.Transform.Position.X.Should().BeApproximately(-3f, 1e-4f, "local = world minus the parent offset");
        SceneNodeTransforms.WorldMatrix(document.Scene, child).Translation.X
            .Should().BeApproximately(2f, 1e-4f, "the world position is unchanged — the node does not jump");
    }

    [Fact]
    public void Reparent_keeping_world_is_one_undo_step()
    {
        var document = new EditorDocument("scene");
        var parent = document.PlaceNode(
            "group", null, EditorTransform.Identity with { Position = new Float3(5f, 0f, 0f) });
        var child = document.PlaceNode(
            "box", null, EditorTransform.Identity with { Position = new Float3(2f, 0f, 0f) });
        document.ReparentNodesKeepingWorld(new[] { child }, parent);

        document.Undo().Should().BeTrue();

        var childNode = document.Scene.Find(child)!;
        childNode.ParentId.Should().BeNull("the parent link and the position restore together");
        childNode.Transform.Position.X.Should().BeApproximately(2f, 1e-4f);
    }

    [Fact]
    public void Reparent_keeping_world_skips_a_cycle_and_a_missing_parent()
    {
        var document = new EditorDocument("scene");
        var a = document.PlaceNode("a", null, EditorTransform.Identity);
        var b = document.PlaceNode("b", null, EditorTransform.Identity);
        document.SetNodeParent(b, a);

        document.ReparentNodesKeepingWorld(new[] { a }, b).Should().BeFalse("a -> b -> a would cycle");
        document.ReparentNodesKeepingWorld(new[] { a }, new SceneNodeId(99)).Should().BeFalse("missing parent");
        document.Scene.Find(a)!.ParentId.Should().BeNull();
    }

    [Fact]
    public void Reparent_keeping_world_detaches_to_a_root_at_the_world_position()
    {
        var document = new EditorDocument("scene");
        var parent = document.PlaceNode(
            "group", null, EditorTransform.Identity with { Position = new Float3(5f, 0f, 0f) });
        var child = document.PlaceNode(
            "box", null, EditorTransform.Identity with { Position = new Float3(2f, 0f, 0f) });
        document.ReparentNodesKeepingWorld(new[] { child }, parent);

        document.ReparentNodesKeepingWorld(new[] { child }, null).Should().BeTrue();

        var childNode = document.Scene.Find(child)!;
        childNode.ParentId.Should().BeNull();
        childNode.Transform.Position.X.Should().BeApproximately(2f, 1e-4f, "a detached node's local is its world");
    }

    [Fact]
    public void Group_creates_a_parent_at_the_centroid_and_preserves_world_positions()
    {
        var document = new EditorDocument("scene");
        var a = document.PlaceNode("a", null, EditorTransform.Identity);
        var b = document.PlaceNode("b", null, EditorTransform.Identity with { Position = new Float3(4f, 0f, 0f) });

        var group = document.GroupNodes(new[] { a, b });

        group.IsValid.Should().BeTrue();
        document.SelectedElement.Should().Be(SceneElementRef.Node(group));
        var groupNode = document.Scene.Find(group)!;
        groupNode.AssetRef.Should().BeNull("a group is an empty parent node");
        groupNode.Transform.Position.X.Should().BeApproximately(2f, 1e-4f, "the group sits at the centroid");
        document.Scene.Find(a)!.ParentId.Should().Be(group);
        document.Scene.Find(b)!.ParentId.Should().Be(group);
        SceneNodeTransforms.WorldMatrix(document.Scene, a).Translation.X.Should().BeApproximately(0f, 1e-4f);
        SceneNodeTransforms.WorldMatrix(document.Scene, b).Translation.X.Should().BeApproximately(4f, 1e-4f);
    }

    [Fact]
    public void Group_is_one_undo_step()
    {
        var document = new EditorDocument("scene");
        var a = document.PlaceNode("a", null, EditorTransform.Identity);
        var b = document.PlaceNode("b", null, EditorTransform.Identity with { Position = new Float3(4f, 0f, 0f) });
        document.GroupNodes(new[] { a, b });
        document.Scene.Count.Should().Be(3);

        document.Undo().Should().BeTrue();

        document.Scene.Count.Should().Be(2, "the group node and both reparents undo together");
        document.Scene.Find(a)!.ParentId.Should().BeNull();
    }

    [Fact]
    public void Group_keeps_an_existing_internal_hierarchy()
    {
        var document = new EditorDocument("scene");
        var parent = document.PlaceNode("parent", null, EditorTransform.Identity);
        var child = document.PlaceNode("child", null, EditorTransform.Identity);
        document.SetNodeParent(child, parent);

        var group = document.GroupNodes(new[] { parent, child });

        document.Scene.Find(parent)!.ParentId.Should().Be(group, "the top-level node parents under the group");
        document.Scene.Find(child)!.ParentId.Should().Be(parent, "the child stays under its selected ancestor");
    }

    [Fact]
    public void Group_of_no_nodes_is_none()
    {
        var document = new EditorDocument("scene");

        document.GroupNodes(System.Array.Empty<SceneNodeId>()).Should().Be(SceneNodeId.None);
    }

    [Fact]
    public void Ungroup_promotes_children_to_the_grandparent_and_preserves_world()
    {
        var document = new EditorDocument("scene");
        var grandparent = document.PlaceNode(
            "grandparent", null, EditorTransform.Identity with { Position = new Float3(10f, 0f, 0f) });
        var group = document.PlaceNode(
            "group", null, EditorTransform.Identity with { Position = new Float3(3f, 0f, 0f) });
        var child = document.PlaceNode(
            "child", null, EditorTransform.Identity with { Position = new Float3(2f, 0f, 0f) });
        document.SetNodeParent(group, grandparent);
        document.SetNodeParent(child, group);

        document.UngroupNodes(new[] { group }).Should().BeTrue();

        document.Scene.Find(group).Should().BeNull("the empty group node is removed");
        var childNode = document.Scene.Find(child)!;
        childNode.ParentId.Should().Be(grandparent, "the child rises to the grandparent");
        SceneNodeTransforms.WorldMatrix(document.Scene, child).Translation.X
            .Should().BeApproximately(15f, 1e-4f, "the world position is unchanged — the child does not jump");
    }

    [Fact]
    public void Ungroup_promotes_children_to_a_root_when_the_group_has_no_parent()
    {
        var document = new EditorDocument("scene");
        var group = document.PlaceNode(
            "group", null, EditorTransform.Identity with { Position = new Float3(4f, 0f, 0f) });
        var child = document.PlaceNode(
            "child", null, EditorTransform.Identity with { Position = new Float3(2f, 0f, 0f) });
        document.SetNodeParent(child, group);

        document.UngroupNodes(new[] { group }).Should().BeTrue();

        var childNode = document.Scene.Find(child)!;
        childNode.ParentId.Should().BeNull("a child of a root group becomes a root");
        childNode.Transform.Position.X.Should().BeApproximately(6f, 1e-4f, "a root child's local is its world");
        document.Scene.Count.Should().Be(1, "only the promoted child remains");
    }

    [Fact]
    public void Ungroup_of_an_asset_node_keeps_the_node_and_releases_its_children()
    {
        var document = new EditorDocument("scene");
        var carrier = document.PlaceNode(
            "tank", "models/tank.glb", EditorTransform.Identity with { Position = new Float3(5f, 0f, 0f) });
        var child = document.PlaceNode(
            "turret", null, EditorTransform.Identity with { Position = new Float3(2f, 0f, 0f) });
        document.SetNodeParent(child, carrier);

        document.UngroupNodes(new[] { carrier }).Should().BeTrue();

        document.Scene.Find(carrier).Should().NotBeNull("a node carrying an asset is never removed");
        document.Scene.Find(child)!.ParentId.Should().BeNull("it only releases its children to its own parent");
        document.Scene.Find(child)!.Transform.Position.X.Should().BeApproximately(7f, 1e-4f);
    }

    [Fact]
    public void Ungroup_is_one_undoable_edit()
    {
        var document = new EditorDocument("scene");
        var group = document.PlaceNode(
            "group", null, EditorTransform.Identity with { Position = new Float3(4f, 0f, 0f) });
        var child = document.PlaceNode(
            "child", null, EditorTransform.Identity with { Position = new Float3(2f, 0f, 0f) });
        document.SetNodeParent(child, group);
        document.UngroupNodes(new[] { group });
        document.Scene.Count.Should().Be(1);

        document.Undo().Should().BeTrue();

        document.Scene.Count.Should().Be(2, "the promotions and the group removal undo together");
        var childNode = document.Scene.Find(child)!;
        childNode.ParentId.Should().Be(group, "the parent link and the local position restore together");
        childNode.Transform.Position.X.Should().BeApproximately(2f, 1e-4f);
    }

    [Fact]
    public void Ungroup_skips_a_childless_node_and_selects_the_promoted_children()
    {
        var document = new EditorDocument("scene");
        var empty = document.PlaceNode("empty", null, EditorTransform.Identity);
        document.UngroupNodes(new[] { empty }).Should().BeFalse("a childless node has nothing to ungroup");

        var group = document.PlaceNode("group", null, EditorTransform.Identity);
        var a = document.PlaceNode("a", null, EditorTransform.Identity);
        var b = document.PlaceNode("b", null, EditorTransform.Identity with { Position = new Float3(4f, 0f, 0f) });
        document.SetNodeParent(a, group);
        document.SetNodeParent(b, group);

        document.UngroupNodes(new[] { group }).Should().BeTrue();

        document.SelectedElements.Should().BeEquivalentTo(
            new[] { SceneElementRef.Node(a), SceneElementRef.Node(b) },
            "the promoted children become the selection");
    }

    [Fact]
    public void Ungroup_of_nested_groups_promotes_to_a_surviving_ancestor_without_jumps()
    {
        var document = new EditorDocument("scene");
        var outer = document.PlaceNode(
            "outer", null, EditorTransform.Identity with { Position = new Float3(10f, 0f, 0f) });
        var inner = document.PlaceNode(
            "inner", null, EditorTransform.Identity with { Position = new Float3(5f, 0f, 0f) });
        var leaf = document.PlaceNode(
            "leaf", null, EditorTransform.Identity with { Position = new Float3(2f, 0f, 0f) });
        document.SetNodeParent(inner, outer);
        document.SetNodeParent(leaf, inner);

        document.UngroupNodes(new[] { outer, inner }).Should().BeTrue();

        document.Scene.Find(outer).Should().BeNull();
        document.Scene.Find(inner).Should().BeNull();
        var leafNode = document.Scene.Find(leaf)!;
        leafNode.ParentId.Should().BeNull("the leaf lands on the nearest surviving ancestor — the root");
        SceneNodeTransforms.WorldMatrix(document.Scene, leaf).Translation.X
            .Should().BeApproximately(17f, 1e-4f, "the leaf keeps its world position through the dissolved chain");
        document.SelectedElements.Should().ContainSingle().Which.Should().Be(SceneElementRef.Node(leaf));

        document.Undo().Should().BeTrue();
        document.Scene.Count.Should().Be(3, "the whole nested ungroup undoes in one step");
        document.Scene.Find(leaf)!.ParentId.Should().Be(inner);
        document.Scene.Find(inner)!.ParentId.Should().Be(outer);
    }
}
