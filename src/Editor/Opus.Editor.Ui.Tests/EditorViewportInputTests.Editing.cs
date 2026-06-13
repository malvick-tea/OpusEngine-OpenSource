using System.Numerics;
using FluentAssertions;
using Opus.Editor.Core;
using Opus.Engine.Input;
using Opus.Foundation.Geometry;
using Xunit;

namespace Opus.Editor.Ui.Tests;

/// <summary>Save-as, gizmo / planar drag, and the editing chords (save / delete / undo / redo / duplicate / copy / paste).</summary>
public sealed partial class EditorViewportInputTests
{
    [Fact]
    public void Ctrl_shift_S_opens_save_as_instead_of_saving_in_place()
    {
        var controller = EmptyController();
        var mapper = new EditorViewportInput();
        var input = new FakeInputSource();
        input.HoldKey(Key.LeftControl);
        input.HoldKey(Key.LeftShift);
        input.PressKey(Key.S);

        var result = mapper.Apply(input, controller, Viewport);

        result.SaveRequested.Should().BeFalse("one keystroke must not both save and start a save-as");
        controller.SaveAs.Should().NotBeNull();
    }

    [Fact]
    public void Plain_ctrl_S_still_saves_in_place()
    {
        var controller = EmptyController();
        var input = new FakeInputSource();
        input.HoldKey(Key.LeftControl);
        input.PressKey(Key.S);

        var result = new EditorViewportInput().Apply(input, controller, Viewport);

        result.SaveRequested.Should().BeTrue();
        controller.SaveAs.Should().BeNull();
    }

    [Fact]
    public void Save_as_typing_then_enter_reports_the_name_to_the_app_layer()
    {
        var controller = new ViewportController(new EditorDocument("Harbor"), new NullBounds());
        var mapper = new EditorViewportInput();
        controller.BeginSaveAs();

        var typing = new FakeInputSource();
        typing.PressKey(Key.Hyphen);
        mapper.Apply(typing, controller, Viewport);
        typing.EndFrame();
        typing.PressKey(Key.D2);
        mapper.Apply(typing, controller, Viewport);

        var confirming = new FakeInputSource();
        confirming.PressKey(Key.Enter);
        var result = mapper.Apply(confirming, controller, Viewport);

        result.SaveAsName.Should().Be("Harbor-2");
        controller.SaveAs.Should().BeNull();
    }

    [Fact]
    public void Escape_cancels_the_save_as_without_quitting()
    {
        var controller = EmptyController();
        var mapper = new EditorViewportInput();
        controller.BeginSaveAs();
        var input = new FakeInputSource();
        input.PressKey(Key.Escape);

        var result = mapper.Apply(input, controller, Viewport);

        result.QuitRequested.Should().BeFalse("Esc inside a modal never quits the window");
        result.SaveAsName.Should().BeNull();
        controller.SaveAs.Should().BeNull();
    }

    [Fact]
    public void Dragging_a_gizmo_axis_moves_the_node_instead_of_orbiting()
    {
        var document = new EditorDocument("Harbor");
        var id = document.PlaceNode("alpha", "models/tank.glb", EditorTransform.Identity);
        var controller = new ViewportController(document, new FixedBounds(UnitBox));
        var mapper = new EditorViewportInput();

        float length = TranslateGizmo.HandleLength(controller.Camera.Distance);
        var viewProjection = controller.Camera.ViewMatrix * controller.Camera.ProjectionMatrix(Viewport.AspectRatio);
        WorldScreenProjector.TryProject(Vector3.Zero, viewProjection, Viewport.Width, Viewport.Height, out var originPx);
        WorldScreenProjector.TryProject(new Vector3(length, 0f, 0f), viewProjection, Viewport.Width, Viewport.Height, out var tipPx);
        var grab = (originPx + tipPx) * 0.5f;
        float yaw = controller.Camera.YawDegrees;

        var input = new FakeInputSource { MousePosition = ((int)grab.X, (int)grab.Y) };
        input.PressButton(MouseButton.Left);
        mapper.Apply(input, controller, Viewport);
        input.EndFrame();
        input.MousePosition = ((int)tipPx.X, (int)tipPx.Y);
        mapper.Apply(input, controller, Viewport);

        document.Scene.Find(id)!.Transform.Position.X.Should().BeGreaterThan(0.1f);
        controller.Camera.YawDegrees.Should().Be(yaw, "a gizmo drag must not orbit the camera");
    }

