using System;
using System.Numerics;
using FluentAssertions;
using Opus.Editor.Core;
using Opus.Foundation.Geometry;
using Xunit;

namespace Opus.Editor.Ui.Tests;

public sealed partial class ViewportControllerTests
{
    private static readonly Aabb UnitBox = new(new Vector3(-1f), new Vector3(1f));
    private static readonly EditorPanelRect Viewport = new(0, 0, 800, 600);

    [Fact]
    public void Pick_at_centre_selects_the_node_under_the_ray()
    {
        var document = new EditorDocument("scene");
        var id = document.PlaceNode("tank", "m.glb", EditorTransform.Identity);
        document.Select(SceneNodeId.None);
        var controller = new ViewportController(document, new FixedBounds(UnitBox));

        var result = controller.PickAt(0.5f, 0.5f, 1.0f);

        result.Hit.Should().BeTrue();
        document.Selection.Should().Be(id);
    }

    [Fact]
    public void Pick_on_empty_space_clears_the_selection()
    {
        var document = new EditorDocument("scene");
        document.PlaceNode("tank", "m.glb", EditorTransform.Identity);
        var controller = new ViewportController(document, new FixedBounds(UnitBox));

        var result = controller.PickAt(0.99f, 0.99f, 1.0f);

        result.Hit.Should().BeFalse();
        document.Selection.Should().Be(SceneNodeId.None);
    }

    [Fact]
    public void Frame_selection_moves_the_camera_target_to_the_node()
    {
        var document = new EditorDocument("scene");
        document.PlaceNode("tank", "m.glb", new EditorTransform(new Float3(10f, 2f, -3f), Float3.Zero, Float3.One));
        var controller = new ViewportController(document, new FixedBounds(UnitBox));

        controller.FrameSelection().Should().BeTrue();
        controller.Camera.Target.Should().Be(new Vector3(10f, 2f, -3f));
    }

    [Fact]
    public void Frame_with_nothing_selected_and_nothing_visible_is_false()
    {
        var document = new EditorDocument("scene");
        var controller = new ViewportController(document, new NullBounds());

        controller.FrameSelection().Should().BeFalse("an empty scene has nothing to frame");
    }

    [Fact]
    public void Frame_fits_the_orbit_distance_to_the_selected_bounds()
    {
        var document = new EditorDocument("scene");
        document.PlaceNode("tank", "m.glb", EditorTransform.Identity);
        var controller = new ViewportController(document, new FixedBounds(UnitBox));

        controller.FrameSelection().Should().BeTrue();

        float expected = CameraFraming.FitDistance(UnitBox, controller.Camera.FieldOfViewDegrees);
        controller.Camera.Distance.Should().BeApproximately(expected, 1e-4f);
    }

    [Fact]
    public void Frame_without_a_selection_fits_the_whole_visible_scene()
    {
        var document = new EditorDocument("scene");
        document.PlaceNode("west", null, new EditorTransform(new Float3(-10f, 0f, 0f), Float3.Zero, Float3.One));
        document.PlaceNode("east", null, new EditorTransform(new Float3(10f, 0f, 0f), Float3.Zero, Float3.One));
        document.Select(SceneNodeId.None);
        var controller = new ViewportController(document, new FixedBounds(UnitBox));

        controller.FrameSelection().Should().BeTrue();

        controller.Camera.Target.Should().Be(Vector3.Zero, "the union of both nodes centres at the origin");
        controller.Camera.Distance.Should().BeGreaterThan(
            CameraFraming.FitDistance(UnitBox, controller.Camera.FieldOfViewDegrees),
            "two spread nodes need more distance than one");
    }

    [Fact]
    public void Frame_all_ignores_hidden_elements()
    {
        var document = new EditorDocument("scene");
        document.PlaceNode("home", null, EditorTransform.Identity);
        var far = document.PlaceNode("far", null, new EditorTransform(new Float3(100f, 0f, 0f), Float3.Zero, Float3.One));
        document.SetNodeHidden(far, hidden: true);
        document.Select(SceneNodeId.None);
        var controller = new ViewportController(document, new FixedBounds(UnitBox));

        controller.FrameSelection().Should().BeTrue();

        controller.Camera.Target.Should().Be(Vector3.Zero, "the hidden far node does not stretch the frame");
    }

    [Fact]
    public void Fit_distance_never_drops_below_the_working_minimum()
    {
        var point = new Opus.Foundation.Geometry.Aabb(Vector3.Zero, Vector3.Zero);

        CameraFraming.FitDistance(point, 60f).Should().Be(CameraFraming.MinDistance);
    }

    [Fact]
    public void Pick_gizmo_axis_without_a_selection_is_none()
    {
        var controller = new ViewportController(new EditorDocument("scene"), new NullBounds());

        controller.PickGizmoAxis(0.5f, 0.5f, Viewport).Should().Be(GizmoAxis.None);
    }

    [Fact]
    public void Pick_gizmo_axis_on_the_projected_handle_returns_that_axis()
    {
        var document = new EditorDocument("scene");
        document.PlaceNode("tank", "m.glb", EditorTransform.Identity);
        var controller = new ViewportController(document, new FixedBounds(UnitBox));
        var (x01, y01) = HandleMidpoint(controller, new Vector3(1f, 0f, 0f));

        controller.PickGizmoAxis(x01, y01, Viewport).Should().Be(GizmoAxis.X);
    }

