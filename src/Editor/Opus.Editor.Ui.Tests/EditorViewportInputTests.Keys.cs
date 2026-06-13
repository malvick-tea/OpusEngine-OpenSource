using System.Numerics;
using FluentAssertions;
using Opus.Editor.Core;
using Opus.Engine.Input;
using Opus.Foundation.Geometry;
using Xunit;

namespace Opus.Editor.Ui.Tests;

/// <summary>Bare-key shortcuts: arrow nudges, gizmo mode, overlays, rename begin, creation, new scene, and camera home.</summary>
public sealed partial class EditorViewportInputTests
{
    [Fact]
    public void Arrow_keys_nudge_the_selection_one_metre_along_the_grid()
    {
        var document = new EditorDocument("Harbor");
        var id = document.PlaceNode("alpha", "models/tank.glb", EditorTransform.Identity);
        var controller = new ViewportController(document, new FixedBounds(UnitBox));
        var mapper = new EditorViewportInput();

        var right = new FakeInputSource();
        right.PressKey(Key.Right);
        mapper.Apply(right, controller, Viewport);
        document.Scene.Find(id)!.Transform.Position.Should().Be(new Float3(1f, 0f, 0f));

        var up = new FakeInputSource();
        up.PressKey(Key.Up);
        mapper.Apply(up, controller, Viewport);
        document.Scene.Find(id)!.Transform.Position.Should().Be(new Float3(1f, 0f, -1f));
    }

    [Fact]
    public void Arrow_keys_move_the_browser_highlight_not_the_selection()
    {
        var document = new EditorDocument("Harbor");
        var id = document.PlaceNode("alpha", "models/tank.glb", EditorTransform.Identity);
        var controller = new ViewportController(document, new FixedBounds(UnitBox));
        controller.OpenSceneBrowser(new[] { "a.scene.json", "b.scene.json" });
        var input = new FakeInputSource();
        input.PressKey(Key.Down);

        new EditorViewportInput().Apply(input, controller, Viewport);

        controller.SceneBrowser!.Highlight.Should().Be(1, "the modal browser owns the arrows");
        document.Scene.Find(id)!.Transform.Position.Should().Be(Float3.Zero, "no nudge fired through the modal");
    }

    [Fact]
    public void R_switches_the_gizmo_to_rotate()
    {
        var controller = EmptyController();
        var input = new FakeInputSource();
        input.PressKey(Key.R);

        new EditorViewportInput().Apply(input, controller, Viewport);

        controller.GizmoMode.Should().Be(GizmoMode.Rotate);
    }

    [Fact]
    public void F3_toggles_the_stats_overlay()
    {
        var controller = EmptyController();
        var mapper = new EditorViewportInput();

        var open = new FakeInputSource();
        open.PressKey(Key.F3);
        mapper.Apply(open, controller, Viewport);
        controller.StatsVisible.Should().BeTrue();

        var close = new FakeInputSource();
        close.PressKey(Key.F3);
        mapper.Apply(close, controller, Viewport);
        controller.StatsVisible.Should().BeFalse();
    }

    [Fact]
    public void F1_toggles_the_help_overlay()
    {
        var controller = EmptyController();
        var mapper = new EditorViewportInput();

        var open = new FakeInputSource();
        open.PressKey(Key.F1);
        mapper.Apply(open, controller, Viewport);
        controller.HelpVisible.Should().BeTrue();

        var close = new FakeInputSource();
        close.PressKey(Key.F1);
        mapper.Apply(close, controller, Viewport);
        controller.HelpVisible.Should().BeFalse();
    }

    [Fact]
    public void F1_while_renaming_types_nothing_and_keeps_the_help_hidden()
    {
        var document = new EditorDocument("Harbor");
        document.PlaceNode("alpha", null, EditorTransform.Identity);
        var controller = new ViewportController(document, new NullBounds());
        controller.BeginRename();
        var input = new FakeInputSource();
        input.PressKey(Key.F1);

        new EditorViewportInput().Apply(input, controller, Viewport);

        controller.HelpVisible.Should().BeFalse("the rename is modal");
        controller.Rename!.Value.Buffer.Should().Be("alpha");
    }

