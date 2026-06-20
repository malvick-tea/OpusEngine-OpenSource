using System;
using System.Numerics;
using FluentAssertions;
using Opus.Editor.Core;
using Opus.Foundation.Geometry;
using Xunit;

namespace Opus.Editor.Ui.Tests;

public sealed partial class ViewportControllerTests
{
    [Fact]
    public void Nudge_moves_the_whole_selection_as_one_undo()
    {
        var document = new EditorDocument("scene");
        var node = document.PlaceNode("a", "m.glb", EditorTransform.Identity);
        var lamp = document.AddNewPointLight(new Float3(0f, 2f, 3f));
        document.ToggleSelect(SceneElementRef.Node(node));
        var controller = new ViewportController(document, new FixedBounds(UnitBox));
        int depth = document.Commands.UndoDepth;

        controller.NudgeSelection(new Vector3(1f, 0f, 0f)).Should().BeTrue();

        document.Scene.Find(node)!.Transform.Position.Should().Be(new Float3(1f, 0f, 0f));
        document.Scene.FindLight(lamp)!.Position.Should().Be(new Float3(1f, 2f, 3f));
        document.Commands.UndoDepth.Should().Be(depth + 1, "the group nudge is one history entry");

        document.Undo().Should().BeTrue();
        document.Scene.Find(node)!.Transform.Position.Should().Be(Float3.Zero);
        document.Scene.FindLight(lamp)!.Position.Should().Be(new Float3(0f, 2f, 3f));
    }

    [Fact]
    public void Nudge_with_no_selection_is_false()
    {
        var controller = new ViewportController(new EditorDocument("scene"), new NullBounds());

        controller.NudgeSelection(new Vector3(1f, 0f, 0f)).Should().BeFalse();
    }

    [Fact]
    public void Framing_a_multi_selection_targets_the_union_centre()
    {
        var document = new EditorDocument("scene");
        var first = document.PlaceNode("a", "m.glb", EditorTransform.Identity);
        document.PlaceNode("b", "m.glb", EditorTransform.Identity with { Position = new Float3(10f, 0f, 0f) });
        document.ToggleSelect(SceneElementRef.Node(first));
        var controller = new ViewportController(document, new FixedBounds(UnitBox));

        controller.FrameSelection().Should().BeTrue();

        controller.Camera.Target.Should().Be(new Vector3(5f, 0f, 0f), "the union of both unit boxes centres at x=5");
    }

    [Fact]
    public void Snapped_translate_drag_lands_on_a_whole_metre()
    {
        var document = new EditorDocument("scene");
        var id = document.PlaceNode("tank", "m.glb", EditorTransform.Identity);
        var controller = new ViewportController(document, new FixedBounds(UnitBox));
        float length = TranslateGizmo.HandleLength(controller.Camera.Distance);
        float aspect = Viewport.AspectRatio;
        var (originX01, originY01) = Project(controller, Vector3.Zero);
        var (tipX01, tipY01) = Project(controller, new Vector3(length, 0f, 0f));

        controller.BeginGizmoDrag(GizmoAxis.X, originX01, originY01, aspect).Should().BeTrue();
        controller.UpdateGizmoDrag(tipX01, tipY01, aspect, snap: true);

        document.Scene.Find(id)!.Transform.Position.X.Should()
            .Be(2f, "the ~1.8 m drag snaps to the nearest whole metre");
    }

    [Fact]
    public void Snapped_rotate_drag_lands_on_a_fixed_degree_step()
    {
        var document = new EditorDocument("scene");
        var id = document.PlaceNode("tank", "m.glb", EditorTransform.Identity);
        var controller = new ViewportController(document, new FixedBounds(UnitBox));
        controller.SetGizmoMode(GizmoMode.Rotate);
        float radius = TranslateGizmo.HandleLength(controller.Camera.Distance);
        float aspect = Viewport.AspectRatio;
        float radians = 40f * MathF.PI / 180f;
        var (grabX01, grabY01) = Project(controller, new Vector3(0f, 0f, radius));
        var (dragX01, dragY01) = Project(
            controller,
            new Vector3(radius * MathF.Sin(radians), 0f, radius * MathF.Cos(radians)));

        controller.BeginGizmoDrag(GizmoAxis.Y, grabX01, grabY01, aspect).Should().BeTrue();
        controller.UpdateGizmoDrag(dragX01, dragY01, aspect, snap: true);

        document.Scene.Find(id)!.Transform.RotationEulerDegrees.Y.Should()
            .BeApproximately(45f, 0.01f, "a ~40 degree drag snaps to the nearest 15 degree step");
    }

    [Fact]
    public void Snapped_scale_drag_lands_on_a_quarter_step()
    {
        var document = new EditorDocument("scene");
        var id = document.PlaceNode("tank", "m.glb", EditorTransform.Identity);
        var controller = new ViewportController(document, new FixedBounds(UnitBox));
        controller.SetGizmoMode(GizmoMode.Scale);
        float length = TranslateGizmo.HandleLength(controller.Camera.Distance);
        float aspect = Viewport.AspectRatio;
        var (tipX01, tipY01) = Project(controller, new Vector3(length, 0f, 0f));
        var (farX01, farY01) = Project(controller, new Vector3(1.8f * length, 0f, 0f));

        controller.BeginGizmoDrag(GizmoAxis.X, tipX01, tipY01, aspect).Should().BeTrue();
        controller.UpdateGizmoDrag(farX01, farY01, aspect, snap: true);

        document.Scene.Find(id)!.Transform.Scale.X.Should()
            .BeApproximately(1.75f, 0.01f, "the ~1.8 scale snaps to the nearest quarter step");
    }
}
