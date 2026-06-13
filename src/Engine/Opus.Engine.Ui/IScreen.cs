using Opus.Engine.Input;
using Opus.Foundation;

namespace Opus.Engine.Ui;

/// <summary>
/// One stage of UI: splash, main menu, garage, settings, etc. Screens are stacked by
/// a host-owned screen stack; only the top one renders + receives
/// updates. Lifecycle: <see cref="OnEnter"/> → <see cref="Update"/>* → <see cref="Render"/>*
/// → <see cref="OnExit"/>.
/// </summary>
public interface IScreen
{
    /// <summary>Called once when the screen becomes top-of-stack. Screens may allocate here.</summary>
    void OnEnter();

    /// <summary>Called once before the screen is removed / covered. Screens release per-screen state.</summary>
    void OnExit();

    /// <summary>
    /// Per-frame logic. <paramref name="time"/> ticks at the host frame rate, not Sim's
    /// fixed step. <paramref name="input"/> is a per-frame snapshot — screens query the
    /// state they need; there is no event-based dispatch.
    /// </summary>
    void Update(GameTime time, IInputSource input);

    /// <summary>Per-frame draw. Surface is already cleared by ScreenStack with the screen's clear colour.</summary>
    void Render(IDrawSurface surface);
}
