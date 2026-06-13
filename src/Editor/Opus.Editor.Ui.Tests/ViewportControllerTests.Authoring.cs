using System;
using System.Numerics;
using FluentAssertions;
using Opus.Editor.Core;
using Opus.Foundation.Geometry;
using Xunit;

namespace Opus.Editor.Ui.Tests;

/// <summary>Element authoring: creation at the camera target, light editing, modal rename / field edit, and shape / kind cycling.</summary>
public sealed partial class ViewportControllerTests
{
    [Fact]
    public void Add_node_at_target_places_a_selected_node_at_the_camera_target()
    {
        var document = new EditorDocument("scene");
        var controller = new ViewportController(document, new NullBounds());
        controller.Camera.Target = new Vector3(3f, 1f, -2f);

        var id = controller.AddNodeAtTarget();

        document.Selection.Should().Be(id);
        document.Scene.Find(id)!.Transform.Position.Should().Be(new Float3(3f, 1f, -2f));
    }

    [Fact]
    public void Add_point_light_at_target_places_the_light_at_the_camera_target()
    {
        var document = new EditorDocument("scene");
        var controller = new ViewportController(document, new NullBounds());
        controller.Camera.Target = new Vector3(0f, 5f, 0f);

        var id = controller.AddPointLightAtTarget();

        document.Scene.FindLight(id)!.Position.Should().Be(new Float3(0f, 5f, 0f));
    }

    [Fact]
    public void Pick_at_centre_selects_the_light_under_the_ray()
    {
        var document = new EditorDocument("scene");
        var lamp = document.AddNewPointLight(Float3.Zero);
        document.SelectLight(SceneLightId.None);
        var controller = new ViewportController(document, new NullBounds());

        var result = controller.PickAt(0.5f, 0.5f, 1.0f);

        result.Hit.Should().BeTrue();
        result.Element.Should().Be(SceneElementRef.Light(lamp));
        document.LightSelection.Should().Be(lamp);
    }

    [Fact]
    public void Delete_selected_removes_the_selected_light()
    {
        var document = new EditorDocument("scene");
        document.AddNewPointLight(new Float3(1f, 2f, 3f));
        var controller = new ViewportController(document, new NullBounds());

        controller.DeleteSelected().Should().BeTrue();

        document.Scene.LightCount.Should().Be(0);
        controller.ToolbarState.HasSelection.Should().BeFalse();
    }

    [Fact]
    public void Frame_selection_targets_the_selected_light()
    {
        var document = new EditorDocument("scene");
        document.AddNewPointLight(new Float3(7f, 8f, 9f));
        var controller = new ViewportController(document, new NullBounds());

        controller.FrameSelection().Should().BeTrue();

        controller.Camera.Target.Should().Be(new Vector3(7f, 8f, 9f));
    }

    [Fact]
    public void Dragging_the_translate_gizmo_moves_the_selected_light_as_one_undoable_edit()
    {
        var document = new EditorDocument("scene");
        document.AddNewPointLight(Float3.Zero);
        var controller = new ViewportController(document, new NullBounds());

        float length = TranslateGizmo.HandleLength(controller.Camera.Distance);
        var (grabX, grabY) = Project(controller, new Vector3(0.5f * length, 0f, 0f));
        var (dropX, dropY) = Project(controller, new Vector3(1.5f * length, 0f, 0f));

        controller.PickGizmoAxis(grabX, grabY, Viewport).Should().Be(GizmoAxis.X);
        controller.BeginGizmoDrag(GizmoAxis.X, grabX, grabY, Viewport.AspectRatio).Should().BeTrue();
        controller.UpdateGizmoDrag(dropX, dropY, Viewport.AspectRatio);
        controller.EndGizmoDrag();

        document.Scene.Lights[0].Position.X.Should().BeApproximately(length, 0.05f);
        document.Undo().Should().BeTrue();
        document.Scene.Lights[0].Position.X.Should().Be(0f, "the whole drag commits as one reversible edit");
    }

