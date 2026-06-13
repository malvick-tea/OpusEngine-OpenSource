using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using FluentAssertions;
using Opus.Editor.Core;
using Opus.Foundation.Geometry;
using Xunit;

namespace Opus.Editor.Ui.Tests;

public sealed class ViewportSceneDrawListTests
{
    private static readonly Aabb UnitBox = new(new Vector3(-1f), new Vector3(1f));

    [Fact]
    public void Grid_has_two_lines_per_division()
    {
        var lines = ViewportSceneDrawList.Build(
            new EditorScene(), new NullBounds(), SceneElementRef.None, gridHalfCount: 2);

        int expectedGridLines = ((2 * 2) + 1) * 2;
        lines.Count(l => l.Role is ViewportLineRole.Grid or ViewportLineRole.GridAxis).Should().Be(expectedGridLines);
    }

    [Fact]
    public void Each_node_adds_a_twelve_edge_wire_box()
    {
        var scene = new EditorScene();
        scene.Add(new SceneNode(scene.AllocateId(), "a", "m.glb", EditorTransform.Identity));

        var lines = ViewportSceneDrawList.Build(scene, new FixedBounds(UnitBox), SceneElementRef.None, gridHalfCount: 0);

        lines.Count(l => l.Role == ViewportLineRole.NodeBounds).Should().Be(12);
    }

    [Fact]
    public void The_selected_node_box_uses_the_selection_role()
    {
        var scene = new EditorScene();
        var id = scene.AllocateId();
        scene.Add(new SceneNode(id, "a", "m.glb", EditorTransform.Identity));

        var lines = ViewportSceneDrawList.Build(
            scene, new FixedBounds(UnitBox), SceneElementRef.Node(id), gridHalfCount: 0);

        lines.Count(l => l.Role == ViewportLineRole.Selection).Should().Be(12);
        lines.Should().NotContain(l => l.Role == ViewportLineRole.NodeBounds);
    }

    [Fact]
    public void Every_selection_set_member_uses_the_selection_role()
    {
        var scene = new EditorScene();
        var first = scene.AllocateId();
        scene.Add(new SceneNode(first, "a", "m.glb", EditorTransform.Identity));
        var second = scene.AllocateId();
        scene.Add(new SceneNode(second, "b", "m.glb", EditorTransform.Identity with { Position = new Float3(4f, 0f, 0f) }));

        var lines = ViewportSceneDrawList.Build(
            scene,
            new FixedBounds(UnitBox),
            new[] { SceneElementRef.Node(first), SceneElementRef.Node(second) },
            gridHalfCount: 0);

        lines.Count(l => l.Role == ViewportLineRole.Selection).Should().Be(24, "both set members highlight");
        lines.Should().NotContain(l => l.Role == ViewportLineRole.NodeBounds);
    }

    [Fact]
    public void The_selected_light_glyph_uses_the_selection_role()
    {
        var scene = new EditorScene();
        var id = scene.AllocateLightId();
        scene.AddLight(SceneLight.CreatePoint("lamp").WithId(id));

        var lines = ViewportSceneDrawList.Build(
            scene, new NullBounds(), SceneElementRef.Light(id), gridHalfCount: 0);

        lines.Count(l => l.Role == ViewportLineRole.Selection).Should().Be(
            3 + LightGizmo.RangeRingSegments, "the star arms and the range ring are promoted");
        lines.Should().NotContain(l => l.Role == ViewportLineRole.Light);
    }

    [Fact]
    public void A_primitive_node_draws_its_true_shape_instead_of_a_bounds_box()
    {
        var scene = new EditorScene();
        scene.Add(new SceneNode(
            scene.AllocateId(), "ball", ScenePrimitive.AssetRef(ScenePrimitiveKind.Sphere), EditorTransform.Identity));

        var lines = ViewportSceneDrawList.Build(scene, new NullBounds(), SceneElementRef.None, gridHalfCount: 0);

        lines.Count(l => l.Role == ViewportLineRole.NodeBounds)
            .Should().Be(3 * PrimitiveWire.CircleSegments, "three great circles, not a 12-edge box");
    }

