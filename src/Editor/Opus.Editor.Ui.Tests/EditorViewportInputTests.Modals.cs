using System.Numerics;
using FluentAssertions;
using Opus.Editor.Core;
using Opus.Engine.Input;
using Opus.Foundation.Geometry;
using Xunit;

namespace Opus.Editor.Ui.Tests;

/// <summary>Modal input: the open-scene and place-model browsers and the inspector field edit.</summary>
public sealed partial class EditorViewportInputTests
{
    [Fact]
    public void Ctrl_plus_O_requests_the_open_browser()
    {
        var controller = EmptyController();
        var input = new FakeInputSource();
        input.HoldKey(Key.LeftControl);
        input.PressKey(Key.O);

        var result = new EditorViewportInput().Apply(input, controller, Viewport);

        result.OpenBrowserRequested.Should().BeTrue();
    }

    [Fact]
    public void The_open_browser_navigates_confirms_and_never_quits()
    {
        var controller = EmptyController();
        controller.OpenSceneBrowser(new[] { @"C:\w\a.scene.json", @"C:\w\b.scene.json" });
        var mapper = new EditorViewportInput();

        var down = new FakeInputSource();
        down.PressKey(Key.Down);
        mapper.Apply(down, controller, Viewport).OpenSceneConfirmed.Should().BeFalse();
        controller.SceneBrowserChoice.Should().EndWith("b.scene.json");

        var enter = new FakeInputSource();
        enter.PressKey(Key.Enter);
        var confirmed = mapper.Apply(enter, controller, Viewport);

        confirmed.OpenSceneConfirmed.Should().BeTrue();
        confirmed.QuitRequested.Should().BeFalse();
        controller.SceneBrowser.Should().NotBeNull("the app layer closes the browser after performing the load");
    }

    [Fact]
    public void Escape_closes_the_open_browser_without_quitting()
    {
        var controller = EmptyController();
        controller.OpenSceneBrowser(new[] { @"C:\w\a.scene.json" });
        var input = new FakeInputSource();
        input.PressKey(Key.Escape);

        var result = new EditorViewportInput().Apply(input, controller, Viewport);

        result.QuitRequested.Should().BeFalse("Esc closes the overlay, not the editor");
        controller.SceneBrowser.Should().BeNull();
    }

    [Fact]
    public void Clicking_a_browser_row_highlights_and_confirms_it()
    {
        var controller = EmptyController();
        controller.OpenSceneBrowser(new[] { @"C:\w\a.scene.json", @"C:\w\b.scene.json" });
        var view = EditorSceneBrowser.Build(Viewport, controller.SceneBrowser!, EditorChromeStrings.English);
        var second = view.Rows[1].Rect;
        var input = new FakeInputSource { MousePosition = (second.X + 5, second.Y + 5) };
        input.PressButton(MouseButton.Left);

        var result = new EditorViewportInput().Apply(input, controller, Viewport);

        result.OpenSceneConfirmed.Should().BeTrue();
        controller.SceneBrowserChoice.Should().EndWith("b.scene.json");
    }

    [Fact]
    public void The_open_browser_suppresses_the_editing_shortcuts()
    {
        var document = new EditorDocument("Harbor");
        var controller = new ViewportController(document, new NullBounds());
        controller.OpenSceneBrowser(new[] { @"C:\w\a.scene.json" });
        var input = new FakeInputSource();
        input.PressKey(Key.A);
        input.PressKey(Key.D1);

        new EditorViewportInput().Apply(input, controller, Viewport);

        document.Scene.Count.Should().Be(0, "the modal browser must not spawn nodes");
    }

    [Fact]
    public void V_toggles_the_selected_element_visibility()
    {
        var document = new EditorDocument("Harbor");
        var id = document.PlaceNode("tank", null, EditorTransform.Identity);
        var controller = new ViewportController(document, new NullBounds());
        var input = new FakeInputSource();
        input.PressKey(Key.V);

        new EditorViewportInput().Apply(input, controller, Viewport);

        document.Scene.Find(id)!.Hidden.Should().BeTrue();
    }

    [Fact]
    public void M_requests_the_model_browser()
    {
        var controller = EmptyController();
        var input = new FakeInputSource();
        input.PressKey(Key.M);

        var result = new EditorViewportInput().Apply(input, controller, Viewport);

        result.ModelBrowserRequested.Should().BeTrue();
    }

    [Fact]
    public void Ctrl_plus_M_does_not_request_the_model_browser()
    {
        var controller = EmptyController();
        var input = new FakeInputSource();
        input.HoldKey(Key.LeftControl);
        input.PressKey(Key.M);

        var result = new EditorViewportInput().Apply(input, controller, Viewport);

        result.ModelBrowserRequested.Should().BeFalse("bare keys never fire alongside a chord");
    }

    [Fact]
    public void Confirming_the_model_browser_places_the_model_on_the_controller()
    {
        var document = new EditorDocument("Harbor");
        var controller = new ViewportController(document, new NullBounds());
        controller.OpenModelBrowser(new[] { "models/tank.glb" });
        var input = new FakeInputSource();
        input.PressKey(Key.Enter);

        var result = new EditorViewportInput().Apply(input, controller, Viewport);

        result.OpenSceneConfirmed.Should().BeFalse("a model placement is not a scene open");
        document.Scene.Count.Should().Be(1, "the placement completes purely, with no app round-trip");
        controller.SceneBrowser.Should().BeNull();
    }