    [Fact]
    public void A_rotate_drag_aims_the_selected_spot_light()
    {
        var document = new EditorDocument("scene");
        var id = document.AddLight(SceneLight.CreateSpot("beam"));
        document.SelectLight(id);
        var controller = new ViewportController(document, new NullBounds());
        controller.SetGizmoMode(GizmoMode.Rotate);

        float radius = TranslateGizmo.HandleLength(controller.Camera.Distance);
        var (grabX, grabY) = Project(controller, new Vector3(0f, 0f, radius));
        var (dropX, dropY) = Project(controller, new Vector3(0f, radius, 0f));

        controller.BeginGizmoDrag(GizmoAxis.X, grabX, grabY, Viewport.AspectRatio).Should().BeTrue();
        controller.UpdateGizmoDrag(dropX, dropY, Viewport.AspectRatio);
        controller.EndGizmoDrag();

        var direction = document.Scene.FindLight(id)!.Direction.ToVector3();
        direction.Length().Should().BeApproximately(1f, 0.01f, "the aim keeps its length");
        direction.Y.Should().NotBeApproximately(-1f, 0.05f, "the ring drag swung the aim away from straight down");
        document.Undo().Should().BeTrue();
        document.Scene.FindLight(id)!.Direction.Should().Be(SceneLight.DefaultDirection);
    }

    [Fact]
    public void A_light_has_no_gizmo_in_scale_mode()
    {
        var document = new EditorDocument("scene");
        document.AddNewPointLight(Float3.Zero);
        var controller = new ViewportController(document, new NullBounds());
        controller.SetGizmoMode(GizmoMode.Scale);

        controller.PickGizmoAxis(0.5f, 0.5f, Viewport).Should().Be(GizmoAxis.None);
        controller.BeginGizmoDrag(GizmoAxis.X, 0.5f, 0.5f, Viewport.AspectRatio).Should().BeFalse();
    }

    [Fact]
    public void A_point_light_has_no_gizmo_in_rotate_mode()
    {
        var document = new EditorDocument("scene");
        document.AddNewPointLight(Float3.Zero);
        var controller = new ViewportController(document, new NullBounds());
        controller.SetGizmoMode(GizmoMode.Rotate);

        controller.PickGizmoAxis(0.5f, 0.5f, Viewport).Should().Be(GizmoAxis.None);
        controller.BeginGizmoDrag(GizmoAxis.X, 0.5f, 0.5f, Viewport.AspectRatio).Should().BeFalse();
    }

    [Fact]
    public void Begin_rename_seeds_the_buffer_with_the_current_name_and_commit_renames_the_node()
    {
        var document = new EditorDocument("scene");
        var id = document.PlaceNode("alpha", null, EditorTransform.Identity);
        var controller = new ViewportController(document, new NullBounds());

        controller.BeginRename().Should().BeTrue();
        controller.Rename!.Value.Buffer.Should().Be("alpha");

        controller.RenameBackspace();
        controller.RenameAppend('x');
        controller.CommitRename().Should().BeTrue();

        controller.Rename.Should().BeNull("the session ends on commit");
        document.Scene.Find(id)!.Name.Should().Be("alphx");
        document.Undo().Should().BeTrue();
        document.Scene.Find(id)!.Name.Should().Be("alpha", "the rename is one undoable edit");
    }

    [Fact]
    public void Commit_rename_renames_the_selected_light()
    {
        var document = new EditorDocument("scene");
        var id = document.AddNewPointLight(Float3.Zero);
        var controller = new ViewportController(document, new NullBounds());

        controller.BeginRename().Should().BeTrue();
        controller.RenameAppend('!');
        controller.CommitRename().Should().BeTrue();

        document.Scene.FindLight(id)!.Name.Should().EndWith("!");
    }

