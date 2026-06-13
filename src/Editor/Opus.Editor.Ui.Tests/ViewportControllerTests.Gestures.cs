using System;
using System.Numerics;
using FluentAssertions;
using Opus.Editor.Core;
using Opus.Foundation.Geometry;
using Xunit;

namespace Opus.Editor.Ui.Tests;

/// <summary>Handle-free gestures and modal browsers: planar drag and the open-scene / place-model overlays.</summary>
public sealed partial class ViewportControllerTests
{
    [Fact]
    public void A_planar_drag_slides_the_grabbed_node_across_its_ground_plane()
    {
        var document = new EditorDocument("scene");
        var id = document.PlaceNode("tank", "m.glb", EditorTransform.Identity);
        var controller = new ViewportController(document, new FixedBounds(UnitBox));
        var grab = Project(controller, Vector3.Zero);
        var destination = Project(controller, new Vector3(2f, 0f, -1f));

        controller.BeginPlanarDrag(grab.X01, grab.Y01, Viewport.AspectRatio).Should().BeTrue();
        controller.UpdatePlanarDrag(destination.X01, destination.Y01, Viewport.AspectRatio);
        controller.EndPlanarDrag();

        var position = document.Scene.Find(id)!.Transform.Position;
        position.X.Should().BeApproximately(2f, 0.05f);
        position.Z.Should().BeApproximately(-1f, 0.05f);
        position.Y.Should().Be(0f, "a planar drag never changes the height");

        document.Undo().Should().BeTrue();
        document.Scene.Find(id)!.Transform.Position.Should().Be(Float3.Zero, "the whole drag is one undoable edit");
    }

    [Fact]
    public void A_snapped_planar_drag_lands_on_whole_metres()
    {
        var document = new EditorDocument("scene");
        var id = document.PlaceNode("tank", "m.glb", EditorTransform.Identity);
        var controller = new ViewportController(document, new FixedBounds(UnitBox));
        var grab = Project(controller, Vector3.Zero);
        var destination = Project(controller, new Vector3(1.3f, 0f, 2.2f));

        controller.BeginPlanarDrag(grab.X01, grab.Y01, Viewport.AspectRatio).Should().BeTrue();
        controller.UpdatePlanarDrag(destination.X01, destination.Y01, Viewport.AspectRatio, snap: true);
        controller.EndPlanarDrag();

        var position = document.Scene.Find(id)!.Transform.Position;
        position.X.Should().Be(1f);
        position.Z.Should().Be(2f);
    }

    [Fact]
    public void A_planar_drag_moves_a_grabbed_light_at_its_own_height()
    {
        var document = new EditorDocument("scene");
        document.AddNewPointLight(new Float3(0f, 2f, 0f));
        var controller = new ViewportController(document, new NullBounds());
        var grab = Project(controller, new Vector3(0f, 2f, 0f));
        var destination = Project(controller, new Vector3(3f, 2f, 1f));

        controller.BeginPlanarDrag(grab.X01, grab.Y01, Viewport.AspectRatio).Should().BeTrue();
        controller.UpdatePlanarDrag(destination.X01, destination.Y01, Viewport.AspectRatio);
        controller.EndPlanarDrag();

        var position = document.Scene.Lights[0].Position;
        position.X.Should().BeApproximately(3f, 0.05f);
        position.Z.Should().BeApproximately(1f, 0.05f);
        position.Y.Should().Be(2f);
    }

    [Fact]
    public void A_press_off_the_selected_element_does_not_begin_a_planar_drag()
    {
        var document = new EditorDocument("scene");
        document.PlaceNode("tank", "m.glb", EditorTransform.Identity);
        var controller = new ViewportController(document, new FixedBounds(UnitBox));
        var away = Project(controller, new Vector3(6f, 0f, 6f));

        controller.BeginPlanarDrag(away.X01, away.Y01, Viewport.AspectRatio)
            .Should().BeFalse("the press missed the selected element, so the gesture stays an orbit");
    }

    [Fact]
    public void A_grab_without_movement_commits_nothing()
    {
        var document = new EditorDocument("scene");
        document.PlaceNode("tank", "m.glb", EditorTransform.Identity);
        document.MarkSaved();
        var controller = new ViewportController(document, new FixedBounds(UnitBox));
        var grab = Project(controller, Vector3.Zero);

        controller.BeginPlanarDrag(grab.X01, grab.Y01, Viewport.AspectRatio).Should().BeTrue();
        controller.EndPlanarDrag();

        document.IsDirty.Should().BeFalse("no movement, no edit, no history entry");
    }

    [Fact]
    public void The_scene_browser_opens_navigates_and_reports_its_choice()
    {
        var controller = new ViewportController(new EditorDocument("scene"), new NullBounds());
        var files = new[] { @"C:\w\a.scene.json", @"C:\w\b.scene.json", @"C:\w\c.scene.json" };

        controller.OpenSceneBrowser(files);
        controller.IsModalActive.Should().BeTrue();
        controller.SceneBrowserChoice.Should().Be(files[0]);

        controller.MoveSceneBrowserHighlight(1);
        controller.MoveSceneBrowserHighlight(1);
        controller.MoveSceneBrowserHighlight(1);
        controller.SceneBrowserChoice.Should().Be(files[2], "the highlight clamps at the last entry");

        controller.MoveSceneBrowserHighlight(-9);
        controller.SceneBrowserChoice.Should().Be(files[0], "the highlight clamps at the first entry");

        controller.CloseSceneBrowser();
        controller.SceneBrowser.Should().BeNull();
        controller.SceneBrowserChoice.Should().BeNull();
    }

