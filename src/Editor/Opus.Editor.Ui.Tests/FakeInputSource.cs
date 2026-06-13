using System.Collections.Generic;
using Opus.Engine.Input;

namespace Opus.Editor.Ui.Tests;

/// <summary>A scriptable <see cref="IInputSource"/> for testing the viewport input mapper without SDL.</summary>
internal sealed class FakeInputSource : IInputSource
{
    private readonly HashSet<Key> _keysDown = new();
    private readonly HashSet<Key> _keysPressed = new();
    private readonly HashSet<MouseButton> _buttonsDown = new();
    private readonly HashSet<MouseButton> _buttonsPressed = new();

    public (int X, int Y) MousePosition { get; set; }

    public (int X, int Y) MouseDelta { get; set; }

    public float MouseWheelDelta { get; set; }

    public bool IsKeyDown(Key key) => _keysDown.Contains(key);

    public bool IsKeyPressed(Key key) => _keysPressed.Contains(key);

    public bool IsMouseButtonDown(MouseButton button) => _buttonsDown.Contains(button);

    public bool IsMouseButtonPressed(MouseButton button) => _buttonsPressed.Contains(button);

    public void PressKey(Key key)
    {
        _keysDown.Add(key);
        _keysPressed.Add(key);
    }

    /// <summary>Marks a key as held without flagging a rising edge — for testing modifier chords where the
    /// modifier is down across frames while another key is the one freshly pressed.</summary>
    public void HoldKey(Key key) => _keysDown.Add(key);

    public void PressButton(MouseButton button)
    {
        _buttonsDown.Add(button);
        _buttonsPressed.Add(button);
    }

    public void HoldButton(MouseButton button) => _buttonsDown.Add(button);

    public void ReleaseButton(MouseButton button) => _buttonsDown.Remove(button);

    /// <summary>Clears the rising-edge sets and per-frame deltas, exactly as the real source does after a
    /// frame, so held buttons persist while presses do not.</summary>
    public void EndFrame()
    {
        _keysPressed.Clear();
        _buttonsPressed.Clear();
        MouseDelta = (0, 0);
        MouseWheelDelta = 0f;
    }
}
