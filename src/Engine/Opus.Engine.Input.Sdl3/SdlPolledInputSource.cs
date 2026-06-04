using System;
using Opus.Engine.Pal.Sdl3;

namespace Opus.Engine.Input.Sdl3;

/// <summary>
/// <see cref="IInputSource"/> implementation wired to <see cref="SdlWindowService"/>'s
/// event surface. Subscribes once at construction; the constructor takes the window so
/// the subscription lifetime is tied to the adapter.
///
/// <para>Frame contract: the host must call <see cref="EndFrame"/> at the end of each
/// frame's draw cycle (after <c>ScreenStack.Update</c> consumes the input) so the
/// rising-edge sets reset for the next frame. <c>SdlWindowService.PollEvents</c> at the
/// frame boundary drains the queue and updates the held-state sets via the event
/// handlers below.</para>
///
/// <para>Mouse position is polled directly from SDL via
/// <see cref="SdlWindowService.GetMousePosition"/> — SDL's <c>MOUSEMOTION</c> event
/// carries only relative deltas, and accumulating those would race the SDL warp-cursor
/// path that some screens may use later.</para>
/// </summary>
public sealed class SdlPolledInputSource : IInputSource, IMouseModeService, IDisposable
{
    private readonly SdlWindowService _window;
    private readonly PolledInputState _state = new();
    private bool _disposed;
    private bool _isRelativeMouseMode;

    public SdlPolledInputSource(SdlWindowService window)
    {
        ArgumentNullException.ThrowIfNull(window);
        _window = window;
        _window.KeyPressed += OnKeyPressed;
        _window.KeyReleased += OnKeyReleased;
        _window.MouseMoved += OnMouseMoved;
        _window.MouseButtonPressed += OnMouseButtonPressed;
        _window.MouseButtonReleased += OnMouseButtonReleased;
        _window.MouseWheelScrolled += OnMouseWheelScrolled;
    }

    public (int X, int Y) MousePosition => _window.GetMousePosition();

    public (int X, int Y) MouseDelta => _state.MouseDelta;

    public float MouseWheelDelta => _state.MouseWheelDelta;

    public bool IsRelativeMouseMode => _isRelativeMouseMode;

    public bool IsKeyDown(Key key) => _state.IsKeyDown(key);

    public bool IsKeyPressed(Key key) => _state.IsKeyPressed(key);

    public bool IsMouseButtonDown(MouseButton button) => _state.IsMouseButtonDown(button);

    public bool IsMouseButtonPressed(MouseButton button) => _state.IsMouseButtonPressed(button);

    public void SetRelativeMouseMode(bool enabled)
    {
        _window.SetRelativeMouseMode(enabled);
        _isRelativeMouseMode = enabled;
    }

    /// <summary>Clears the rising-edge sets — host calls this after the screen stack has
    /// consumed the frame's input. Held-state survives the call.</summary>
    public void EndFrame() => _state.EndFrame();

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (_isRelativeMouseMode)
        {
            SetRelativeMouseMode(false);
        }

        _window.KeyPressed -= OnKeyPressed;
        _window.KeyReleased -= OnKeyReleased;
        _window.MouseMoved -= OnMouseMoved;
        _window.MouseButtonPressed -= OnMouseButtonPressed;
        _window.MouseButtonReleased -= OnMouseButtonReleased;
        _window.MouseWheelScrolled -= OnMouseWheelScrolled;
        _disposed = true;
    }

    private void OnKeyPressed(Key key) => _state.NoteKeyDown(key);

    private void OnKeyReleased(Key key) => _state.NoteKeyUp(key);

    private void OnMouseMoved(int deltaX, int deltaY) => _state.NoteMouseMoved(deltaX, deltaY);

    private void OnMouseButtonPressed(MouseButton button) => _state.NoteMouseDown(button);

    private void OnMouseButtonReleased(MouseButton button) => _state.NoteMouseUp(button);

    private void OnMouseWheelScrolled(float delta) => _state.NoteMouseWheel(delta);
}
