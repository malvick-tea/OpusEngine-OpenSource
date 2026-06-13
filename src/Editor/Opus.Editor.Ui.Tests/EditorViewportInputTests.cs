using System.Numerics;
using FluentAssertions;
using Opus.Editor.Core;
using Opus.Engine.Input;
using Opus.Foundation.Geometry;
using Xunit;

namespace Opus.Editor.Ui.Tests;

public sealed partial class EditorViewportInputTests
{
    private static readonly EditorPanelRect Viewport = new(0, 0, 800, 600);
    private static readonly Aabb UnitBox = new(new Vector3(-1f), new Vector3(1f));

    private static ViewportController EmptyController() =>
        new(new EditorDocument("Harbor"), new NullBounds());

    [Fact]
    public void Wheel_up_over_the_viewport_dollies_the_camera_in()
    {
        var controller = EmptyController();
        float before = controller.Camera.Distance;
        var input = new FakeInputSource { MouseWheelDelta = 1f, MousePosition = (400, 300) };

        new EditorViewportInput().Apply(input, controller, Viewport);

        controller.Camera.Distance.Should().BeLessThan(before);
    }

    [Fact]
    public void Wheel_outside_the_viewport_does_not_zoom()
    {
        var controller = EmptyController();
        float before = controller.Camera.Distance;
        var input = new FakeInputSource { MouseWheelDelta = 1f, MousePosition = (5000, 5000) };

        new EditorViewportInput().Apply(input, controller, Viewport);

        controller.Camera.Distance.Should().Be(before, "the wheel only zooms over the 3D viewport");
    }

    [Fact]
    public void Middle_drag_pans_the_camera_target()
    {
        var controller = EmptyController();
        var before = controller.Camera.Target;
        var input = new FakeInputSource { MousePosition = (400, 300), MouseDelta = (20, 10) };
        input.HoldButton(MouseButton.Middle);

        new EditorViewportInput().Apply(input, controller, Viewport);

        controller.Camera.Target.Should().NotBe(before);
    }

    [Fact]
    public void Left_drag_inside_the_viewport_orbits_the_camera()
    {
        var controller = EmptyController();
        float yaw = controller.Camera.YawDegrees;
        var mapper = new EditorViewportInput();
        var input = new FakeInputSource { MousePosition = (400, 300) };

        input.PressButton(MouseButton.Left);
        mapper.Apply(input, controller, Viewport);
        input.EndFrame();
        input.MouseDelta = (30, 0);
        mapper.Apply(input, controller, Viewport);

        controller.Camera.YawDegrees.Should().NotBe(yaw);
    }

