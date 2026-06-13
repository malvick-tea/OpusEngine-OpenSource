using FluentAssertions;
using Xunit;

namespace Opus.Editor.Core.Tests;

public sealed partial class EditorDocumentTests
{
    [Fact]
    public void Place_selects_the_new_node_and_marks_dirty()
    {
        var document = new EditorDocument("scene");

        var id = document.PlaceNode("tank", "models/tank.glb", EditorTransform.Identity);

        document.Selection.Should().Be(id);
        document.IsDirty.Should().BeTrue();
        document.Scene.Find(id)!.AssetRef.Should().Be("models/tank.glb");
    }

    [Fact]
    public void Remove_clears_a_matching_selection()
    {
        var document = new EditorDocument("scene");
        var id = document.PlaceNode("tank", null, EditorTransform.Identity);

        document.RemoveNode(id).Should().BeTrue();

        document.Selection.Should().Be(SceneNodeId.None);
        document.Scene.Count.Should().Be(0);
    }

    [Fact]
    public void Place_new_primitive_names_selects_and_carries_the_primitive_ref()
    {
        var document = new EditorDocument("scene");

        var id = document.PlaceNewPrimitive(
            ScenePrimitiveKind.Sphere, EditorTransform.Identity with { Position = new Float3(1f, 2f, 3f) });

        document.Selection.Should().Be(id);
        var node = document.Scene.Find(id)!;
        node.Name.Should().Be($"sphere {id.Value}");
        node.AssetRef.Should().Be("primitive:sphere");
        node.Transform.Position.Should().Be(new Float3(1f, 2f, 3f));
    }

    [Fact]
    public void Place_new_primitive_is_one_undoable_edit()
    {
        var document = new EditorDocument("scene");

        document.PlaceNewPrimitive(ScenePrimitiveKind.Cone, EditorTransform.Identity);
        document.Undo().Should().BeTrue();

        document.Scene.Count.Should().Be(0);
        document.Selection.Should().Be(SceneNodeId.None);
    }

    [Fact]
    public void Set_node_asset_round_trips_through_undo_and_redo()
    {
        var document = new EditorDocument("scene");
        var id = document.PlaceNode("box", null, EditorTransform.Identity);

        document.SetNodeAsset(id, "primitive:cube").Should().BeTrue();
        document.Scene.Find(id)!.AssetRef.Should().Be("primitive:cube");

        document.Undo();
        document.Scene.Find(id)!.AssetRef.Should().BeNull();

        document.Redo();
        document.Scene.Find(id)!.AssetRef.Should().Be("primitive:cube");
    }

    [Fact]
    public void Set_node_asset_is_a_no_op_for_an_unchanged_reference_or_missing_node()
    {
        var document = new EditorDocument("scene");
        var id = document.PlaceNode("box", "m.glb", EditorTransform.Identity);

        document.SetNodeAsset(id, "m.glb").Should().BeFalse();
        document.SetNodeAsset(new SceneNodeId(99), null).Should().BeFalse();
    }

    [Fact]
    public void Removing_a_missing_node_is_a_no_op()
    {
        var document = new EditorDocument("scene");

        document.RemoveNode(new SceneNodeId(99)).Should().BeFalse();
    }

    [Fact]
    public void Node_id_is_stable_across_undo_and_redo()
    {
        var document = new EditorDocument("scene");
        var id = document.PlaceNode("tank", null, EditorTransform.Identity);

        document.Undo();
        document.Scene.Count.Should().Be(0);

        document.Redo();
        document.Scene.Count.Should().Be(1);
        document.Scene.Nodes[0].Id.Should().Be(id);
    }

    [Fact]
    public void Allocation_after_undo_never_reuses_an_id()
    {
        var document = new EditorDocument("scene");
        document.PlaceNode("a", null, EditorTransform.Identity);
        var b = document.PlaceNode("b", null, EditorTransform.Identity);

        document.Undo();
        var c = document.PlaceNode("c", null, EditorTransform.Identity);

        c.Value.Should().BeGreaterThan(b.Value);
        document.Scene.Count.Should().Be(2);
    }