    [Fact]
    public void Cancel_rename_leaves_the_name_untouched()
    {
        var document = new EditorDocument("scene");
        var id = document.PlaceNode("alpha", null, EditorTransform.Identity);
        var controller = new ViewportController(document, new NullBounds());

        controller.BeginRename();
        controller.RenameAppend('z');
        controller.CancelRename();

        controller.Rename.Should().BeNull();
        document.Scene.Find(id)!.Name.Should().Be("alpha");
    }

    [Fact]
    public void A_buffer_that_trims_to_nothing_commits_as_a_cancel()
    {
        var document = new EditorDocument("scene");
        var id = document.PlaceNode("alpha", null, EditorTransform.Identity);
        var controller = new ViewportController(document, new NullBounds());
        controller.BeginRename();
        for (int i = 0; i < 5; i++)
        {
            controller.RenameBackspace();
        }

        controller.RenameAppend(' ');

        controller.CommitRename().Should().BeFalse();
        document.Scene.Find(id)!.Name.Should().Be("alpha", "an element never takes an empty name");
    }

    [Fact]
    public void Field_edit_commits_a_node_transform_as_one_undoable_edit()
    {
        var document = new EditorDocument("scene");
        var id = document.PlaceNode("tank", null, EditorTransform.Identity);
        var controller = new ViewportController(document, new NullBounds());

        controller.BeginFieldEdit(InspectorField.PositionX).Should().BeTrue();
        controller.FieldEditAppend('2');
        controller.FieldEditAppend('.');
        controller.FieldEditAppend('5');
        controller.CommitFieldEdit().Should().BeTrue();

        document.Scene.Find(id)!.Transform.Position.X.Should().Be(2.5f);
        controller.FieldEdit.Should().BeNull();
        document.Undo().Should().BeTrue();
        document.Scene.Find(id)!.Transform.Position.X.Should().Be(0f, "the commit was a single undoable edit");
    }

    [Fact]
    public void Field_edit_commits_a_light_field()
    {
        var document = new EditorDocument("scene");
        document.AddNewPointLight(Float3.Zero);
        var controller = new ViewportController(document, new NullBounds());

        controller.BeginFieldEdit(InspectorField.Intensity).Should().BeTrue();
        controller.FieldEditAppend('3');
        controller.CommitFieldEdit().Should().BeTrue();

        document.Scene.Lights[0].Intensity.Should().Be(3f);
    }

    [Fact]
    public void Field_edit_rejects_letters_in_the_numeric_buffer()
    {
        var document = new EditorDocument("scene");
        document.PlaceNode("tank", null, EditorTransform.Identity);
        var controller = new ViewportController(document, new NullBounds());
        controller.BeginFieldEdit(InspectorField.ScaleY);

        controller.FieldEditAppend('x');
        controller.FieldEditAppend('-');
        controller.FieldEditAppend('4');

        controller.FieldEdit!.Value.Buffer.Should().Be("-4", "letters never enter a numeric buffer");
    }

    [Fact]
    public void An_unparsable_or_empty_buffer_cancels_instead_of_committing()
    {
        var document = new EditorDocument("scene");
        var id = document.PlaceNode("tank", null, EditorTransform.Identity);
        var controller = new ViewportController(document, new NullBounds());

        controller.BeginFieldEdit(InspectorField.PositionZ).Should().BeTrue();
        controller.FieldEditAppend('-');
        controller.FieldEditAppend('.');
        controller.CommitFieldEdit().Should().BeFalse();

        document.Scene.Find(id)!.Transform.Position.Z.Should().Be(0f);
        controller.FieldEdit.Should().BeNull();

        controller.BeginFieldEdit(InspectorField.PositionZ).Should().BeTrue();
        controller.CommitFieldEdit().Should().BeFalse("an empty buffer commits nothing");
    }