    [Fact]
    public void Holding_control_while_dragging_a_gizmo_snaps_the_move_to_the_grid()
    {
        var document = new EditorDocument("Harbor");
        var id = document.PlaceNode("alpha", "models/tank.glb", EditorTransform.Identity);
        var controller = new ViewportController(document, new FixedBounds(UnitBox));
        var mapper = new EditorViewportInput();

        float length = TranslateGizmo.HandleLength(controller.Camera.Distance);
        var viewProjection = controller.Camera.ViewMatrix * controller.Camera.ProjectionMatrix(Viewport.AspectRatio);
        WorldScreenProjector.TryProject(new Vector3(length, 0f, 0f), viewProjection, Viewport.Width, Viewport.Height, out var tipPx);
        WorldScreenProjector.TryProject(new Vector3(2f * length, 0f, 0f), viewProjection, Viewport.Width, Viewport.Height, out var farPx);

        var input = new FakeInputSource { MousePosition = ((int)tipPx.X, (int)tipPx.Y) };
        input.PressButton(MouseButton.Left);
        mapper.Apply(input, controller, Viewport);
        input.EndFrame();
        input.MousePosition = ((int)farPx.X, (int)farPx.Y);
        input.HoldKey(Key.LeftControl);
        mapper.Apply(input, controller, Viewport);

        document.Scene.Find(id)!.Transform.Position.X.Should().Be(2f, "Ctrl snaps the dragged move to a whole metre");
    }

    [Fact]
    public void Dragging_the_selected_node_slides_it_on_the_ground_plane_instead_of_orbiting()
    {
        var document = new EditorDocument("Harbor");
        var id = document.PlaceNode("alpha", "models/tank.glb", EditorTransform.Identity);
        var controller = new ViewportController(document, new FixedBounds(UnitBox));
        var mapper = new EditorViewportInput();
        float yaw = controller.Camera.YawDegrees;

        // Grab the node's body away from the origin so the press cannot land on a gizmo handle: at the
        // default 45-degree yaw the world point (-0.5, 0, 0.5) projects straight screen-left of the
        // origin, clear of the +X (right-down), +Y (up), and +Z (left-down) handle segments.
        var viewProjection = controller.Camera.ViewMatrix * controller.Camera.ProjectionMatrix(Viewport.AspectRatio);
        WorldScreenProjector.TryProject(
            new Vector3(-0.5f, 0f, 0.5f), viewProjection, Viewport.Width, Viewport.Height, out var grabPx);
        WorldScreenProjector.TryProject(
            new Vector3(1.5f, 0f, 0.5f), viewProjection, Viewport.Width, Viewport.Height, out var farPx);

        var input = new FakeInputSource { MousePosition = ((int)grabPx.X, (int)grabPx.Y) };
        input.PressButton(MouseButton.Left);
        mapper.Apply(input, controller, Viewport);
        input.EndFrame();
        input.MousePosition = ((int)farPx.X, (int)farPx.Y);
        mapper.Apply(input, controller, Viewport);
        input.EndFrame();
        input.ReleaseButton(MouseButton.Left);
        mapper.Apply(input, controller, Viewport);

        document.Scene.Find(id)!.Transform.Position.X.Should().BeGreaterThan(1.5f);
        document.Selection.Should().Be(id, "the grabbed element stays selected after the drop");
        controller.Camera.YawDegrees.Should().Be(yaw, "a body grab must not orbit the camera");
    }

    [Fact]
    public void Pressing_F_frames_the_selected_node()
    {
        var document = new EditorDocument("Harbor");
        document.PlaceNode("alpha", "models/tank.glb", EditorTransform.Identity with { Position = new Float3(5f, 0f, 0f) });
        var controller = new ViewportController(document, new FixedBounds(UnitBox));
        var input = new FakeInputSource();
        input.PressKey(Key.F);

        new EditorViewportInput().Apply(input, controller, Viewport);

        controller.Camera.Target.Should().Be(new Vector3(5f, 0f, 0f));
    }

    [Fact]
    public void Escape_requests_quit()
    {
        var controller = EmptyController();
        var input = new FakeInputSource();
        input.PressKey(Key.Escape);

        var result = new EditorViewportInput().Apply(input, controller, Viewport);

        result.QuitRequested.Should().BeTrue();
    }

    [Fact]
    public void Pressing_F2_requests_a_screenshot()
    {
        var controller = EmptyController();
        var input = new FakeInputSource();
        input.PressKey(Key.F2);

        var result = new EditorViewportInput().Apply(input, controller, Viewport);

        result.ScreenshotRequested.Should().BeTrue();
        result.QuitRequested.Should().BeFalse();
    }

    [Fact]
    public void Ctrl_plus_S_requests_a_save()
    {
        var controller = EmptyController();
        var input = new FakeInputSource();
        input.HoldKey(Key.LeftControl);
        input.PressKey(Key.S);

        var result = new EditorViewportInput().Apply(input, controller, Viewport);

        result.SaveRequested.Should().BeTrue();
    }

