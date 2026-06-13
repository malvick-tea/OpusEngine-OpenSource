using System.Linq;
using System.Numerics;
using FluentAssertions;
using Opus.Editor.Core;
using Opus.Engine.Input;
using Opus.Foundation.Geometry;
using Xunit;

namespace Opus.Editor.Ui.Tests;

public sealed class EditorToolbarInputTests
{
    private static readonly EditorPanelRect Toolbar = new(0, 0, 1280, 32);
    private static readonly Aabb UnitBox = new(new Vector3(-1f), new Vector3(1f));

    private static ViewportController WithOnePlacedNode()
    {
        var document = new EditorDocument("scene");
        document.PlaceNode("alpha", "m.glb", EditorTransform.Identity);
        return new ViewportController(document, new FixedBounds(UnitBox));
    }

    private static FakeInputSource ClickAt(EditorPanelRect rect) => Clicked(
        rect.X + (rect.Width / 2), rect.Y + (rect.Height / 2));

    private static FakeInputSource Clicked(int x, int y)
    {
        var input = new FakeInputSource { MousePosition = (x, y) };
        input.PressButton(MouseButton.Left);
        return input;
    }

    private static EditorPanelRect ButtonRect(ViewportController controller, EditorToolbarAction action) =>
        EditorToolbarButtons.Build(Toolbar, EditorChromeStrings.English, controller.ToolbarState)
            .Single(b => b.Action == action).Rect;

    [Fact]
    public void Clicking_undo_undoes_the_last_edit()
    {
        var controller = WithOnePlacedNode();
        var input = ClickAt(ButtonRect(controller, EditorToolbarAction.Undo));

        var action = EditorToolbarInput.Apply(input, controller, Toolbar, EditorChromeStrings.English);

        action.Should().Be(EditorToolbarAction.Undo);
        controller.ToolbarState.HasSelection.Should().BeFalse("the placement was undone");
    }

    [Fact]
    public void Clicking_save_reports_the_save_action_without_changing_the_document()
    {
        var controller = WithOnePlacedNode();
        var input = ClickAt(ButtonRect(controller, EditorToolbarAction.Save));

        var action = EditorToolbarInput.Apply(input, controller, Toolbar, EditorChromeStrings.English);

        action.Should().Be(EditorToolbarAction.Save);
        controller.ToolbarState.HasSelection.Should().BeTrue("save performs no document mutation here");
        controller.ToolbarState.IsDirty.Should().BeTrue("the pure UI layer does not clear dirty; the app save does");
    }

    [Fact]
    public void Clicking_delete_removes_the_selected_node()
    {
        var controller = WithOnePlacedNode();
        var input = ClickAt(ButtonRect(controller, EditorToolbarAction.Delete));

        EditorToolbarInput.Apply(input, controller, Toolbar, EditorChromeStrings.English)
            .Should().Be(EditorToolbarAction.Delete);
        controller.ToolbarState.HasSelection.Should().BeFalse();
    }

    [Fact]
    public void A_click_outside_the_toolbar_performs_no_action()
    {
        var controller = WithOnePlacedNode();
        var input = Clicked(400, 400);

        EditorToolbarInput.Apply(input, controller, Toolbar, EditorChromeStrings.English)
            .Should().Be(EditorToolbarAction.None);
        controller.ToolbarState.HasSelection.Should().BeTrue("nothing was deleted or undone");
    }

    [Fact]
    public void Clicking_add_node_places_a_new_selected_node()
    {
        var document = new EditorDocument("scene");
        var controller = new ViewportController(document, new NullBounds());
        var input = ClickAt(ButtonRect(controller, EditorToolbarAction.AddNode));

        EditorToolbarInput.Apply(input, controller, Toolbar, EditorChromeStrings.English)
            .Should().Be(EditorToolbarAction.AddNode);
        document.Scene.Count.Should().Be(1);
        document.Selection.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Clicking_add_light_adds_a_point_light()
    {
        var document = new EditorDocument("scene");
        var controller = new ViewportController(document, new NullBounds());
        var input = ClickAt(ButtonRect(controller, EditorToolbarAction.AddLight));

        EditorToolbarInput.Apply(input, controller, Toolbar, EditorChromeStrings.English)
            .Should().Be(EditorToolbarAction.AddLight);
        document.Scene.LightCount.Should().Be(1);
    }

    [Theory]
    [InlineData(EditorToolbarAction.AddCube, "primitive:cube")]
    [InlineData(EditorToolbarAction.AddSphere, "primitive:sphere")]
    [InlineData(EditorToolbarAction.AddCylinder, "primitive:cylinder")]
    [InlineData(EditorToolbarAction.AddPlane, "primitive:plane")]
    [InlineData(EditorToolbarAction.AddCone, "primitive:cone")]
    public void Clicking_a_primitive_button_places_that_shape_selected(
        EditorToolbarAction action, string expectedRef)
    {
        var document = new EditorDocument("scene");
        var controller = new ViewportController(document, new NullBounds());
        var input = ClickAt(ButtonRect(controller, action));

        EditorToolbarInput.Apply(input, controller, Toolbar, EditorChromeStrings.English).Should().Be(action);

        document.Scene.Count.Should().Be(1);
        document.Scene.Nodes[0].AssetRef.Should().Be(expectedRef);
        document.Selection.Should().Be(document.Scene.Nodes[0].Id);
    }
}