    [Fact]
    public void A_left_click_without_dragging_selects_the_node_under_the_cursor()
    {
        var document = new EditorDocument("Harbor");
        document.PlaceNode("alpha", "models/tank.glb", EditorTransform.Identity);
        document.Select(SceneNodeId.None);
        var controller = new ViewportController(document, new FixedBounds(UnitBox));
        var mapper = new EditorViewportInput();
        var input = new FakeInputSource { MousePosition = (400, 300) };

        input.PressButton(MouseButton.Left);
        mapper.Apply(input, controller, Viewport);
        input.EndFrame();
        input.ReleaseButton(MouseButton.Left);
        mapper.Apply(input, controller, Viewport);

        document.Selection.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Ctrl_click_adds_the_clicked_element_to_the_selection()
    {
        var document = new EditorDocument("Harbor");
        var away = document.PlaceNode(
            "away", "models/tank.glb", EditorTransform.Identity with { Position = new Float3(50f, 0f, 0f) });
        var centre = document.PlaceNode("centre", "models/tank.glb", EditorTransform.Identity);
        document.Select(away);
        var controller = new ViewportController(document, new FixedBounds(UnitBox));
        var mapper = new EditorViewportInput();
        var input = new FakeInputSource { MousePosition = (400, 300) };
        input.HoldKey(Key.LeftControl);

        input.PressButton(MouseButton.Left);
        mapper.Apply(input, controller, Viewport);
        input.EndFrame();
        input.ReleaseButton(MouseButton.Left);
        mapper.Apply(input, controller, Viewport);

        document.SelectedElements.Should().Equal(
            new[] { SceneElementRef.Node(away), SceneElementRef.Node(centre) },
            "the Ctrl+click joined the centre node to the selection instead of replacing it");
    }

    [Fact]
    public void Ctrl_click_on_the_selected_element_deselects_it()
    {
        var document = new EditorDocument("Harbor");
        document.PlaceNode("alpha", "models/tank.glb", EditorTransform.Identity);
        var controller = new ViewportController(document, new FixedBounds(UnitBox));
        var mapper = new EditorViewportInput();

        // Press the node's body clear of the gizmo handles (same world point the planar-drag test grabs),
        // so the gesture is a toggle click rather than an axis grab.
        var viewProjection = controller.Camera.ViewMatrix * controller.Camera.ProjectionMatrix(Viewport.AspectRatio);
        WorldScreenProjector.TryProject(
            new Vector3(-0.5f, 0f, 0.5f), viewProjection, Viewport.Width, Viewport.Height, out var bodyPx);
        var input = new FakeInputSource { MousePosition = ((int)bodyPx.X, (int)bodyPx.Y) };
        input.HoldKey(Key.LeftControl);

        input.PressButton(MouseButton.Left);
        mapper.Apply(input, controller, Viewport);
        input.EndFrame();
        input.ReleaseButton(MouseButton.Left);
        mapper.Apply(input, controller, Viewport);

        document.SelectedElements.Should().BeEmpty(
            "with Ctrl held the press is a toggle click, not a planar grab, so it removes the member");
    }

    [Fact]
    public void Ctrl_click_on_empty_space_keeps_the_selection()
    {
        var document = new EditorDocument("Harbor");
        var id = document.PlaceNode("alpha", "models/tank.glb", EditorTransform.Identity);
        var controller = new ViewportController(document, new FixedBounds(UnitBox));
        var mapper = new EditorViewportInput();
        var input = new FakeInputSource { MousePosition = (20, 20) };
        input.HoldKey(Key.LeftControl);

        input.PressButton(MouseButton.Left);
        mapper.Apply(input, controller, Viewport);
        input.EndFrame();
        input.ReleaseButton(MouseButton.Left);
        mapper.Apply(input, controller, Viewport);

        document.SelectedElements.Should().Equal(
            new[] { SceneElementRef.Node(id) }, "a sloppy Ctrl+click never throws away the built-up set");
    }

    [Fact]
    public void Shift_drag_box_selects_instead_of_orbiting()
    {
        var document = new EditorDocument("Harbor");
        var node = document.PlaceNode("alpha", "models/tank.glb", EditorTransform.Identity);
        document.Select(SceneNodeId.None);
        var controller = new ViewportController(document, new FixedBounds(UnitBox));
        var mapper = new EditorViewportInput();
        float yaw = controller.Camera.YawDegrees;
        var input = new FakeInputSource { MousePosition = (100, 100) };
        input.HoldKey(Key.LeftShift);

        input.PressButton(MouseButton.Left);
        mapper.Apply(input, controller, Viewport);
        input.EndFrame();
        input.MousePosition = (700, 500);
        input.MouseDelta = (600, 400);
        mapper.Apply(input, controller, Viewport);
        controller.Marquee.Should().NotBeNull("the Shift drag is a live marquee");
        input.EndFrame();
        input.ReleaseButton(MouseButton.Left);
        mapper.Apply(input, controller, Viewport);

        controller.Camera.YawDegrees.Should().Be(yaw, "a Shift drag never orbits");
        controller.Marquee.Should().BeNull();
        document.SelectedElements.Should().Equal(
            new[] { SceneElementRef.Node(node) }, "the box covered the node at the viewport centre");
    }

    [Fact]
    public void Ctrl_shift_drag_adds_the_boxed_elements_to_the_selection()
    {
        var document = new EditorDocument("Harbor");
        var away = document.PlaceNode(
            "away", "models/tank.glb", EditorTransform.Identity with { Position = new Float3(100f, 0f, 0f) });
        var centre = document.PlaceNode("centre", "models/tank.glb", EditorTransform.Identity);
        document.Select(away);
        var controller = new ViewportController(document, new FixedBounds(UnitBox));
        var mapper = new EditorViewportInput();
        var input = new FakeInputSource { MousePosition = (100, 100) };
        input.HoldKey(Key.LeftShift);
        input.HoldKey(Key.LeftControl);

        input.PressButton(MouseButton.Left);
        mapper.Apply(input, controller, Viewport);
        input.EndFrame();
        input.MousePosition = (700, 500);
        input.MouseDelta = (600, 400);
        mapper.Apply(input, controller, Viewport);
        input.EndFrame();
        input.ReleaseButton(MouseButton.Left);
        mapper.Apply(input, controller, Viewport);

        document.SelectedElements.Should().Equal(
            new[] { SceneElementRef.Node(away), SceneElementRef.Node(centre) },
            "the additive box keeps the off-screen member and adds the boxed one");
    }

    [Fact]
    public void Shift_click_without_dragging_toggles_membership_like_ctrl_click()
    {
        var document = new EditorDocument("Harbor");
        var away = document.PlaceNode(
            "away", "models/tank.glb", EditorTransform.Identity with { Position = new Float3(100f, 0f, 0f) });
        var centre = document.PlaceNode("centre", "models/tank.glb", EditorTransform.Identity);
        document.Select(away);
        var controller = new ViewportController(document, new FixedBounds(UnitBox));
        var mapper = new EditorViewportInput();
        var input = new FakeInputSource { MousePosition = (400, 300) };
        input.HoldKey(Key.LeftShift);

        input.PressButton(MouseButton.Left);
        mapper.Apply(input, controller, Viewport);
        input.EndFrame();
        input.ReleaseButton(MouseButton.Left);
        mapper.Apply(input, controller, Viewport);

        document.SelectedElements.Should().Equal(
            new[] { SceneElementRef.Node(away), SceneElementRef.Node(centre) },
            "a Shift+click that never travelled is an additive toggle, not a box");
    }

    [Fact]
    public void Ctrl_A_selects_every_visible_element_without_adding_a_node()
    {
        var document = new EditorDocument("Harbor");
        var node = document.PlaceNode("alpha", "models/tank.glb", EditorTransform.Identity);
        var lamp = document.AddNewPointLight(Float3.Zero);
        document.Select(SceneNodeId.None);
        var controller = new ViewportController(document, new FixedBounds(UnitBox));
        var input = new FakeInputSource { MousePosition = (400, 300) };
        input.HoldKey(Key.LeftControl);
        input.PressKey(Key.A);

        new EditorViewportInput().Apply(input, controller, Viewport);

        document.Scene.Count.Should().Be(1, "the chord must never fire the bare A add-node twin");
        document.SelectedElements.Should().Equal(
            SceneElementRef.Node(node), SceneElementRef.Light(lamp));
    }
}