    [Fact]
    public void S_without_a_control_modifier_does_not_request_a_save()
    {
        var controller = EmptyController();
        var input = new FakeInputSource();
        input.PressKey(Key.S);

        var result = new EditorViewportInput().Apply(input, controller, Viewport);

        result.SaveRequested.Should().BeFalse("a bare S is not the save chord");
    }

    [Fact]
    public void Pressing_Delete_removes_the_selected_node()
    {
        var document = new EditorDocument("Harbor");
        document.PlaceNode("alpha", "models/tank.glb", EditorTransform.Identity);
        var controller = new ViewportController(document, new FixedBounds(UnitBox));
        var input = new FakeInputSource();
        input.PressKey(Key.Delete);

        new EditorViewportInput().Apply(input, controller, Viewport);

        document.Scene.Count.Should().Be(0);
    }

    [Fact]
    public void Ctrl_plus_Z_undoes_the_last_edit()
    {
        var document = new EditorDocument("Harbor");
        document.PlaceNode("alpha", "models/tank.glb", EditorTransform.Identity);
        var controller = new ViewportController(document, new FixedBounds(UnitBox));
        var input = new FakeInputSource();
        input.HoldKey(Key.LeftControl);
        input.PressKey(Key.Z);

        new EditorViewportInput().Apply(input, controller, Viewport);

        document.Scene.Count.Should().Be(0, "Ctrl+Z undid the placement");
    }

    [Fact]
    public void Ctrl_plus_Y_redoes_the_undone_edit()
    {
        var document = new EditorDocument("Harbor");
        document.PlaceNode("alpha", "models/tank.glb", EditorTransform.Identity);
        document.Undo();
        var controller = new ViewportController(document, new FixedBounds(UnitBox));
        var input = new FakeInputSource();
        input.HoldKey(Key.LeftControl);
        input.PressKey(Key.Y);

        new EditorViewportInput().Apply(input, controller, Viewport);

        document.Scene.Count.Should().Be(1, "Ctrl+Y reapplied the placement");
    }

    [Fact]
    public void E_switches_the_gizmo_to_scale_and_W_back_to_move()
    {
        var controller = EmptyController();
        var mapper = new EditorViewportInput();

        var toScale = new FakeInputSource();
        toScale.PressKey(Key.E);
        mapper.Apply(toScale, controller, Viewport);
        controller.GizmoMode.Should().Be(GizmoMode.Scale);

        var toMove = new FakeInputSource();
        toMove.PressKey(Key.W);
        mapper.Apply(toMove, controller, Viewport);
        controller.GizmoMode.Should().Be(GizmoMode.Translate);
    }

    [Fact]
    public void Ctrl_plus_D_duplicates_the_selected_node()
    {
        var document = new EditorDocument("Harbor");
        document.PlaceNode("alpha", "models/tank.glb", EditorTransform.Identity);
        var controller = new ViewportController(document, new FixedBounds(UnitBox));
        var input = new FakeInputSource();
        input.HoldKey(Key.LeftControl);
        input.PressKey(Key.D);

        new EditorViewportInput().Apply(input, controller, Viewport);

        document.Scene.Count.Should().Be(2, "Ctrl+D cloned the selected node");
        document.Scene.Find(document.Selection)!.Name.Should().Be("alpha copy");
    }

    [Fact]
    public void Ctrl_plus_C_then_Ctrl_plus_V_pastes_a_copy_of_the_selection()
    {
        var document = new EditorDocument("Harbor");
        document.PlaceNode("alpha", "models/tank.glb", EditorTransform.Identity);
        var controller = new ViewportController(document, new FixedBounds(UnitBox));
        var mapper = new EditorViewportInput();
        var input = new FakeInputSource();
        input.HoldKey(Key.LeftControl);
        input.PressKey(Key.C);
        mapper.Apply(input, controller, Viewport);
        input.EndFrame();

        input.PressKey(Key.V);
        mapper.Apply(input, controller, Viewport);

        document.Scene.Count.Should().Be(2, "Ctrl+V stamped the copied node");
        document.Scene.Find(document.Selection)!.Name.Should().Be("alpha");
    }

    [Fact]
    public void Ctrl_plus_V_does_not_fire_the_bare_visibility_toggle()
    {
        var document = new EditorDocument("Harbor");
        var id = document.PlaceNode("alpha", "models/tank.glb", EditorTransform.Identity);
        var controller = new ViewportController(document, new FixedBounds(UnitBox));
        var input = new FakeInputSource();
        input.HoldKey(Key.LeftControl);
        input.PressKey(Key.V);

        new EditorViewportInput().Apply(input, controller, Viewport);

        document.Scene.Find(id)!.Hidden.Should().BeFalse("Ctrl+V is the paste chord, not the V hide key");
        document.Scene.Count.Should().Be(1, "an empty clipboard pastes nothing");
    }
}