    [Fact]
    public void Transform_then_undo_restores_previous_transform()
    {
        var document = new EditorDocument("scene");
        var id = document.PlaceNode("a", null, EditorTransform.Identity);
        var moved = new EditorTransform(new Float3(1f, 2f, 3f), Float3.Zero, Float3.One);

        document.TransformNode(id, moved);
        document.Scene.Find(id)!.Transform.Should().Be(moved);

        document.Undo();
        document.Scene.Find(id)!.Transform.Should().Be(EditorTransform.Identity);
    }

    [Fact]
    public void Preview_transform_moves_the_node_without_recording_an_undo_step()
    {
        var document = new EditorDocument("scene");
        var id = document.PlaceNode("a", null, EditorTransform.Identity);
        var moved = EditorTransform.Identity with { Position = new Float3(4f, 0f, 0f) };

        document.PreviewNodeTransform(id, moved).Should().BeTrue();
        document.Scene.Find(id)!.Transform.Should().Be(moved);

        document.Undo();
        document.Scene.Contains(id).Should().BeFalse("a preview records no undo step, so undo reverts the placement");
    }

    [Fact]
    public void Commit_transform_collapses_a_drag_into_one_reversible_edit()
    {
        var document = new EditorDocument("scene");
        var id = document.PlaceNode("a", null, EditorTransform.Identity);
        var start = document.Scene.Find(id)!.Transform;
        var target = start with { Position = new Float3(5f, 0f, 0f) };

        document.PreviewNodeTransform(id, target);
        document.CommitNodeTransform(id, start, target).Should().BeTrue();
        document.Scene.Find(id)!.Transform.Should().Be(target);

        document.Undo();
        document.Scene.Find(id)!.Transform.Should().Be(start, "one undo reverts the whole drag");
        document.Redo();
        document.Scene.Find(id)!.Transform.Should().Be(target);
    }

    [Fact]
    public void Rename_then_undo_restores_previous_name()
    {
        var document = new EditorDocument("scene");
        var id = document.PlaceNode("a", null, EditorTransform.Identity);

        document.RenameNode(id, "b");
        document.Scene.Find(id)!.Name.Should().Be("b");

        document.Undo();
        document.Scene.Find(id)!.Name.Should().Be("a");
    }

    [Fact]
    public void Duplicate_clones_with_a_copy_name_offset_and_selects_the_copy()
    {
        var document = new EditorDocument("scene");
        var transform = new EditorTransform(new Float3(2f, 0f, 0f), new Float3(0f, 45f, 0f), new Float3(2f, 2f, 2f));
        var source = document.PlaceNode("tank", "models/tank.glb", transform);

        var copy = document.DuplicateNode(source);

        copy.IsValid.Should().BeTrue();
        document.Selection.Should().Be(copy, "the copy becomes the new selection");
        document.Scene.Count.Should().Be(2);
        var node = document.Scene.Find(copy)!;
        node.Name.Should().Be("tank copy");
        node.AssetRef.Should().Be("models/tank.glb");
        node.Transform.Position.Should().Be(new Float3(3f, 0f, 0f), "the copy is offset one metre along X");
        node.Transform.RotationEulerDegrees.Should().Be(new Float3(0f, 45f, 0f), "rotation carries to the copy");
        node.Transform.Scale.Should().Be(new Float3(2f, 2f, 2f), "scale carries to the copy");
    }

    [Fact]
    public void Duplicate_honours_an_explicit_position()
    {
        var document = new EditorDocument("scene");
        var source = document.PlaceNode("a", "m.glb", EditorTransform.Identity);

        var copy = document.DuplicateNode(source, new Float3(7f, 1f, 3f));

        document.Scene.Find(copy)!.Transform.Position.Should().Be(new Float3(7f, 1f, 3f));
    }

