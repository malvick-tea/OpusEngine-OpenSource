namespace Opus.Engine.Input;

/// <summary>
/// Per-frame input snapshot. Screens query it during their <c>Update</c> tick — there is
/// no event-based dispatch. Two semantics:
/// <list type="bullet">
/// <item><description><see cref="IsKeyDown"/> / <see cref="IsMouseButtonDown"/> — held this frame.</description></item>
/// <item><description><see cref="IsKeyPressed"/> / <see cref="IsMouseButtonPressed"/> — became down THIS frame (rising edge).</description></item>
/// </list>
/// </summary>
public interface IInputSource
{
    /// <summary>(x, y) in pixels relative to the surface top-left.</summary>
    (int X, int Y) MousePosition { get; }

    /// <summary>Accumulated relative motion for this frame.</summary>
    (int X, int Y) MouseDelta { get; }

    /// <summary>Accumulated vertical wheel delta for this frame.</summary>
    float MouseWheelDelta { get; }

    bool IsKeyDown(Key key);

    bool IsKeyPressed(Key key);

    bool IsMouseButtonDown(MouseButton button);

    bool IsMouseButtonPressed(MouseButton button);
}