    [Fact]
    public void Opening_the_browser_cancels_a_text_entry_in_progress()
    {
        var document = new EditorDocument("scene");
        document.PlaceNode("tank", null, EditorTransform.Identity);
        var controller = new ViewportController(document, new NullBounds());
        controller.BeginFieldEdit(InspectorField.PositionX);

        controller.OpenSceneBrowser(new[] { @"C:\w\a.scene.json" });

        controller.FieldEdit.Should().BeNull("one modal at a time");
        controller.SceneBrowser.Should().NotBeNull();
    }

    [Fact]
    public void An_empty_browser_has_no_choice()
    {
        var controller = new ViewportController(new EditorDocument("scene"), new NullBounds());

        controller.OpenSceneBrowser(System.Array.Empty<string>());

        controller.SceneBrowserChoice.Should().BeNull();
        controller.MoveSceneBrowserHighlight(1);
        controller.SceneBrowserChoice.Should().BeNull();
    }

    [Fact]
    public void Begin_rename_without_a_selection_renames_the_scene_document()
    {
        var document = new EditorDocument("Harbor");
        var controller = new ViewportController(document, new NullBounds());

        controller.BeginRename().Should().BeTrue();
        controller.Rename!.Value.Buffer.Should().Be("Harbor", "the buffer seeds with the document name");

        controller.RenameAppend('!');
        controller.CommitRename().Should().BeTrue();

        document.Name.Should().Be("Harbor!");
        document.IsDirty.Should().BeTrue();
        document.Undo().Should().BeTrue();
        document.Name.Should().Be("Harbor", "the document rename is one undoable edit");
    }

    [Fact]
    public void Toggling_visibility_hides_then_shows_the_selected_element()
    {
        var document = new EditorDocument("scene");
        var nodeId = document.PlaceNode("tank", null, EditorTransform.Identity);
        var controller = new ViewportController(document, new NullBounds());

        controller.ToggleSelectedHidden().Should().BeTrue();
        document.Scene.Find(nodeId)!.Hidden.Should().BeTrue();
        controller.ToggleSelectedHidden().Should().BeTrue();
        document.Scene.Find(nodeId)!.Hidden.Should().BeFalse();

        var lightId = document.AddNewPointLight(Float3.Zero);
        controller.ToggleSelectedHidden().Should().BeTrue();
        document.Scene.FindLight(lightId)!.Hidden.Should().BeTrue();
        document.Undo().Should().BeTrue();
        document.Scene.FindLight(lightId)!.Hidden.Should().BeFalse("the light toggle is one undoable edit");
    }

    [Fact]
    public void Toggling_visibility_without_a_selection_is_a_no_op()
    {
        var controller = new ViewportController(new EditorDocument("scene"), new NullBounds());

        controller.ToggleSelectedHidden().Should().BeFalse();
    }

    [Fact]
    public void The_model_browser_opens_as_a_place_model_modal()
    {
        var controller = new ViewportController(new EditorDocument("scene"), new NullBounds());

        controller.OpenModelBrowser(new[] { "models/tank.glb", "props/crate.glb" });

        controller.IsModalActive.Should().BeTrue();
        controller.SceneBrowser!.Purpose.Should().Be(BrowserPurpose.PlaceModel);
        controller.SceneBrowserChoice.Should().Be("models/tank.glb");
    }

    [Fact]
    public void Placing_from_the_model_browser_lands_a_named_selected_node_at_the_camera_target()
    {
        var document = new EditorDocument("scene");
        var controller = new ViewportController(document, new NullBounds());
        controller.Camera.Target = new Vector3(3f, 0f, -2f);
        controller.OpenModelBrowser(new[] { "models/tank.glb" });

        var id = controller.PlaceModelFromBrowser();

        id.IsValid.Should().BeTrue();
        var node = document.Scene.Find(id)!;
        node.Name.Should().Be("tank");
        node.AssetRef.Should().Be("models/tank.glb");
        node.Transform.Position.Should().Be(new Float3(3f, 0f, -2f));
        document.Selection.Should().Be(id);
        controller.SceneBrowser.Should().BeNull("placing closes the browser");

        document.Undo().Should().BeTrue();
        document.Scene.Count.Should().Be(0, "the placement is one undoable edit");
    }

    [Fact]
    public void Placing_from_the_scene_browser_or_an_empty_model_browser_is_a_no_op()
    {
        var document = new EditorDocument("scene");
        var controller = new ViewportController(document, new NullBounds());

        controller.OpenSceneBrowser(new[] { @"C:\w\a.scene.json" });
        controller.PlaceModelFromBrowser().IsValid.Should().BeFalse("the open-scene browser places nothing");

        controller.OpenModelBrowser(Array.Empty<string>());
        controller.PlaceModelFromBrowser().IsValid.Should().BeFalse("an empty listing has no choice");
        document.Scene.Count.Should().Be(0);
    }

    [Fact]
    public void Opening_the_model_browser_cancels_a_text_entry_in_progress()
    {
        var document = new EditorDocument("scene");
        document.PlaceNode("tank", null, EditorTransform.Identity);
        var controller = new ViewportController(document, new NullBounds());
        controller.BeginFieldEdit(InspectorField.PositionX);

        controller.OpenModelBrowser(new[] { "models/tank.glb" });

        controller.FieldEdit.Should().BeNull("one modal at a time");
        controller.SceneBrowser.Should().NotBeNull();
    }
}
