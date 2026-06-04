using Opus.Engine.Input;
using Silk.NET.SDL;

namespace Opus.Engine.Pal.Sdl3;

/// <summary>Maps an SDL <see cref="KeyCode"/> to the engine-side <see cref="Key"/> enum.
/// Returns <see cref="Key.None"/> for keys not represented in the engine enum — those are
/// dropped silently so the input pipeline sees only known codes.</summary>
internal static class SdlKeyMap
{
    public static Key From(KeyCode sdlKeyCode) => sdlKeyCode switch
    {
        KeyCode.KEscape => Key.Escape,
        KeyCode.KReturn => Key.Enter,
        KeyCode.KSpace => Key.Space,
        KeyCode.KTab => Key.Tab,
        KeyCode.KBackspace => Key.Backspace,

        KeyCode.KLeft => Key.Left,
        KeyCode.KRight => Key.Right,
        KeyCode.KUp => Key.Up,
        KeyCode.KDown => Key.Down,

        KeyCode.KA => Key.A,
        KeyCode.KB => Key.B,
        KeyCode.KC => Key.C,
        KeyCode.KD => Key.D,
        KeyCode.KE => Key.E,
        KeyCode.KF => Key.F,
        KeyCode.KG => Key.G,
        KeyCode.KH => Key.H,
        KeyCode.KI => Key.I,
        KeyCode.KJ => Key.J,
        KeyCode.KK => Key.K,
        KeyCode.KL => Key.L,
        KeyCode.KM => Key.M,
        KeyCode.KN => Key.N,
        KeyCode.KO => Key.O,
        KeyCode.KP => Key.P,
        KeyCode.KQ => Key.Q,
        KeyCode.KR => Key.R,
        KeyCode.KS => Key.S,
        KeyCode.KT => Key.T,
        KeyCode.KU => Key.U,
        KeyCode.KV => Key.V,
        KeyCode.KW => Key.W,
        KeyCode.KX => Key.X,
        KeyCode.KY => Key.Y,
        KeyCode.KZ => Key.Z,

        KeyCode.KF1 => Key.F1,
        KeyCode.KF2 => Key.F2,
        KeyCode.KF3 => Key.F3,
        KeyCode.KF4 => Key.F4,
        KeyCode.KF5 => Key.F5,
        KeyCode.KF6 => Key.F6,
        KeyCode.KF7 => Key.F7,
        KeyCode.KF8 => Key.F8,
        KeyCode.KF9 => Key.F9,
        KeyCode.KF10 => Key.F10,
        KeyCode.KF11 => Key.F11,
        KeyCode.KF12 => Key.F12,

        KeyCode.K0 => Key.D0,
        KeyCode.K1 => Key.D1,
        KeyCode.K2 => Key.D2,
        KeyCode.K3 => Key.D3,
        KeyCode.K4 => Key.D4,
        KeyCode.K5 => Key.D5,
        KeyCode.K6 => Key.D6,
        KeyCode.K7 => Key.D7,
        KeyCode.K8 => Key.D8,
        KeyCode.K9 => Key.D9,
        KeyCode.KPeriod => Key.Period,
        KeyCode.KMinus => Key.Hyphen,

        _ => Key.None,
    };
}
