using System.Linq;
using FluentAssertions;
using Opus.Editor.Core;
using Opus.Engine.Input;
using Xunit;

namespace Opus.Editor.Ui.Tests;

public sealed class EditorInspectorInputTests
{
    private static readonly EditorPanelRect Panel = new(960, 32, 320, 300);

    private static ViewportController WithSelectedNode(out EditorDocument document)
    {
        document = new EditorDocument("scene");
        document.PlaceNode("tank", "models/tank.glb", EditorTransform.Identity);
        return new ViewportController(document, new NullBounds());
    }

    private static FakeInputSource ClickAt(EditorPanelRect rect)
    {
        var input = new FakeInputSource { MousePosition = (rect.X + 4, rect.Y + 4) };
        input.PressButton(MouseButton.Left);
        return input;
    }

    private static EditorPanelRect RowRect(ViewportController controller, InspectorField field) =>
        EditorInspector.Build(Panel, controller.Scene, controller.SelectedElement)
            .Single(r => r.Field == field).Rect;

    [Fact]
    public void Clicking_a_numeric_row_begins_a_field_edit()
    {
        var controller = WithSelectedNode(out _);
        var input = ClickAt(RowRect(controller, InspectorField.PositionY));

        EditorInspectorInput.Apply(input, controller, Panel).Should().Be(InspectorField.PositionY);

        controller.FieldEdit.Should().NotBeNull();
        controller.FieldEdit!.Value.Field.Should().Be(InspectorField.PositionY);
        controller.FieldEdit!.Value.Buffer.Should().BeEmpty("the author types the new value directly");
    }

    [Fact]
    public void Clicking_the_name_row_begins_a_rename()
    {
        var controller = WithSelectedNode(out _);
        var input = ClickAt(RowRect(controller, InspectorField.Name));

        EditorInspectorInput.Apply(input, controller, Panel).Should().Be(InspectorField.Name);

        controller.Rename.Should().NotBeNull();
        controller.Rename!.Value.Buffer.Should().Be("tank");
        controller.FieldEdit.Should().BeNull();
    }

    [Fact]
    public void Clicking_a_display_only_row_does_nothing()
    {
        var controller = WithSelectedNode(out _);
        var input = ClickAt(RowRect(controller, InspectorField.Asset));

        EditorInspectorInput.Apply(input, controller, Panel).Should().Be(InspectorField.None);

        controller.IsTextEntryActive.Should().BeFalse();
    }

    [Fact]
    public void Clicking_the_asset_row_of_an_empty_node_gives_it_a_cube_shape()
    {
        var document = new EditorDocument("scene");
        var id = document.PlaceNewNode(EditorTransform.Identity);
        var controller = new ViewportController(document, new NullBounds());
        var input = ClickAt(RowRect(controller, InspectorField.Asset));

        EditorInspectorInput.Apply(input, controller, Panel).Should().Be(InspectorField.Asset);

        document.Scene.Find(id)!.AssetRef.Should().Be("primitive:cube");
        controller.IsTextEntryActive.Should().BeFalse("a shape cycle is immediate, not a typed edit");
    }

    [Fact]
    public void Clicking_the_kind_row_cycles_the_selected_light_kind()
    {
        var document = new EditorDocument("scene");
        document.AddNewPointLight(Float3.Zero);
        var controller = new ViewportController(document, new NullBounds());
        var input = ClickAt(RowRect(controller, InspectorField.Kind));

        EditorInspectorInput.Apply(input, controller, Panel).Should().Be(InspectorField.Kind);

        document.Scene.Lights[0].Kind.Should().Be(SceneLightKind.Spot);
        controller.IsTextEntryActive.Should().BeFalse("a kind cycle is immediate, not a typed edit");
    }

    [Fact]
    public void A_click_outside_the_panel_does_nothing()
    {
        var controller = WithSelectedNode(out _);
        var input = new FakeInputSource { MousePosition = (10, 200) };
        input.PressButton(MouseButton.Left);

        EditorInspectorInput.Apply(input, controller, Panel).Should().Be(InspectorField.None);

        controller.IsTextEntryActive.Should().BeFalse();
    }
}
