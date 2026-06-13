using System.Linq;
using System.Numerics;
using FluentAssertions;
using Opus.Editor.Core;
using Opus.Engine.Input;
using Opus.Foundation.Geometry;
using Xunit;

namespace Opus.Editor.Ui.Tests;

public sealed class EditorOutlinerInputTests
{
    private static readonly EditorPanelRect Outliner = new(960, 32, 320, 300);
    private static readonly Aabb UnitBox = new(new Vector3(-1f), new Vector3(1f));

    [Fact]
    public void Clicking_a_row_selects_that_node()
    {
        var document = new EditorDocument("scene");
        var alpha = document.PlaceNode("alpha", "m.glb", EditorTransform.Identity);
        document.PlaceNode("bravo", "m.glb", EditorTransform.Identity);
        var controller = new ViewportController(document, new FixedBounds(UnitBox));
        var alphaRow = EditorOutliner.Build(Outliner, controller.Scene, controller.SelectedElement)
            .First(r => r.Element == SceneElementRef.Node(alpha)).Rect;
        var input = new FakeInputSource { MousePosition = (alphaRow.X + 5, alphaRow.Y + 5) };
        input.PressButton(MouseButton.Left);

        EditorOutlinerInput.Apply(input, controller, Outliner).Should().Be(SceneElementRef.Node(alpha));
        controller.Selection.Should().Be(alpha);
    }

    [Fact]
    public void Clicking_a_light_row_selects_that_light()
    {
        var document = new EditorDocument("scene");
        document.PlaceNode("alpha", "m.glb", EditorTransform.Identity);
        var lamp = document.AddNewPointLight(Float3.Zero);
        document.Select(SceneNodeId.None);
        var controller = new ViewportController(document, new FixedBounds(UnitBox));
        var lampRow = EditorOutliner.Build(Outliner, controller.Scene, controller.SelectedElement)
            .First(r => r.Element == SceneElementRef.Light(lamp)).Rect;
        var input = new FakeInputSource { MousePosition = (lampRow.X + 5, lampRow.Y + 5) };
        input.PressButton(MouseButton.Left);

        EditorOutlinerInput.Apply(input, controller, Outliner).Should().Be(SceneElementRef.Light(lamp));
        document.LightSelection.Should().Be(lamp);
        document.Selection.Should().Be(SceneNodeId.None, "selecting a light clears the node selection");
    }

    [Fact]
    public void Ctrl_clicking_a_second_row_adds_it_to_the_selection()
    {
        var document = new EditorDocument("scene");
        var alpha = document.PlaceNode("alpha", "m.glb", EditorTransform.Identity);
        var bravo = document.PlaceNode("bravo", "m.glb", EditorTransform.Identity);
        document.Select(alpha);
        var controller = new ViewportController(document, new FixedBounds(UnitBox));
        var bravoRow = EditorOutliner.Build(Outliner, controller.Scene, controller.SelectedElement)
            .First(r => r.Element == SceneElementRef.Node(bravo)).Rect;
        var input = new FakeInputSource { MousePosition = (bravoRow.X + 5, bravoRow.Y + 5) };
        input.HoldKey(Key.LeftControl);
        input.PressButton(MouseButton.Left);

        EditorOutlinerInput.Apply(input, controller, Outliner);

        document.SelectedElements.Should().Equal(SceneElementRef.Node(alpha), SceneElementRef.Node(bravo));
    }

    [Fact]
    public void Ctrl_clicking_a_selected_row_removes_it_from_the_selection()
    {
        var document = new EditorDocument("scene");
        var alpha = document.PlaceNode("alpha", "m.glb", EditorTransform.Identity);
        var controller = new ViewportController(document, new FixedBounds(UnitBox));
        var alphaRow = EditorOutliner.Build(Outliner, controller.Scene, controller.SelectedElement)
            .First(r => r.Element == SceneElementRef.Node(alpha)).Rect;
        var input = new FakeInputSource { MousePosition = (alphaRow.X + 5, alphaRow.Y + 5) };
        input.HoldKey(Key.LeftControl);
        input.PressButton(MouseButton.Left);

        EditorOutlinerInput.Apply(input, controller, Outliner);

        document.SelectedElements.Should().BeEmpty();
    }

    [Fact]
    public void Shift_clicking_a_row_selects_the_range_from_the_primary()
    {
        var document = new EditorDocument("scene");
        var alpha = document.PlaceNode("alpha", "m.glb", EditorTransform.Identity);
        var bravo = document.PlaceNode("bravo", "m.glb", EditorTransform.Identity);
        var charlie = document.PlaceNode("charlie", "m.glb", EditorTransform.Identity);
        document.Select(alpha);
        var controller = new ViewportController(document, new FixedBounds(UnitBox));
        var charlieRow = EditorOutliner.Build(Outliner, controller.Scene, controller.SelectedElement)
            .First(r => r.Element == SceneElementRef.Node(charlie)).Rect;
        var input = new FakeInputSource { MousePosition = (charlieRow.X + 5, charlieRow.Y + 5) };
        input.HoldKey(Key.LeftShift);
        input.PressButton(MouseButton.Left);

        EditorOutlinerInput.Apply(input, controller, Outliner);

        document.SelectedElements.Should().Equal(
            SceneElementRef.Node(alpha), SceneElementRef.Node(bravo), SceneElementRef.Node(charlie));
        document.SelectedElement.Should().Be(
            SceneElementRef.Node(charlie), "the clicked end of the range is the primary");
    }

    [Fact]
    public void A_click_outside_the_outliner_selects_nothing()
    {
        var document = new EditorDocument("scene");
        var alpha = document.PlaceNode("alpha", "m.glb", EditorTransform.Identity);
        var controller = new ViewportController(document, new FixedBounds(UnitBox));
        var input = new FakeInputSource { MousePosition = (100, 400) };
        input.PressButton(MouseButton.Left);

        EditorOutlinerInput.Apply(input, controller, Outliner).Should().Be(SceneElementRef.None);
        controller.Selection.Should().Be(alpha, "the selection is unchanged");
    }

    [Fact]
    public void Wheel_down_over_the_outliner_scrolls_the_list_toward_the_bottom()
    {
        var controller = ControllerWith(30);
        var input = new FakeInputSource { MousePosition = (Outliner.X + 5, Outliner.Y + 50), MouseWheelDelta = -1f };

        EditorOutlinerInput.Apply(input, controller, Outliner);

        controller.OutlinerScroll.Should().Be(1);
    }

    [Fact]
    public void Wheel_up_at_the_top_is_clamped_to_zero()
    {
        var controller = ControllerWith(30);
        var input = new FakeInputSource { MousePosition = (Outliner.X + 5, Outliner.Y + 50), MouseWheelDelta = 1f };

        EditorOutlinerInput.Apply(input, controller, Outliner);

        controller.OutlinerScroll.Should().Be(0, "the list is already at the top");
    }

    [Fact]
    public void Wheel_outside_the_outliner_does_not_scroll_it()
    {
        var controller = ControllerWith(30);
        var input = new FakeInputSource { MousePosition = (100, 400), MouseWheelDelta = -1f };

        EditorOutlinerInput.Apply(input, controller, Outliner);

        controller.OutlinerScroll.Should().Be(0);
    }

    private static ViewportController ControllerWith(int nodeCount)
    {
        var document = new EditorDocument("scene");
        for (int i = 0; i < nodeCount; i++)
        {
            document.PlaceNode($"n{i}", "m.glb", EditorTransform.Identity);
        }

        return new ViewportController(document, new FixedBounds(UnitBox));
    }
}
