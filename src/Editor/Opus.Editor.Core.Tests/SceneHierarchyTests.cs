using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Xunit;

namespace Opus.Editor.Core.Tests;

public sealed class SceneHierarchyTests
{
    private static SceneNode Node(int id, SceneNodeId? parent = null) =>
        new SceneNode(new SceneNodeId(id), "n" + id, null, EditorTransform.Identity) { ParentId = parent };

    [Fact]
    public void Every_node_of_a_flat_scene_is_a_root()
    {
        var nodes = new[] { Node(1), Node(2), Node(3) };

        nodes.All(n => SceneHierarchy.IsRoot(nodes, n)).Should().BeTrue();
    }

    [Fact]
    public void A_parented_node_is_not_a_root_but_a_dangling_parent_is_treated_as_a_root()
    {
        var nodes = new[] { Node(1), Node(2, new SceneNodeId(1)), Node(3, new SceneNodeId(99)) };

        SceneHierarchy.IsRoot(nodes, nodes[0]).Should().BeTrue();
        SceneHierarchy.IsRoot(nodes, nodes[1]).Should().BeFalse();
        SceneHierarchy.IsRoot(nodes, nodes[2]).Should().BeTrue("a parent id naming a missing node is dangling");
    }

    [Fact]
    public void Children_are_listed_in_document_order()
    {
        var nodes = new[]
        {
            Node(1),
            Node(2, new SceneNodeId(1)),
            Node(3),
            Node(4, new SceneNodeId(1)),
        };

        SceneHierarchy.ChildrenOf(nodes, new SceneNodeId(1)).Select(n => n.Id.Value)
            .Should().Equal(2, 4);
        SceneHierarchy.ChildrenOf(nodes, new SceneNodeId(3)).Should().BeEmpty();
    }

    [Fact]
    public void Descendants_are_the_whole_subtree_in_pre_order_excluding_the_root()
    {
        // 1 -> {2 -> {4}, 3}
        var nodes = new[]
        {
            Node(1),
            Node(2, new SceneNodeId(1)),
            Node(3, new SceneNodeId(1)),
            Node(4, new SceneNodeId(2)),
        };

        SceneHierarchy.DescendantsOf(nodes, new SceneNodeId(1)).Select(i => i.Value)
            .Should().Equal(2, 4, 3);
        SceneHierarchy.DescendantsOf(nodes, new SceneNodeId(4)).Should().BeEmpty();
    }

    [Fact]
    public void Descendants_of_a_cyclic_scene_terminate_and_visit_each_node_once()
    {
        // A malformed file: 1 -> 2 -> 1 (a cycle). The walk must not loop forever.
        var nodes = new[]
        {
            Node(1, new SceneNodeId(2)),
            Node(2, new SceneNodeId(1)),
        };

        var descendants = SceneHierarchy.DescendantsOf(nodes, new SceneNodeId(1)).Select(i => i.Value).ToList();

        descendants.Should().OnlyHaveUniqueItems();
        descendants.Should().Contain(2);
    }

    [Fact]
    public void Self_parenting_and_descendant_parenting_are_cycles()
    {
        // 1 -> 2 -> 3
        var nodes = new[]
        {
            Node(1),
            Node(2, new SceneNodeId(1)),
            Node(3, new SceneNodeId(2)),
        };

        SceneHierarchy.WouldCreateCycle(nodes, new SceneNodeId(1), new SceneNodeId(1)).Should().BeTrue("self");
        SceneHierarchy.WouldCreateCycle(nodes, new SceneNodeId(1), new SceneNodeId(3)).Should().BeTrue("descendant");
        SceneHierarchy.WouldCreateCycle(nodes, new SceneNodeId(3), new SceneNodeId(1)).Should().BeFalse("ancestor is fine");
    }

    [Fact]
    public void Depth_counts_the_ancestor_chain_and_is_cycle_and_dangling_safe()
    {
        var nodes = new[]
        {
            Node(1),
            Node(2, new SceneNodeId(1)),
            Node(3, new SceneNodeId(2)),
            Node(4, new SceneNodeId(50)),
        };

        SceneHierarchy.Depth(nodes, new SceneNodeId(1)).Should().Be(0);
        SceneHierarchy.Depth(nodes, new SceneNodeId(2)).Should().Be(1);
        SceneHierarchy.Depth(nodes, new SceneNodeId(3)).Should().Be(2);
        SceneHierarchy.Depth(nodes, new SceneNodeId(4)).Should().Be(0, "a dangling parent ends the chain");
        SceneHierarchy.Depth(nodes, new SceneNodeId(99)).Should().Be(0, "a missing node has no chain");

        var cyclic = new[] { Node(1, new SceneNodeId(2)), Node(2, new SceneNodeId(1)) };
        SceneHierarchy.Depth(cyclic, new SceneNodeId(1)).Should().BeGreaterThanOrEqualTo(0, "the walk terminates");
    }
}