    [Fact]
    public void Clicking_a_model_browser_row_places_that_model()
    {
        var document = new EditorDocument("Harbor");
        var controller = new ViewportController(document, new NullBounds());
        controller.OpenModelBrowser(new[] { "models/tank.glb", "props/crate.glb" });
        var view = EditorSceneBrowser.Build(Viewport, controller.SceneBrowser!, EditorChromeStrings.English);
        var second = view.Rows[1].Rect;
        var input = new FakeInputSource { MousePosition = (second.X + 5, second.Y + 5) };
        input.PressButton(MouseButton.Left);

        new EditorViewportInput().Apply(input, controller, Viewport);

        document.Scene.Nodes[0].AssetRef.Should().Be("props/crate.glb");
    }

    [Fact]
    public void The_wheel_walks_the_browser_highlight()
    {
        var controller = EmptyController();
        controller.OpenSceneBrowser(new[] { @"C:\w\a.scene.json", @"C:\w\b.scene.json", @"C:\w\c.scene.json" });
        var mapper = new EditorViewportInput();

        var down = new FakeInputSource { MouseWheelDelta = -1f };
        mapper.Apply(down, controller, Viewport);
        controller.SceneBrowserChoice.Should().EndWith("b.scene.json");

        var up = new FakeInputSource { MouseWheelDelta = 1f };
        mapper.Apply(up, controller, Viewport);
        controller.SceneBrowserChoice.Should().EndWith("a.scene.json");
    }

    [Fact]
    public void Escape_closes_the_model_browser_without_quitting()
    {
        var controller = EmptyController();
        controller.OpenModelBrowser(new[] { "models/tank.glb" });
        var input = new FakeInputSource();
        input.PressKey(Key.Escape);

        var result = new EditorViewportInput().Apply(input, controller, Viewport);

        result.QuitRequested.Should().BeFalse("Esc closes the overlay, not the editor");
        controller.SceneBrowser.Should().BeNull();
    }

    [Fact]
    public void Field_edit_keys_type_commit_and_do_not_quit()
    {
        var document = new EditorDocument("Harbor");
        var id = document.PlaceNode("tank", null, EditorTransform.Identity);
        var controller = new ViewportController(document, new NullBounds());
        controller.BeginFieldEdit(InspectorField.PositionX).Should().BeTrue();
        var mapper = new EditorViewportInput();

        var typing = new FakeInputSource();
        typing.PressKey(Key.D7);
        mapper.Apply(typing, controller, Viewport);
        controller.FieldEdit!.Value.Buffer.Should().Be("7");

        var committing = new FakeInputSource();
        committing.PressKey(Key.Enter);
        var result = mapper.Apply(committing, controller, Viewport);

        result.QuitRequested.Should().BeFalse();
        controller.FieldEdit.Should().BeNull();
        document.Scene.Find(id)!.Transform.Position.X.Should().Be(7f);
    }

    [Fact]
    public void Escape_cancels_a_field_edit_without_quitting()
    {
        var document = new EditorDocument("Harbor");
        var id = document.PlaceNode("tank", null, EditorTransform.Identity);
        var controller = new ViewportController(document, new NullBounds());
        controller.BeginFieldEdit(InspectorField.ScaleX);
        controller.FieldEditAppend('5');
        var input = new FakeInputSource();
        input.PressKey(Key.Escape);

        var result = new EditorViewportInput().Apply(input, controller, Viewport);

        result.QuitRequested.Should().BeFalse("Esc ends the edit, not the editor");
        controller.FieldEdit.Should().BeNull();
        document.Scene.Find(id)!.Transform.Scale.X.Should().Be(1f);
    }

    [Fact]
    public void A_field_edit_suppresses_the_creation_shortcuts()
    {
        var document = new EditorDocument("Harbor");
        document.PlaceNode("tank", null, EditorTransform.Identity);
        var controller = new ViewportController(document, new NullBounds());
        controller.BeginFieldEdit(InspectorField.PositionY);
        var input = new FakeInputSource();
        input.PressKey(Key.A);
        input.PressKey(Key.D1);

        new EditorViewportInput().Apply(input, controller, Viewport);

        document.Scene.Count.Should().Be(1, "modal typing must not spawn nodes");
        controller.FieldEdit!.Value.Buffer.Should().Be("1", "the digit went into the buffer instead");
    }

