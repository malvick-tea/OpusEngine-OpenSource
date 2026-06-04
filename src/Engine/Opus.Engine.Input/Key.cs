namespace Opus.Engine.Input;

/// <summary>
/// Stable key codes used across the game. Mirrors the subset of Raylib KeyboardKey we
/// need today; new entries land here when a feature requires them. Mapping to platform
/// codes happens in <c>Engine.Input.{platform}</c>.
/// </summary>
public enum Key
{
    None,

    Escape,
    Enter,
    Space,
    Tab,
    Backspace,

    Left,
    Right,
    Up,
    Down,

    A,
    B,
    C,
    D,
    E,
    F,
    G,
    H,
    I,
    J,
    K,
    L,
    M,
    N,
    O,
    P,
    Q,
    R,
    S,
    T,
    U,
    V,
    W,
    X,
    Y,
    Z,

    F1,
    F2,
    F3,
    F4,
    F5,
    F6,
    F7,
    F8,
    F9,
    F10,
    F11,
    F12,

    // Top-row number keys, NOT the numeric keypad. Required by text-input fields that
    // accept digits — multiplayer host/port entry, future numeric settings rows. The
    // numpad block stays unmapped until a feature actually needs it.
    D0,
    D1,
    D2,
    D3,
    D4,
    D5,
    D6,
    D7,
    D8,
    D9,

    // Punctuation needed by hostname / IPv4 text input: dots separate IPv4 octets and DNS
    // labels; hyphens are legal mid-label hostname characters. Slash, colon and the rest
    // stay unmapped until a use lands.
    Period,
    Hyphen,
}