    [Fact]
    public void Duplicate_of_a_missing_node_returns_none()
    {
        var document = new EditorDocument("scene");

        document.DuplicateNode(new SceneNodeId(99)).Should().Be(SceneNodeId.None);
        document.Scene.Count.Should().Be(0);
    }

    [Fact]
    public void Duplicate_then_undo_removes_only_the_copy()
    {
        var document = new EditorDocument("scene");
        var source = document.PlaceNode("a", "m.glb", EditorTransform.Identity);

        document.DuplicateNode(source);
        document.Scene.Count.Should().Be(2);

        document.Undo().Should().BeTrue();
        document.Scene.Count.Should().Be(1);
        document.Scene.Contains(source).Should().BeTrue("undo removes the copy, not the original");
    }

    [Fact]
    public void Add_light_allocates_an_id_and_marks_dirty()
    {
        var document = new EditorDocument("scene");

        var id = document.AddLight(SceneLight.CreateDirectional("sun"));

        id.IsValid.Should().BeTrue();
        document.IsDirty.Should().BeTrue();
        document.Scene.FindLight(id)!.Kind.Should().Be(SceneLightKind.Directional);
    }

    [Fact]
    public void Add_light_does_not_change_the_node_selection()
    {
        var document = new EditorDocument("scene");
        var node = document.PlaceNode("a", null, EditorTransform.Identity);

        document.AddLight(SceneLight.CreatePoint("lamp"));

        document.Selection.Should().Be(node, "lights are a separate element kind from the node selection");
    }

    [Fact]
    public void Add_light_then_undo_removes_it_and_redo_restores_a_stable_id()
    {
        var document = new EditorDocument("scene");
        var id = document.AddLight(SceneLight.CreateSpot("torch"));

        document.Undo();
        document.Scene.LightCount.Should().Be(0);

        document.Redo();
        document.Scene.LightCount.Should().Be(1);
        document.Scene.Lights[0].Id.Should().Be(id);
    }

    [Fact]
    public void Remove_light_is_undoable()
    {
        var document = new EditorDocument("scene");
        var id = document.AddLight(SceneLight.CreatePoint("lamp"));

        document.RemoveLight(id).Should().BeTrue();
        document.Scene.LightCount.Should().Be(0);

        document.Undo();
        document.Scene.FindLight(id)!.Name.Should().Be("lamp");
    }

    [Fact]
    public void Removing_a_missing_light_is_a_no_op()
    {
        var document = new EditorDocument("scene");

        document.RemoveLight(new SceneLightId(99)).Should().BeFalse();
    }

    [Fact]
    public void Set_light_replaces_fields_but_keeps_the_id_and_marks_dirty()
    {
        var document = new EditorDocument("scene");
        var id = document.AddLight(SceneLight.CreatePoint("lamp"));
        document.MarkSaved();

        var edited = document.Scene.FindLight(id)! with { Intensity = 5f, Range = 20f };
        document.SetLight(edited).Should().BeTrue();

        var stored = document.Scene.FindLight(id)!;
        stored.Id.Should().Be(id);
        stored.Intensity.Should().Be(5f);
        stored.Range.Should().Be(20f);
        document.IsDirty.Should().BeTrue();
    }

    [Fact]
    public void Set_light_then_undo_restores_the_previous_value()
    {
        var document = new EditorDocument("scene");
        var id = document.AddLight(SceneLight.CreatePoint("lamp"));

        document.SetLight(document.Scene.FindLight(id)! with { Intensity = 9f });
        document.Undo();

        document.Scene.FindLight(id)!.Intensity.Should().Be(SceneLight.DefaultIntensity);
    }

    [Fact]
    public void Setting_a_missing_light_is_a_no_op()
    {
        var document = new EditorDocument("scene");

        document.SetLight(SceneLight.CreatePoint("ghost").WithId(new SceneLightId(99))).Should().BeFalse();
    }