    [Fact]
    public void Ctrl_plus_R_begins_a_rename_without_switching_the_gizmo_mode()
    {
        var document = new EditorDocument("Harbor");
        document.PlaceNode("alpha", null, EditorTransform.Identity);
        var controller = new ViewportController(document, new NullBounds());
        var input = new FakeInputSource();
        input.HoldKey(Key.LeftControl);
        input.PressKey(Key.R);

        new EditorViewportInput().Apply(input, controller, Viewport);

        controller.Rename.Should().NotBeNull();
        controller.Rename!.Value.Buffer.Should().Be("alpha");
        controller.GizmoMode.Should().Be(GizmoMode.Translate, "Ctrl+R must not also act as the rotate key");
    }

    [Fact]
    public void While_renaming_typed_keys_edit_the_buffer_instead_of_firing_shortcuts()
    {
        var document = new EditorDocument("Harbor");
        document.PlaceNode("alpha", null, EditorTransform.Identity);
        var controller = new ViewportController(document, new NullBounds());
        controller.BeginRename();
        var mapper = new EditorViewportInput();
        var input = new FakeInputSource();
        input.PressKey(Key.W);
        input.PressKey(Key.Delete);

        mapper.Apply(input, controller, Viewport);

        controller.Rename!.Value.Buffer.Should().Be("alphaw", "W typed into the buffer");
        controller.GizmoMode.Should().Be(GizmoMode.Translate, "the gizmo-mode shortcut is suppressed");
        document.Scene.Count.Should().Be(1, "Delete did not remove the node mid-rename");
    }

    [Fact]
    public void While_renaming_escape_cancels_the_rename_instead_of_quitting()
    {
        var document = new EditorDocument("Harbor");
        document.PlaceNode("alpha", null, EditorTransform.Identity);
        var controller = new ViewportController(document, new NullBounds());
        controller.BeginRename();
        var input = new FakeInputSource();
        input.PressKey(Key.Escape);

        var result = new EditorViewportInput().Apply(input, controller, Viewport);

        result.QuitRequested.Should().BeFalse("Esc ends the rename, not the window");
        controller.Rename.Should().BeNull();
    }

    [Fact]
    public void While_renaming_enter_commits_the_new_name()
    {
        var document = new EditorDocument("Harbor");
        var id = document.PlaceNode("alpha", null, EditorTransform.Identity);
        var controller = new ViewportController(document, new NullBounds());
        controller.BeginRename();
        var mapper = new EditorViewportInput();

        var typing = new FakeInputSource();
        typing.PressKey(Key.Backspace);
        mapper.Apply(typing, controller, Viewport);

        var committing = new FakeInputSource();
        committing.PressKey(Key.Enter);
        mapper.Apply(committing, controller, Viewport);

        controller.Rename.Should().BeNull();
        document.Scene.Find(id)!.Name.Should().Be("alph");
    }

    [Fact]
    public void Pressing_A_adds_a_selected_node_at_the_camera_target()
    {
        var document = new EditorDocument("Harbor");
        var controller = new ViewportController(document, new NullBounds());
        controller.Camera.Target = new Vector3(3f, 0f, -2f);
        var input = new FakeInputSource();
        input.PressKey(Key.A);

        new EditorViewportInput().Apply(input, controller, Viewport);

        document.Scene.Count.Should().Be(1);
        document.Selection.IsValid.Should().BeTrue();
        document.Scene.Find(document.Selection)!.Transform.Position.Should().Be(new Float3(3f, 0f, -2f));
    }

    [Fact]
    public void Ctrl_plus_N_reports_a_new_scene_request()
    {
        var controller = new ViewportController(new EditorDocument("Harbor"), new NullBounds());
        var input = new FakeInputSource();
        input.HoldKey(Key.LeftControl);
        input.PressKey(Key.N);

        var result = new EditorViewportInput().Apply(input, controller, Viewport);

        result.NewSceneRequested.Should().BeTrue();
        result.SaveRequested.Should().BeFalse();
    }

    [Fact]
    public void Pressing_H_resets_the_camera_to_the_home_view()
    {
        var controller = new ViewportController(new EditorDocument("Harbor"), new NullBounds());
        controller.Camera.Orbit(90f, 30f);
        controller.Camera.Target = new Vector3(5f, 1f, -4f);
        var input = new FakeInputSource();
        input.PressKey(Key.H);

        new EditorViewportInput().Apply(input, controller, Viewport);

        controller.Camera.Target.Should().Be(Vector3.Zero);
        controller.Camera.YawDegrees.Should().Be(OrbitCamera.DefaultYawDegrees);
    }
}
