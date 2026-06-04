using Opus.Engine.Input;

namespace Opus.Engine.Pal.Sdl3;

/// <summary>Maps the byte-encoded button index SDL ships in
/// <c>SDL_MouseButtonEvent.button</c> to the engine-side <see cref="MouseButton"/> enum.
/// Returns <c>null</c> for buttons the engine does not represent (X1 / X2 / additional
/// gaming-mouse keys) — those events are dropped at dispatch.</summary>
internal static class SdlMouseButtonMap
{
    // Constants mirror SDL_BUTTON_LEFT / SDL_BUTTON_MIDDLE / SDL_BUTTON_RIGHT (1 / 2 / 3).
    private const byte SdlButtonLeft = 1;
    private const byte SdlButtonMiddle = 2;
    private const byte SdlButtonRight = 3;

    public static MouseButton? From(byte sdlButton) => sdlButton switch
    {
        SdlButtonLeft => MouseButton.Left,
        SdlButtonMiddle => MouseButton.Middle,
        SdlButtonRight => MouseButton.Right,
        _ => null,
    };
}
