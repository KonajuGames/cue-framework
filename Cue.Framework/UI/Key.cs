namespace Cue.Framework.UI;

/// <summary>
/// The keys the UI acts on; anything else arrives as <see cref="Other"/>.
/// </summary>
public enum Key
{
    Other,
    Tab,
    Enter,
    Escape,
    Space,
    Backspace,
    Delete,
    Left,
    Right,
    Up,
    Down,
    Home,
    End,
    PageUp,
    PageDown,
    A,
    B,
    C,
    D,
    S,
    V,
    W,
    X,
    Y,
    Z,

    // The rest of the letters (for menus' Alt+letter and type-to-find) and F10, after the first ones so those keep
    // their numbers.
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
    T,
    U,
    F10,
}