    [Fact]
    public void Changed_event_fires_on_mutation()
    {
        var document = new EditorDocument("scene");
        int notifications = 0;
        document.Changed += () => notifications++;

        document.PlaceNode("a", null, EditorTransform.Identity);

        notifications.Should().BeGreaterThan(0);
    }

    [Fact]
    public void Mark_saved_clears_the_dirty_flag()
    {
        var document = new EditorDocument("scene");
        document.PlaceNode("a", null, EditorTransform.Identity);

        document.MarkSaved();

        document.IsDirty.Should().BeFalse();
    }

    [Fact]
    public void Load_scene_replaces_document_and_resets_history()
    {
        var document = new EditorDocument("scene");
        document.PlaceNode("a", null, EditorTransform.Identity);

        var loaded = new EditorSceneDocument(
            "Harbor",
            new[] { new SceneNode(new SceneNodeId(5), "x", "m.glb", EditorTransform.Identity) });
        document.LoadScene(loaded);

        document.Name.Should().Be("Harbor");
        document.Scene.Count.Should().Be(1);
        document.IsDirty.Should().BeFalse();
        document.CanUndo.Should().BeFalse();
        document.PlaceNode("y", null, EditorTransform.Identity).Value.Should().Be(6);
    }

    [Fact]
    public void Place_new_node_names_it_after_its_id_selects_it_and_is_undoable()
    {
        var document = new EditorDocument("scene");
        var transform = EditorTransform.Identity with { Position = new Float3(2f, 0f, -1f) };

        var id = document.PlaceNewNode(transform);

        var node = document.Scene.Find(id)!;
        node.Name.Should().Be($"node {id.Value}");
        node.AssetRef.Should().BeNull("a window-created node starts as an empty placeholder");
        node.Transform.Should().Be(transform);
        document.Selection.Should().Be(id);

        document.Undo().Should().BeTrue();
        document.Scene.Count.Should().Be(0);
    }

    [Fact]
    public void Add_new_point_light_names_it_after_its_id_positions_it_and_is_undoable()
    {
        var document = new EditorDocument("scene");

        var id = document.AddNewPointLight(new Float3(1f, 2f, 3f));

        var light = document.Scene.FindLight(id)!;
        light.Name.Should().Be($"light {id.Value}");
        light.Kind.Should().Be(SceneLightKind.Point);
        light.Position.Should().Be(new Float3(1f, 2f, 3f));
        document.IsDirty.Should().BeTrue();

        document.Undo().Should().BeTrue();
        document.Scene.LightCount.Should().Be(0);
    }

    [Fact]
    public void Selecting_a_light_clears_the_node_selection_and_vice_versa()
    {
        var document = new EditorDocument("scene");
        var node = document.PlaceNode("a", null, EditorTransform.Identity);
        var lamp = document.AddNewPointLight(Float3.Zero);

        document.LightSelection.Should().Be(lamp, "adding a light from the window selects it");
        document.Selection.Should().Be(SceneNodeId.None);
        document.SelectedElement.Should().Be(SceneElementRef.Light(lamp));

        document.Select(node);

        document.Selection.Should().Be(node);
        document.LightSelection.Should().Be(SceneLightId.None);
        document.SelectedElement.Should().Be(SceneElementRef.Node(node));
    }

    [Fact]
    public void Removing_the_selected_light_clears_the_light_selection()
    {
        var document = new EditorDocument("scene");
        var lamp = document.AddNewPointLight(Float3.Zero);

        document.RemoveLight(lamp).Should().BeTrue();

        document.LightSelection.Should().Be(SceneLightId.None);
        document.SelectedElement.IsValid.Should().BeFalse();
    }

    [Fact]
    public void Undoing_a_light_add_clamps_the_light_selection()
    {
        var document = new EditorDocument("scene");
        document.AddNewPointLight(Float3.Zero);

        document.Undo().Should().BeTrue();

        document.LightSelection.Should().Be(SceneLightId.None);
        document.SelectedElement.IsValid.Should().BeFalse();
    }
}
