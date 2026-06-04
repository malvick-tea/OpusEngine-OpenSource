using System;
using System.Collections.Generic;

namespace Opus.Engine.Input.Sdl3;

/// <summary>
/// Pure rising-edge / held-state machine that backs <see cref="SdlPolledInputSource"/>.
/// SDL fires <c>KEYDOWN</c> / <c>KEYUP</c> / <c>MOUSEBUTTONDOWN</c> / <c>MOUSEBUTTONUP</c>
/// events; screens query <see cref="IsKeyDown"/> / <see cref="IsKeyPressed"/> per frame.
/// This class converts between the two.
///
/// <para>Lifecycle per frame:</para>
/// <list type="number">
/// <item><description>Host drains SDL events; each one calls <see cref="NoteKeyDown"/> /
///   <see cref="NoteKeyUp"/> / mouse counterparts.</description></item>
/// <item><description>Screens query the four <c>Is*</c> methods during Update.</description></item>
/// <item><description>Host calls <see cref="EndFrame"/> — clears every rising-edge flag
///   so next frame's pressed-set starts empty.</description></item>
/// </list>
///
/// <para>Pure-CPU and event-bus-agnostic — exists as its own class so the wire-from-SDL
/// adapter (<see cref="SdlPolledInputSource"/>) is thin and the rising-edge semantics
/// stay independently unit-testable.</para>
/// </summary>
internal sealed class PolledInputState
{
    private readonly HashSet<Key> _keysDown = new();
    private readonly HashSet<Key> _keysPressedThisFrame = new();
    private readonly HashSet<MouseButton> _mouseDown = new();
    private readonly HashSet<MouseButton> _mousePressedThisFrame = new();
    private int _mouseDeltaX;
    private int _mouseDeltaY;
    private float _mouseWheelDelta;

    public void NoteKeyDown(Key key)
    {
        if (key == Key.None)
        {
            return;
        }

        // SDL emits autorepeated KEYDOWNs; only flag a rising edge on the first transition.
        if (_keysDown.Add(key))
        {
            _keysPressedThisFrame.Add(key);
        }
    }

    public void NoteKeyUp(Key key) => _keysDown.Remove(key);

    public void NoteMouseDown(MouseButton button)
    {
        if (_mouseDown.Add(button))
        {
            _mousePressedThisFrame.Add(button);
        }
    }

    public void NoteMouseUp(MouseButton button) => _mouseDown.Remove(button);

    public void NoteMouseWheel(float delta)
    {
        if (float.IsFinite(delta))
        {
            _mouseWheelDelta += delta;
        }
    }

    public void NoteMouseMoved(int deltaX, int deltaY)
    {
        _mouseDeltaX += deltaX;
        _mouseDeltaY += deltaY;
    }

    public (int X, int Y) MouseDelta => (_mouseDeltaX, _mouseDeltaY);

    public float MouseWheelDelta => _mouseWheelDelta;

    public bool IsKeyDown(Key key) => _keysDown.Contains(key);

    public bool IsKeyPressed(Key key) => _keysPressedThisFrame.Contains(key);

    public bool IsMouseButtonDown(MouseButton button) => _mouseDown.Contains(button);

    public bool IsMouseButtonPressed(MouseButton button) => _mousePressedThisFrame.Contains(button);

    /// <summary>Clears the rising-edge sets. Held-state sets persist across frames — they
    /// are owned by key/mouse-up events, not the frame boundary.</summary>
    public void EndFrame()
    {
        _keysPressedThisFrame.Clear();
        _mousePressedThisFrame.Clear();
        _mouseDeltaX = 0;
        _mouseDeltaY = 0;
        _mouseWheelDelta = 0f;
    }
}
