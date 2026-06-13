using System;
using System.Numerics;
using FluentAssertions;
using Opus.Editor.Core;
using Opus.Foundation.Geometry;
using Xunit;

namespace Opus.Editor.Ui.Tests;

/// <summary>Multi-element editing: duplicate / delete / hide, the cross-scene clipboard, group transforms, nudging, and snap.</summary>
public sealed partial class ViewportControllerTests
{
    [Fact]
    public void Duplicate_selected_clones_the_node_and_selects_the_copy()
    {
        var document = new EditorDocument("scene");
        var source = document.PlaceNode("tank", "m.glb", EditorTransform.Identity);
        var controller = new ViewportController(document, new FixedBounds(UnitBox));

        controller.DuplicateSelected().Should().BeTrue();

        document.Scene.Count.Should().Be(2);
        document.Selection.Should().NotBe(source, "the copy becomes the selection");
        document.Scene.Find(document.Selection)!.Name.Should().Be("tank copy");

        document.Undo().Should().BeTrue();
        document.Scene.Count.Should().Be(1, "the duplicate is one undoable edit");
    }

    [Fact]
    public void Duplicate_selected_without_a_selection_is_false()
    {
        var controller = new ViewportController(new EditorDocument("scene"), new NullBounds());

        controller.DuplicateSelected().Should().BeFalse();
    }

    [Fact]
    public void Delete_selected_removes_the_whole_multi_selection_as_one_undo()
    {
        var document = new EditorDocument("scene");
        var first = document.PlaceNode("a", "m.glb", EditorTransform.Identity);
        document.PlaceNode("b", "m.glb", EditorTransform.Identity);
        document.ToggleSelect(SceneElementRef.Node(first));
        var controller = new ViewportController(document, new FixedBounds(UnitBox));

        controller.DeleteSelected().Should().BeTrue();

        document.Scene.Count.Should().Be(0);
        document.Undo().Should().BeTrue();
        document.Scene.Count.Should().Be(2, "one undo restored both deleted members");
    }

    [Fact]
    public void Toggle_hidden_hides_a_mixed_visibility_selection_then_shows_it()
    {
        var document = new EditorDocument("scene");
        var first = document.PlaceNode("a", "m.glb", EditorTransform.Identity);
        var second = document.PlaceNode("b", "m.glb", EditorTransform.Identity);
        document.SetNodeHidden(second, true);
        document.Select(first);
        document.ToggleSelect(SceneElementRef.Node(second));
        var controller = new ViewportController(document, new FixedBounds(UnitBox));

        controller.ToggleSelectedHidden().Should().BeTrue();
        document.Scene.Find(first)!.Hidden.Should().BeTrue("any visible member hides the whole selection");
        document.Scene.Find(second)!.Hidden.Should().BeTrue();

        controller.ToggleSelectedHidden().Should().BeTrue();
        document.Scene.Find(first)!.Hidden.Should().BeFalse("an all-hidden selection shows");
        document.Scene.Find(second)!.Hidden.Should().BeFalse();
    }

    [Fact]
    public void Duplicate_selected_clones_the_whole_multi_selection()
    {
        var document = new EditorDocument("scene");
        var first = document.PlaceNode("a", "m.glb", EditorTransform.Identity);
        document.PlaceNode("b", "m.glb", EditorTransform.Identity);
        document.ToggleSelect(SceneElementRef.Node(first));
        var controller = new ViewportController(document, new FixedBounds(UnitBox));

        controller.DuplicateSelected().Should().BeTrue();

        document.Scene.Count.Should().Be(4);
        document.SelectedElements.Should().HaveCount(2, "the copies are the new selection");
        document.Undo().Should().BeTrue();
        document.Scene.Count.Should().Be(2, "the group duplicate is one undoable edit");
    }

    [Fact]
    public void Copy_and_paste_stamps_the_node_at_the_camera_target()
    {
        var document = new EditorDocument("scene");
        var source = document.PlaceNode("barrel", "models/barrel.glb", EditorTransform.Identity with
        {
            Position = new Float3(5f, 1f, 2f),
            RotationEulerDegrees = new Float3(0f, 45f, 0f),
            Scale = new Float3(2f, 2f, 2f),
        });
        var controller = new ViewportController(document, new FixedBounds(UnitBox));
        controller.Camera.Target = new Vector3(10f, 0f, -4f);

        controller.CopySelected().Should().BeTrue();
        controller.PasteAtTarget().Should().BeTrue();

        document.Scene.Count.Should().Be(2);
        var copy = document.Scene.Find(document.Selection)!;
        copy.Id.Should().NotBe(source, "the paste is a new node");
        copy.Name.Should().Be("barrel");
        copy.AssetRef.Should().Be("models/barrel.glb");
        copy.Transform.Position.Should().Be(new Float3(10f, 0f, -4f), "the paste lands at the camera target");
        copy.Transform.RotationEulerDegrees.Should().Be(new Float3(0f, 45f, 0f));
        copy.Transform.Scale.Should().Be(new Float3(2f, 2f, 2f));

        document.Undo().Should().BeTrue();
        document.Scene.Count.Should().Be(1, "the paste is one undoable edit");
    }

