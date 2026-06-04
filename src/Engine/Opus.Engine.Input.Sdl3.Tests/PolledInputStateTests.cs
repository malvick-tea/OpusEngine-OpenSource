using FluentAssertions;
using Xunit;

namespace Opus.Engine.Input.Sdl3.Tests;

/// <summary>
/// Specification for <see cref="PolledInputState"/>: held vs. rising-edge semantics +
/// EndFrame clearing. The SDL-wire (<see cref="SdlPolledInputSource"/>) is just an
/// event-handler shim around this class; everything interesting lives here.
/// </summary>
public sealed class PolledInputStateTests
{
    [Fact]
    public void KeyDown_flips_held_and_rising_edge()
    {
        var state = new PolledInputState();

        state.NoteKeyDown(Key.W);

        state.IsKeyDown(Key.W).Should().BeTrue();
        state.IsKeyPressed(Key.W).Should().BeTrue();
    }

    [Fact]
    public void KeyDown_autorepeat_does_not_re_raise_rising_edge()
    {
        var state = new PolledInputState();
        state.NoteKeyDown(Key.W);
        state.EndFrame();

        // SDL autorepeats fire KEYDOWN every ~30ms while a key is held; the rising-edge
        // flag must stay cleared until a KEYUP / KEYDOWN cycle re-arms it.
        state.NoteKeyDown(Key.W);

        state.IsKeyDown(Key.W).Should().BeTrue();
        state.IsKeyPressed(Key.W).Should().BeFalse();
    }

    [Fact]
    public void KeyUp_clears_held_but_leaves_history_alone()
    {
        var state = new PolledInputState();
        state.NoteKeyDown(Key.A);
        state.NoteKeyUp(Key.A);

        state.IsKeyDown(Key.A).Should().BeFalse();
        state.IsKeyPressed(Key.A).Should().BeTrue(); // rising-edge stays until EndFrame
    }

    [Fact]
    public void EndFrame_clears_rising_edge_but_preserves_held()
    {
        var state = new PolledInputState();
        state.NoteKeyDown(Key.Space);

        state.EndFrame();

        state.IsKeyDown(Key.Space).Should().BeTrue();
        state.IsKeyPressed(Key.Space).Should().BeFalse();
    }

    [Fact]
    public void KeyDown_after_keyup_rearms_rising_edge()
    {
        var state = new PolledInputState();
        state.NoteKeyDown(Key.Escape);
        state.EndFrame();
        state.NoteKeyUp(Key.Escape);
        state.NoteKeyDown(Key.Escape);

        state.IsKeyDown(Key.Escape).Should().BeTrue();
        state.IsKeyPressed(Key.Escape).Should().BeTrue();
    }

    [Fact]
    public void None_key_is_ignored()
    {
        var state = new PolledInputState();
        state.NoteKeyDown(Key.None);

        state.IsKeyDown(Key.None).Should().BeFalse();
        state.IsKeyPressed(Key.None).Should().BeFalse();
    }

    [Fact]
    public void Mouse_button_down_flags_held_and_rising_edge()
    {
        var state = new PolledInputState();
        state.NoteMouseDown(MouseButton.Left);

        state.IsMouseButtonDown(MouseButton.Left).Should().BeTrue();
        state.IsMouseButtonPressed(MouseButton.Left).Should().BeTrue();
    }

    [Fact]
    public void Mouse_button_up_clears_held()
    {
        var state = new PolledInputState();
        state.NoteMouseDown(MouseButton.Right);
        state.NoteMouseUp(MouseButton.Right);

        state.IsMouseButtonDown(MouseButton.Right).Should().BeFalse();
    }

    [Fact]
    public void EndFrame_clears_mouse_rising_edge()
    {
        var state = new PolledInputState();
        state.NoteMouseDown(MouseButton.Middle);

        state.EndFrame();

        state.IsMouseButtonDown(MouseButton.Middle).Should().BeTrue();
        state.IsMouseButtonPressed(MouseButton.Middle).Should().BeFalse();
    }

    [Fact]
    public void Mouse_wheel_events_accumulate_for_the_current_frame()
    {
        var state = new PolledInputState();

        state.NoteMouseWheel(1f);
        state.NoteMouseWheel(-0.25f);

        state.MouseWheelDelta.Should().Be(0.75f);
    }

    [Fact]
    public void EndFrame_clears_mouse_wheel_delta()
    {
        var state = new PolledInputState();
        state.NoteMouseWheel(2f);

        state.EndFrame();

        state.MouseWheelDelta.Should().Be(0f);
    }

    [Fact]
    public void Non_finite_mouse_wheel_delta_is_ignored()
    {
        var state = new PolledInputState();

        state.NoteMouseWheel(float.NaN);
        state.NoteMouseWheel(float.PositiveInfinity);

        state.MouseWheelDelta.Should().Be(0f);
    }

    [Fact]
    public void Mouse_motion_accumulates_until_end_of_frame()
    {
        var state = new PolledInputState();

        state.NoteMouseMoved(7, -3);
        state.NoteMouseMoved(-2, 8);

        state.MouseDelta.Should().Be((5, 5));

        state.EndFrame();

        state.MouseDelta.Should().Be((0, 0));
    }
}