    [Fact]
    public void A_selected_primitive_promotes_its_shape_to_the_selection_role()
    {
        var scene = new EditorScene();
        var id = scene.AllocateId();
        scene.Add(new SceneNode(id, "box", ScenePrimitive.AssetRef(ScenePrimitiveKind.Cube), EditorTransform.Identity));

        var lines = ViewportSceneDrawList.Build(scene, new NullBounds(), SceneElementRef.Node(id), gridHalfCount: 0);

        lines.Count(l => l.Role == ViewportLineRole.Selection).Should().Be(12);
        lines.Should().NotContain(l => l.Role == ViewportLineRole.NodeBounds);
    }

    [Fact]
    public void Wire_box_emits_twelve_edges()
    {
        var sink = new List<ViewportLine>();

        WireBox.AppendEdges(sink, UnitBox, ViewportLineRole.NodeBounds);

        sink.Should().HaveCount(12);
    }

    [Fact]
    public void A_point_light_adds_its_star_and_range_ring_glyph()
    {
        var scene = new EditorScene();
        scene.AddLight(SceneLight.CreatePoint("lamp").WithId(scene.AllocateLightId()));

        var lines = ViewportSceneDrawList.Build(scene, new NullBounds(), SceneElementRef.None, gridHalfCount: 0);

        lines.Count(l => l.Role == ViewportLineRole.Light).Should().Be(3 + LightGizmo.RangeRingSegments);
    }

    [Fact]
    public void A_directional_light_glyph_includes_its_aim_ray()
    {
        var scene = new EditorScene();
        scene.AddLight(SceneLight.CreateDirectional("sun").WithId(scene.AllocateLightId()));

        var lines = ViewportSceneDrawList.Build(scene, new NullBounds(), SceneElementRef.None, gridHalfCount: 0);

        lines.Count(l => l.Role == ViewportLineRole.Light).Should().Be(4, "three star arms plus the aim ray");
    }

    [Fact]
    public void Hidden_elements_draw_nothing()
    {
        var scene = new EditorScene();
        scene.Add(new SceneNode(
            scene.AllocateId(), "terrain", ScenePrimitive.AssetRef(ScenePrimitiveKind.Cube),
            EditorTransform.Identity).WithHidden(true));
        scene.AddLight(SceneLight.CreatePoint("torch").WithId(scene.AllocateLightId()).WithHidden(true));

        var lines = ViewportSceneDrawList.Build(scene, new NullBounds(), SceneElementRef.None, gridHalfCount: 0);

        lines.Should().OnlyContain(
            l => l.Role == ViewportLineRole.GridAxis,
            "a hidden node and a hidden light contribute no lines beyond the grid axes");
    }

    [Fact]
    public void A_parented_primitive_wireframe_is_drawn_through_its_parent_transform()
    {
        var scene = new EditorScene();
        var parent = scene.AllocateId();
        scene.Add(new SceneNode(
            parent, "group", null, EditorTransform.Identity with { Position = new Float3(10f, 0f, 0f) }));
        var child = scene.AllocateId();
        scene.Add(new SceneNode(
            child, "cube", ScenePrimitive.AssetRef(ScenePrimitiveKind.Cube), EditorTransform.Identity)
        {
            ParentId = parent,
        });

        var lines = ViewportSceneDrawList.Build(scene, new NullBounds(), SceneElementRef.None, gridHalfCount: 0);

        var primitiveEndpoints = lines
            .Where(l => l.Role == ViewportLineRole.NodeBounds)
            .SelectMany(l => new[] { l.A.X, l.B.X });
        primitiveEndpoints.Min().Should().BeGreaterThan(
            9f, "the child cube is drawn around its parent's world position, not the origin");
    }
}
