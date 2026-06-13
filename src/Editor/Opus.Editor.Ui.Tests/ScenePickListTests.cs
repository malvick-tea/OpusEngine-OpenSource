using System.Numerics;
using FluentAssertions;
using Opus.Editor.Core;
using Opus.Foundation.Geometry;
using Xunit;

namespace Opus.Editor.Ui.Tests;

public sealed class ScenePickListTests
{
    private static readonly Aabb UnitBox = new(new Vector3(-1f), new Vector3(1f));

    [Fact]
    public void Builds_one_candidate_per_node()
    {
        var scene = new EditorScene();
        scene.Add(new SceneNode(scene.AllocateId(), "a", "m.glb", EditorTransform.Identity));
        scene.Add(new SceneNode(scene.AllocateId(), "b", null, EditorTransform.Identity));

        var list = ScenePickList.Build(scene, new FixedBounds(UnitBox));

        list.Should().HaveCount(2);
    }

    [Fact]
    public void Transforms_local_bounds_into_world_by_node_position()
    {
        var scene = new EditorScene();
        scene.Add(new SceneNode(
            scene.AllocateId(), "a", "m.glb", new EditorTransform(new Float3(10f, 0f, 0f), Float3.Zero, Float3.One)));

        var list = ScenePickList.Build(scene, new FixedBounds(UnitBox));

        list[0].WorldBounds.Centre.X.Should().BeApproximately(10f, 1e-4f);
    }

    [Fact]
    public void Uses_a_fallback_box_when_bounds_are_unknown()
    {
        var scene = new EditorScene();
        scene.Add(new SceneNode(scene.AllocateId(), "a", null, EditorTransform.Identity));

        var list = ScenePickList.Build(scene, new NullBounds());

        list.Should().ContainSingle();
        list[0].WorldBounds.IsEmpty.Should().BeFalse();
    }

    [Fact]
    public void A_primitive_node_resolves_its_shape_bounds_without_any_bounds_source()
    {
        var scene = new EditorScene();
        scene.Add(new SceneNode(
            scene.AllocateId(), "ground", ScenePrimitive.AssetRef(ScenePrimitiveKind.Plane), EditorTransform.Identity));

        var list = ScenePickList.Build(scene, new NullBounds());

        list[0].WorldBounds.Max.Y.Should().BeApproximately(
            PrimitiveWire.PlanePickHalfThickness, 1e-5f, "the plane's thin slab, not the 0.5 m fallback box");
    }

    [Fact]
    public void Primitive_bounds_scale_with_the_node_transform()
    {
        var scene = new EditorScene();
        scene.Add(new SceneNode(
            scene.AllocateId(),
            "box",
            ScenePrimitive.AssetRef(ScenePrimitiveKind.Cube),
            new EditorTransform(Float3.Zero, Float3.Zero, new Float3(4f, 1f, 1f))));

        var list = ScenePickList.Build(scene, new NullBounds());

        list[0].WorldBounds.Max.X.Should().BeApproximately(2f, 1e-4f);
    }

    [Fact]
    public void Hidden_elements_are_not_pick_candidates()
    {
        var scene = new EditorScene();
        scene.Add(new SceneNode(scene.AllocateId(), "shown", null, EditorTransform.Identity));
        scene.Add(new SceneNode(scene.AllocateId(), "hidden", null, EditorTransform.Identity).WithHidden(true));
        scene.AddLight(SceneLight.CreatePoint("torch").WithId(scene.AllocateLightId()).WithHidden(true));

        var nodes = ScenePickList.Build(scene, new NullBounds());
        var elements = ScenePickList.BuildElements(scene, new NullBounds());

        nodes.Should().ContainSingle("what is not drawn must not be clickable");
        elements.Should().ContainSingle();
        elements[0].Element.Should().Be(SceneElementRef.Node(scene.Nodes[0].Id));
    }

    [Fact]
    public void A_child_pick_box_follows_its_parent_in_world_space()
    {
        var scene = new EditorScene();
        var parent = scene.AllocateId();
        scene.Add(new SceneNode(parent, "group", null, EditorTransform.Identity with { Position = new Float3(10f, 0f, 0f) }));
        var child = scene.AllocateId();
        scene.Add(new SceneNode(child, "box", "m.glb", EditorTransform.Identity with { Position = new Float3(1f, 0f, 0f) })
        {
            ParentId = parent,
        });

        var list = ScenePickList.Build(scene, new FixedBounds(UnitBox));
        var childBounds = list[1].WorldBounds;

        childBounds.Centre.X.Should().BeApproximately(11f, 1e-4f, "the child's box is composed through its parent");
    }
}