    [Fact]
    public void Paste_survives_deleting_the_source()
    {
        var document = new EditorDocument("scene");
        document.PlaceNode("barrel", "models/barrel.glb", EditorTransform.Identity);
        var controller = new ViewportController(document, new FixedBounds(UnitBox));

        controller.CopySelected().Should().BeTrue();
        controller.DeleteSelected().Should().BeTrue();
        controller.PasteAtTarget().Should().BeTrue();

        document.Scene.Count.Should().Be(1);
        document.Scene.Find(document.Selection)!.Name.Should().Be("barrel");
    }

    [Fact]
    public void Paste_survives_a_scene_switch()
    {
        var document = new EditorDocument("scene");
        document.PlaceNode("barrel", "models/barrel.glb", EditorTransform.Identity);
        var controller = new ViewportController(document, new FixedBounds(UnitBox));

        controller.CopySelected().Should().BeTrue();
        document.LoadScene(EditorSceneDocument.Empty("other"));
        controller.PasteAtTarget().Should().BeTrue();

        document.Scene.Count.Should().Be(1, "the clipboard lives on the controller, not the scene");
        document.Scene.Find(document.Selection)!.Name.Should().Be("barrel");
    }

    [Fact]
    public void A_copied_hidden_light_pastes_visible_with_its_parameters()
    {
        var document = new EditorDocument("scene");
        document.AddNewPointLight(new Float3(3f, 2f, 1f));
        var controller = new ViewportController(document, new NullBounds());
        controller.ToggleSelectedHidden().Should().BeTrue();
        controller.Camera.Target = new Vector3(7f, 0f, 0f);

        controller.CopySelected().Should().BeTrue();
        controller.PasteAtTarget().Should().BeTrue();

        document.Scene.LightCount.Should().Be(2);
        var copy = document.Scene.FindLight(document.LightSelection)!;
        copy.Kind.Should().Be(SceneLightKind.Point);
        copy.Position.Should().Be(new Float3(7f, 0f, 0f));
        copy.Hidden.Should().BeFalse("a paste is always visible");

        document.Undo().Should().BeTrue();
        document.Scene.LightCount.Should().Be(1, "the light paste is one undoable edit");
    }

    [Fact]
    public void Group_paste_preserves_the_members_relative_offsets()
    {
        var document = new EditorDocument("scene");
        var primary = document.PlaceNode("a", "m.glb", EditorTransform.Identity);
        document.PlaceNode("b", "m.glb", EditorTransform.Identity with { Position = new Float3(4f, 0f, 2f) });
        document.ToggleSelect(SceneElementRef.Node(primary));
        var controller = new ViewportController(document, new FixedBounds(UnitBox));
        controller.CopySelected().Should().BeTrue();
        controller.Camera.Target = new Vector3(10f, 0f, 0f);
        int depth = document.Commands.UndoDepth;

        controller.PasteAtTarget().Should().BeTrue();

        document.Scene.Count.Should().Be(4);
        document.SelectedElements.Should().HaveCount(2, "the pasted group is the new selection");
        var pastedPrimary = document.Scene.Find(document.SelectedElements[^1].AsNode)!;
        var pastedOther = document.Scene.Find(document.SelectedElements[0].AsNode)!;
        pastedPrimary.Transform.Position.Should().Be(
            new Float3(10f, 0f, 0f), "the copied primary lands on the camera target");
        pastedOther.Transform.Position.Should().Be(
            new Float3(14f, 0f, 2f), "the other member keeps its offset from the primary");
        document.Commands.UndoDepth.Should().Be(depth + 1, "the group paste is one history entry");

        document.Undo().Should().BeTrue();
        document.Scene.Count.Should().Be(2, "one undo removed the whole pasted group");
    }