    [Theory]
    [InlineData(Key.D1, "primitive:cube")]
    [InlineData(Key.D2, "primitive:sphere")]
    [InlineData(Key.D3, "primitive:cylinder")]
    [InlineData(Key.D4, "primitive:plane")]
    [InlineData(Key.D5, "primitive:cone")]
    public void Number_keys_add_the_matching_primitive_at_the_camera_target(Key key, string expectedRef)
    {
        var document = new EditorDocument("Harbor");
        var controller = new ViewportController(document, new NullBounds());
        controller.Camera.Target = new Vector3(1f, 0f, 2f);
        var input = new FakeInputSource();
        input.PressKey(key);

        new EditorViewportInput().Apply(input, controller, Viewport);

        document.Scene.Count.Should().Be(1);
        var node = document.Scene.Find(document.Selection)!;
        node.AssetRef.Should().Be(expectedRef);
        node.Transform.Position.Should().Be(new Float3(1f, 0f, 2f));
    }

    [Fact]
    public void Ctrl_plus_D_duplicates_the_selected_light()
    {
        var document = new EditorDocument("Harbor");
        document.AddNewPointLight(new Float3(1f, 0f, 0f));
        var controller = new ViewportController(document, new NullBounds());
        var input = new FakeInputSource();
        input.HoldKey(Key.LeftControl);
        input.PressKey(Key.D);

        new EditorViewportInput().Apply(input, controller, Viewport);

        document.Scene.LightCount.Should().Be(2, "Ctrl+D cloned the selected light");
        document.Scene.Lights[1].Name.Should().EndWith(" copy");
        document.LightSelection.Should().Be(document.Scene.Lights[1].Id);
    }

    [Fact]
    public void Pressing_L_adds_a_point_light_at_the_camera_target()
    {
        var document = new EditorDocument("Harbor");
        var controller = new ViewportController(document, new NullBounds());
        controller.Camera.Target = new Vector3(0f, 5f, 1f);
        var input = new FakeInputSource();
        input.PressKey(Key.L);

        new EditorViewportInput().Apply(input, controller, Viewport);

        document.Scene.LightCount.Should().Be(1);
        document.Scene.Lights[0].Position.Should().Be(new Float3(0f, 5f, 1f));
    }

    [Fact]
    public void P_parents_the_selection_under_the_primary()
    {
        var document = new EditorDocument("Harbor");
        var child = document.PlaceNode("box", null, EditorTransform.Identity);
        var parent = document.PlaceNode("group", null, EditorTransform.Identity);
        document.SelectElements(new[] { SceneElementRef.Node(child), SceneElementRef.Node(parent) });
        var controller = new ViewportController(document, new NullBounds());
        var input = new FakeInputSource { MousePosition = (400, 300) };
        input.PressKey(Key.P);

        new EditorViewportInput().Apply(input, controller, Viewport);

        document.Scene.Find(child)!.ParentId.Should().Be(parent, "P parents the non-primary under the primary");
    }

    [Fact]
    public void Shift_P_detaches_the_selection_to_a_root()
    {
        var document = new EditorDocument("Harbor");
        var parent = document.PlaceNode("group", null, EditorTransform.Identity);
        var child = document.PlaceNode("box", null, EditorTransform.Identity);
        document.SetNodeParent(child, parent);
        document.Select(child);
        var controller = new ViewportController(document, new NullBounds());
        var input = new FakeInputSource { MousePosition = (400, 300) };
        input.HoldKey(Key.LeftShift);
        input.PressKey(Key.P);

        new EditorViewportInput().Apply(input, controller, Viewport);

        document.Scene.Find(child)!.ParentId.Should().BeNull("Shift+P detaches the selection to a root");
    }

    [Fact]
    public void Ctrl_G_groups_the_selected_nodes_under_a_new_parent()
    {
        var document = new EditorDocument("Harbor");
        var a = document.PlaceNode("a", null, EditorTransform.Identity);
        var b = document.PlaceNode("b", null, EditorTransform.Identity with { Position = new Float3(4f, 0f, 0f) });
        document.SelectElements(new[] { SceneElementRef.Node(a), SceneElementRef.Node(b) });
        var controller = new ViewportController(document, new NullBounds());
        var input = new FakeInputSource { MousePosition = (400, 300) };
        input.HoldKey(Key.LeftControl);
        input.PressKey(Key.G);

        new EditorViewportInput().Apply(input, controller, Viewport);

        document.Scene.Count.Should().Be(3, "a new group node was created");
        document.Scene.Find(a)!.ParentId.Should().Be(document.SelectedElement.AsNode);
    }

    [Fact]
    public void Ctrl_Shift_G_ungroups_the_selected_group_without_grouping_again()
    {
        var document = new EditorDocument("Harbor");
        var a = document.PlaceNode("a", null, EditorTransform.Identity);
        var b = document.PlaceNode("b", null, EditorTransform.Identity with { Position = new Float3(4f, 0f, 0f) });
        var group = document.GroupNodes(new[] { a, b });
        document.Select(group);
        var controller = new ViewportController(document, new NullBounds());
        var input = new FakeInputSource { MousePosition = (400, 300) };
        input.HoldKey(Key.LeftControl);
        input.HoldKey(Key.LeftShift);
        input.PressKey(Key.G);

        new EditorViewportInput().Apply(input, controller, Viewport);

        document.Scene.Find(group).Should().BeNull("Ctrl+Shift+G dissolves the group");
        document.Scene.Count.Should().Be(2, "the two children remain, no new group is created");
        document.Scene.Find(a)!.ParentId.Should().BeNull();
    }
}
