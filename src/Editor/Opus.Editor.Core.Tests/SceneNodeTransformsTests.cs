using System.Numerics;
using FluentAssertions;
using Xunit;

namespace Opus.Editor.Core.Tests;

public sealed class SceneNodeTransformsTests
{
    private static SceneNode At(int id, Float3 position, SceneNodeId? parent = null) =>
        new SceneNode(new SceneNodeId(id), "n" + id, null, EditorTransform.Identity with { Position = position })
        {
            ParentId = parent,
        };

    [Fact]
    public void A_root_world_matrix_is_its_local_matrix()
    {
        var nodes = new[] { At(1, new Float3(10f, 0f, 5f)) };

        var world = SceneNodeTransforms.WorldMatrix(nodes, new SceneNodeId(1));

        world.Translation.X.Should().BeApproximately(10f, 1e-4f);
        world.Translation.Z.Should().BeApproximately(5f, 1e-4f);
    }

    [Fact]
    public void A_child_world_position_adds_the_parent_translation()
    {
        var nodes = new[]
        {
            At(1, new Float3(10f, 0f, 0f)),
            At(2, new Float3(1f, 0f, 0f), new SceneNodeId(1)),
        };

        var world = SceneNodeTransforms.WorldMatrix(nodes, new SceneNodeId(2));

        world.Translation.X.Should().BeApproximately(11f, 1e-4f, "child local + parent world");
    }

    [Fact]
    public void A_grandchild_accumulates_the_whole_chain()
    {
        var nodes = new[]
        {
            At(1, new Float3(10f, 0f, 0f)),
            At(2, new Float3(1f, 0f, 0f), new SceneNodeId(1)),
            At(3, new Float3(0f, 0f, 2f), new SceneNodeId(2)),
        };

        var world = SceneNodeTransforms.WorldMatrix(nodes, new SceneNodeId(3));

        world.Translation.X.Should().BeApproximately(11f, 1e-4f);
        world.Translation.Z.Should().BeApproximately(2f, 1e-4f);
    }

    [Fact]
    public void A_parent_rotation_rotates_the_child_offset()
    {
        // Parent yaw +90deg (about Y) maps a child local +X offset to world about -Z (engine convention).
        var parent = new SceneNode(
            new SceneNodeId(1), "p", null, EditorTransform.Identity with { RotationEulerDegrees = new Float3(0f, 90f, 0f) });
        var child = At(2, new Float3(1f, 0f, 0f), new SceneNodeId(1));
        var nodes = new[] { parent, child };

        var world = SceneNodeTransforms.WorldMatrix(nodes, new SceneNodeId(2));

        // The offset magnitude survives; the rotation has moved it off the X axis.
        var t = world.Translation;
        new Vector3(t.X, t.Y, t.Z).Length().Should().BeApproximately(1f, 1e-3f);
        t.X.Should().BeApproximately(0f, 1e-3f, "the +X offset rotated away from X");
    }

    [Fact]
    public void A_missing_id_is_identity_and_a_dangling_parent_is_treated_as_a_root()
    {
        var nodes = new[] { At(1, new Float3(3f, 0f, 0f), new SceneNodeId(99)) };

        SceneNodeTransforms.WorldMatrix(nodes, new SceneNodeId(50)).Should().Be(Matrix4x4.Identity);
        SceneNodeTransforms.WorldMatrix(nodes, new SceneNodeId(1)).Translation.X
            .Should().BeApproximately(3f, 1e-4f, "a node with a missing parent behaves as a root");
    }

    [Fact]
    public void A_parent_cycle_terminates()
    {
        var nodes = new[]
        {
            At(1, new Float3(1f, 0f, 0f), new SceneNodeId(2)),
            At(2, new Float3(2f, 0f, 0f), new SceneNodeId(1)),
        };

        var world = SceneNodeTransforms.WorldMatrix(nodes, new SceneNodeId(1));

        float.IsFinite(world.Translation.X).Should().BeTrue("the walk must not loop forever");
    }
}