    [Fact]
    public void Group_paste_carries_mixed_kinds()
    {
        var document = new EditorDocument("scene");
        var node = document.PlaceNode("a", "m.glb", EditorTransform.Identity);
        document.AddNewPointLight(new Float3(0f, 3f, 1f));
        document.ToggleSelect(SceneElementRef.Node(node));
        var controller = new ViewportController(document, new FixedBounds(UnitBox));
        controller.CopySelected().Should().BeTrue();
        controller.Camera.Target = new Vector3(5f, 0f, 0f);

        controller.PasteAtTarget().Should().BeTrue();

        document.Scene.Count.Should().Be(2);
        document.Scene.LightCount.Should().Be(2);
        // The pasted selection lists node copies before light copies.
        document.Scene.Find(document.SelectedElements[0].AsNode)!.Transform.Position.Should()
            .Be(new Float3(5f, 0f, 0f), "the copied primary node lands on the target");
        document.Scene.FindLight(document.SelectedElements[^1].AsLight)!.Position.Should()
            .Be(new Float3(5f, 3f, 1f), "the light keeps its offset from the primary");
    }

    [Fact]
    public void Paste_with_an_empty_clipboard_is_false()
    {
        var controller = new ViewportController(new EditorDocument("scene"), new NullBounds());

        controller.HasClipboard.Should().BeFalse();
        controller.PasteAtTarget().Should().BeFalse();
    }

    [Fact]
    public void Copy_with_no_selection_is_false_and_keeps_the_clipboard()
    {
        var document = new EditorDocument("scene");
        document.PlaceNode("barrel", "models/barrel.glb", EditorTransform.Identity);
        var controller = new ViewportController(document, new FixedBounds(UnitBox));
        controller.CopySelected().Should().BeTrue();
        document.Select(SceneNodeId.None);

        controller.CopySelected().Should().BeFalse();

        controller.HasClipboard.Should().BeTrue("a failed copy never clears a useful clipboard");
        controller.PasteAtTarget().Should().BeTrue();
    }

    [Fact]
    public void Outliner_scroll_is_floored_at_zero()
    {
        var controller = new ViewportController(new EditorDocument("scene"), new NullBounds());

        controller.SetOutlinerScroll(-3);

        controller.OutlinerScroll.Should().Be(0);
    }

    [Fact]
    public void Mirror_scroll_is_floored_at_zero()
    {
        var controller = new ViewportController(new EditorDocument("scene"), new NullBounds());

        controller.SetMirrorScroll(-3);

        controller.MirrorScroll.Should().Be(0);
    }

    [Fact]
    public void Mirror_line_count_matches_the_live_pseudo_code()
    {
        var document = new EditorDocument("scene");
        document.PlaceNode("alpha", "m.glb", EditorTransform.Identity);
        var controller = new ViewportController(document, new FixedBounds(UnitBox));

        controller.MirrorLineCount.Should().Be(document.ToPseudoCode().Split('\n').Length);
    }

    [Fact]
    public void Translate_drag_moves_the_whole_multi_selection_as_one_undo()
    {
        var document = new EditorDocument("scene");
        var primary = document.PlaceNode("a", "m.glb", EditorTransform.Identity);
        var follower = document.PlaceNode(
            "b", "m.glb", EditorTransform.Identity with { Position = new Float3(0f, 0f, 3f) });
        document.Select(follower);
        document.ToggleSelect(SceneElementRef.Node(primary));
        var controller = new ViewportController(document, new FixedBounds(UnitBox));
        float length = TranslateGizmo.HandleLength(controller.Camera.Distance);
        float aspect = Viewport.AspectRatio;
        var (originX01, originY01) = Project(controller, Vector3.Zero);
        var (tipX01, tipY01) = Project(controller, new Vector3(length, 0f, 0f));
        int depth = document.Commands.UndoDepth;

        controller.BeginGizmoDrag(GizmoAxis.X, originX01, originY01, aspect).Should().BeTrue();
        controller.UpdateGizmoDrag(tipX01, tipY01, aspect);
        controller.EndGizmoDrag();

        float movedX = document.Scene.Find(primary)!.Transform.Position.X;
        movedX.Should().BeApproximately(length, 1e-2f);
        document.Scene.Find(follower)!.Transform.Position.X.Should()
            .BeApproximately(movedX, 1e-4f, "the follower moves by the primary's delta");
        document.Scene.Find(follower)!.Transform.Position.Z.Should().Be(3f, "only the delta propagates");
        document.Commands.UndoDepth.Should().Be(depth + 1, "the group drag is one history entry");

        document.Undo().Should().BeTrue();
        document.Scene.Find(primary)!.Transform.Position.X.Should().Be(0f);
        document.Scene.Find(follower)!.Transform.Position.X.Should().Be(0f, "one undo restored both");
    }