    [Fact]
    public void A_field_that_does_not_apply_to_the_selection_does_not_begin()
    {
        var document = new EditorDocument("scene");
        document.PlaceNode("tank", null, EditorTransform.Identity);
        var controller = new ViewportController(document, new NullBounds());

        controller.BeginFieldEdit(InspectorField.Intensity).Should().BeFalse("a node has no intensity");
        controller.FieldEdit.Should().BeNull();
    }

    [Fact]
    public void Cancel_field_edit_leaves_the_element_untouched()
    {
        var document = new EditorDocument("scene");
        var id = document.PlaceNode("tank", null, EditorTransform.Identity);
        var controller = new ViewportController(document, new NullBounds());
        controller.BeginFieldEdit(InspectorField.RotationY);
        controller.FieldEditAppend('9');

        controller.CancelFieldEdit();

        controller.FieldEdit.Should().BeNull();
        document.Scene.Find(id)!.Transform.RotationEulerDegrees.Y.Should().Be(0f);
    }

    [Fact]
    public void Cycling_a_point_light_to_spot_seeds_a_real_cone_and_keeps_authored_values()
    {
        var document = new EditorDocument("scene");
        document.AddNewPointLight(new Float3(1f, 2f, 3f));
        var controller = new ViewportController(document, new NullBounds());

        controller.CycleSelectedLightKind().Should().BeTrue();

        var light = document.Scene.Lights[0];
        light.Kind.Should().Be(SceneLightKind.Spot);
        light.Position.Should().Be(new Float3(1f, 2f, 3f));
        light.SpotOuterAngleDegrees.Should().Be(SceneLight.DefaultSpotOuterAngleDegrees, "a zero-degree cone is unusable");
        light.SpotInnerAngleDegrees.Should().Be(SceneLight.DefaultSpotInnerAngleDegrees);

        document.Undo().Should().BeTrue();
        document.Scene.Lights[0].Kind.Should().Be(SceneLightKind.Point, "the cycle is one undoable edit");
    }

    [Fact]
    public void The_kind_cycle_walks_spot_directional_then_point()
    {
        var document = new EditorDocument("scene");
        document.AddNewPointLight(Float3.Zero);
        var controller = new ViewportController(document, new NullBounds());

        controller.CycleSelectedLightKind();
        controller.CycleSelectedLightKind();
        document.Scene.Lights[0].Kind.Should().Be(SceneLightKind.Directional);

        controller.CycleSelectedLightKind();
        var light = document.Scene.Lights[0];
        light.Kind.Should().Be(SceneLightKind.Point);
        light.Range.Should().BeGreaterThan(0f, "a positioned kind needs a usable range");
    }

    [Fact]
    public void The_shape_cycle_walks_empty_through_every_primitive_and_back()
    {
        var document = new EditorDocument("scene");
        var id = document.PlaceNewNode(EditorTransform.Identity);
        var controller = new ViewportController(document, new NullBounds());

        var seen = new System.Collections.Generic.List<string?>();
        for (int i = 0; i < 6; i++)
        {
            controller.CycleSelectedNodeShape().Should().BeTrue();
            seen.Add(document.Scene.Find(id)!.AssetRef);
        }

        seen.Should().Equal(
            "primitive:cube", "primitive:sphere", "primitive:cylinder", "primitive:plane", "primitive:cone", null);
    }

    [Fact]
    public void A_model_node_never_loses_its_reference_to_the_shape_cycle()
    {
        var document = new EditorDocument("scene");
        var id = document.PlaceNode("tank", "models/tank.glb", EditorTransform.Identity);
        var controller = new ViewportController(document, new NullBounds());

        controller.CycleSelectedNodeShape().Should().BeFalse();
        document.Scene.Find(id)!.AssetRef.Should().Be("models/tank.glb");
    }

    [Fact]
    public void Cycling_the_kind_with_a_node_selected_is_false()
    {
        var document = new EditorDocument("scene");
        document.PlaceNode("tank", null, EditorTransform.Identity);
        var controller = new ViewportController(document, new NullBounds());

        controller.CycleSelectedLightKind().Should().BeFalse();
    }
}