    [Fact]
    public void Dragging_the_x_gizmo_slides_the_node_along_x_as_one_undoable_edit()
    {
        var document = new EditorDocument("scene");
        var id = document.PlaceNode("tank", "m.glb", EditorTransform.Identity);
        var controller = new ViewportController(document, new FixedBounds(UnitBox));
        float length = TranslateGizmo.HandleLength(controller.Camera.Distance);
        float aspect = Viewport.AspectRatio;
        var (originX01, originY01) = Project(controller, Vector3.Zero);
        var (tipX01, tipY01) = Project(controller, new Vector3(length, 0f, 0f));

        controller.BeginGizmoDrag(GizmoAxis.X, originX01, originY01, aspect).Should().BeTrue();
        controller.UpdateGizmoDrag(tipX01, tipY01, aspect);

        var moved = document.Scene.Find(id)!.Transform.Position;
        moved.X.Should().BeApproximately(length, 1e-2f);
        moved.Y.Should().Be(0f);
        moved.Z.Should().Be(0f);

        controller.EndGizmoDrag();
        document.Undo().Should().BeTrue();
        document.Scene.Find(id)!.Transform.Position.Should().Be(Float3.Zero);
        document.Scene.Contains(id).Should().BeTrue("the drag commits a single transform edit, not a placement undo");
    }

    [Fact]
    public void Gizmo_mode_defaults_to_translate_and_can_switch_to_scale()
    {
        var controller = new ViewportController(new EditorDocument("scene"), new NullBounds());

        controller.GizmoMode.Should().Be(GizmoMode.Translate);
        controller.SetGizmoMode(GizmoMode.Scale);
        controller.GizmoMode.Should().Be(GizmoMode.Scale);
    }

    [Fact]
    public void Scale_mode_dragging_the_x_gizmo_scales_along_x_only()
    {
        var document = new EditorDocument("scene");
        var id = document.PlaceNode("tank", "m.glb", EditorTransform.Identity);
        var controller = new ViewportController(document, new FixedBounds(UnitBox));
        controller.SetGizmoMode(GizmoMode.Scale);
        float length = TranslateGizmo.HandleLength(controller.Camera.Distance);
        float aspect = Viewport.AspectRatio;
        var (tipX01, tipY01) = Project(controller, new Vector3(length, 0f, 0f));
        var (farX01, farY01) = Project(controller, new Vector3(2f * length, 0f, 0f));

        // Grab at the tip (parameter ≈ length) and drag to twice that distance (parameter ≈ 2·length),
        // so the factor is ≈ 2 and the X scale doubles.
        controller.BeginGizmoDrag(GizmoAxis.X, tipX01, tipY01, aspect).Should().BeTrue();
        controller.UpdateGizmoDrag(farX01, farY01, aspect);

        var transform = document.Scene.Find(id)!.Transform;
        transform.Scale.X.Should().BeApproximately(2f, 0.1f);
        transform.Scale.Y.Should().Be(1f);
        transform.Scale.Z.Should().Be(1f);
        transform.Position.Should().Be(Float3.Zero, "scaling must not move the node");

        controller.EndGizmoDrag();
        document.Undo().Should().BeTrue();
        document.Scene.Find(id)!.Transform.Scale.Should().Be(Float3.One, "the scale drag is one undoable edit");
    }

    [Fact]
    public void Gizmo_mode_can_switch_to_rotate()
    {
        var controller = new ViewportController(new EditorDocument("scene"), new NullBounds());

        controller.SetGizmoMode(GizmoMode.Rotate);

        controller.GizmoMode.Should().Be(GizmoMode.Rotate);
    }

    [Fact]
    public void Rotate_mode_dragging_the_y_ring_rotates_about_y_only()
    {
        var document = new EditorDocument("scene");
        var id = document.PlaceNode("tank", "m.glb", EditorTransform.Identity);
        var controller = new ViewportController(document, new FixedBounds(UnitBox));
        controller.SetGizmoMode(GizmoMode.Rotate);
        float radius = TranslateGizmo.HandleLength(controller.Camera.Distance);
        float aspect = Viewport.AspectRatio;
        var (grabX01, grabY01) = Project(controller, new Vector3(0f, 0f, radius));
        var (dragX01, dragY01) = Project(controller, new Vector3(radius, 0f, 0f));

        // Grab the Y ring at its +Z point (angle 0) and drag to its +X point (a quarter turn), so the node
        // rotates +90 degrees about Y and nothing else moves.
        controller.BeginGizmoDrag(GizmoAxis.Y, grabX01, grabY01, aspect).Should().BeTrue();
        controller.UpdateGizmoDrag(dragX01, dragY01, aspect);

        var transform = document.Scene.Find(id)!.Transform;
        transform.RotationEulerDegrees.Y.Should().BeApproximately(90f, 1f);
        transform.RotationEulerDegrees.X.Should().Be(0f);
        transform.RotationEulerDegrees.Z.Should().Be(0f);
        transform.Position.Should().Be(Float3.Zero, "rotating must not move the node");
        transform.Scale.Should().Be(Float3.One, "rotating must not scale the node");

        controller.EndGizmoDrag();
        document.Undo().Should().BeTrue();
        document.Scene.Find(id)!.Transform.RotationEulerDegrees.Should()
            .Be(Float3.Zero, "the rotate drag is one undoable edit");
    }
}