    [Fact]
    public void Translate_drag_moves_a_selected_light_follower_with_the_primary_node()
    {
        var document = new EditorDocument("scene");
        var primary = document.PlaceNode("a", "m.glb", EditorTransform.Identity);
        var lamp = document.AddNewPointLight(new Float3(0f, 1f, 3f));
        document.ToggleSelect(SceneElementRef.Node(primary));
        var controller = new ViewportController(document, new FixedBounds(UnitBox));
        float length = TranslateGizmo.HandleLength(controller.Camera.Distance);
        float aspect = Viewport.AspectRatio;
        var (originX01, originY01) = Project(controller, Vector3.Zero);
        var (tipX01, tipY01) = Project(controller, new Vector3(length, 0f, 0f));

        controller.BeginGizmoDrag(GizmoAxis.X, originX01, originY01, aspect).Should().BeTrue();
        controller.UpdateGizmoDrag(tipX01, tipY01, aspect);
        controller.EndGizmoDrag();

        var light = document.Scene.FindLight(lamp)!;
        light.Position.X.Should().BeApproximately(
            document.Scene.Find(primary)!.Transform.Position.X, 1e-4f, "the light follower rode the delta");
        light.Position.Y.Should().Be(1f);
        light.Position.Z.Should().Be(3f);
    }

    [Fact]
    public void Rotate_drag_leaves_the_other_selected_members_in_place()
    {
        var document = new EditorDocument("scene");
        var primary = document.PlaceNode("a", "m.glb", EditorTransform.Identity);
        var follower = document.PlaceNode(
            "b", "m.glb", EditorTransform.Identity with { Position = new Float3(0f, 0f, 3f) });
        document.Select(follower);
        document.ToggleSelect(SceneElementRef.Node(primary));
        var controller = new ViewportController(document, new FixedBounds(UnitBox));
        controller.SetGizmoMode(GizmoMode.Rotate);
        float radius = TranslateGizmo.HandleLength(controller.Camera.Distance);
        float aspect = Viewport.AspectRatio;
        var (grabX01, grabY01) = Project(controller, new Vector3(0f, 0f, radius));
        var (dragX01, dragY01) = Project(controller, new Vector3(radius, 0f, 0f));

        controller.BeginGizmoDrag(GizmoAxis.Y, grabX01, grabY01, aspect).Should().BeTrue();
        controller.UpdateGizmoDrag(dragX01, dragY01, aspect);
        controller.EndGizmoDrag();

        document.Scene.Find(primary)!.Transform.RotationEulerDegrees.Y.Should().NotBe(0f);
        document.Scene.Find(follower)!.Transform.Should().Be(
            EditorTransform.Identity with { Position = new Float3(0f, 0f, 3f) },
            "rotation keeps per-element pivots, so the follower stands still");
    }

    [Fact]
    public void Planar_drag_slides_the_whole_multi_selection()
    {
        var document = new EditorDocument("scene");
        var primary = document.PlaceNode("a", "m.glb", EditorTransform.Identity);
        var follower = document.PlaceNode(
            "b", "m.glb", EditorTransform.Identity with { Position = new Float3(0f, 0f, 3f) });
        document.Select(follower);
        document.ToggleSelect(SceneElementRef.Node(primary));
        var controller = new ViewportController(document, new FixedBounds(UnitBox));
        float aspect = Viewport.AspectRatio;
        var (grabX01, grabY01) = Project(controller, new Vector3(-0.5f, 0f, 0.5f));
        var (dropX01, dropY01) = Project(controller, new Vector3(1.5f, 0f, 0.5f));
        int depth = document.Commands.UndoDepth;

        controller.BeginPlanarDrag(grabX01, grabY01, aspect).Should().BeTrue();
        controller.UpdatePlanarDrag(dropX01, dropY01, aspect);
        controller.EndPlanarDrag();

        float movedX = document.Scene.Find(primary)!.Transform.Position.X;
        movedX.Should().BeGreaterThan(1.5f);
        document.Scene.Find(follower)!.Transform.Position.X.Should().BeApproximately(movedX, 1e-4f);
        document.Commands.UndoDepth.Should().Be(depth + 1, "the planar group drag is one history entry");
    }

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
        var (dragX01, dragY01) = Project(controller, new Vector3(radius * MathF.Sin(radians), 0f, radius * MathF.Cos(radians)));

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

        // Grab at the tip (parameter ~ length) and drag to 1.8x it, so the factor is ~1.8 and the X scale
        // snaps to the nearest quarter step, 1.75.
        controller.BeginGizmoDrag(GizmoAxis.X, tipX01, tipY01, aspect).Should().BeTrue();
        controller.UpdateGizmoDrag(farX01, farY01, aspect, snap: true);

        document.Scene.Find(id)!.Transform.Scale.X.Should()
            .BeApproximately(1.75f, 0.01f, "the ~1.8 scale snaps to the nearest quarter step");
    }
}
