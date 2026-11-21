using Brutal.Numerics;

namespace Cue.Framework.UI;

/// <summary>
/// The looks the Cue apps can take, chosen in each app's options.
/// </summary>
public enum ThemeKind
{
    /// <summary>Near-black panels and soft text: the usual, for dark rooms.</summary>
    Dark,

    /// <summary>Pale panels and dark text, for daylight.</summary>
    Light,

    /// <summary>Black panels, white text and bright edges, for bright rooms and tired eyes.</summary>
    HighContrast,
}

/// <summary>
/// One look's colours: what <see cref="Theme"/> hands out while it's in use.
/// </summary>
public sealed record ThemePalette
{
    public required float4 Background { get; init; }
    public required float4 TitleBar { get; init; }
    public required float4 Panel { get; init; }
    public required float4 PanelRaised { get; init; }
    public required float4 PanelBorder { get; init; }
    public required float4 Well { get; init; }
    public required float4 Text { get; init; }
    public required float4 Muted { get; init; }
    public required float4 Faint { get; init; }
    public required float4 Accent { get; init; }
    public required float4 Focus { get; init; }
    public required float4 Connected { get; init; }
    public required float4 Warning { get; init; }
    public required float4 Control { get; init; }
    public required float4 ControlHover { get; init; }
    public required float4 ControlPressed { get; init; }
    public required float4 Selection { get; init; }
    public required float4 Field { get; init; }
    public required float4 Placeholder { get; init; }
    public required float4 Handle { get; init; }

    /// <summary>
    /// What faint tints over the panels are made of (a hovered row, a slider's track, a divider): white on a dark
    /// look, black on a light one.
    /// </summary>
    public required float4 Ink { get; init; }

    /// <summary>
    /// How much stronger than asked those tints come out, so they show as well on this look's panels.
    /// </summary>
    public float InkStrength { get; init; } = 1;

    /// <summary>
    /// How much of the screen behind a dialog is dimmed, as a share of what's asked: less on a light look, where
    /// black over pale panels darkens more.
    /// </summary>
    public float ScrimStrength { get; init; } = 1;

    /// <summary>
    /// How strong a list's every other row is tinted, to follow a row across; before <see cref="InkStrength"/>.
    /// </summary>
    public float AlternateRowTint { get; init; } = 0.03f;

    /// <summary>
    /// Whether it's light, for colours an app keeps that need their own darker or lighter shade.
    /// </summary>
    public bool IsLight { get; init; }
}

/// <summary>
/// The colours the Cue apps share, so their screens hang together, in whichever look's chosen (see
/// <see cref="Use"/>): panels, muted text for labels, orange for the app's own accents, and the fills of buttons,
/// fields and selections. Each app keeps the colours only it uses (a deck's, a timeline's) in its own
/// <c>AppTheme</c>. Elements take their colours as they're made, so choose the look before making any.
/// </summary>
public static class Theme
{
    public static readonly ThemePalette Dark = new()
    {
        Background = Colors.Hex(0x121318),
        TitleBar = Colors.Hex(0x0B0C0F),
        Panel = Colors.Hex(0x1B1C22),
        PanelRaised = Colors.Hex(0x23242C),
        PanelBorder = Colors.Hex(0x2E3039),
        Well = Colors.Hex(0x0E0F12),
        Text = Colors.Hex(0xE4E5EC),
        // A shade brighter than it was, so labels on buttons read at 4.5 to 1.
        Muted = Colors.Hex(0x8F919F),
        Faint = Colors.Hex(0x5A5C68),
        Accent = Colors.Hex(0xEB7828),
        Focus = Colors.Hex(0x7AB8FF),
        Connected = Colors.Hex(0x3CC25A),
        Warning = Colors.Hex(0xFF4D3A),
        Control = Colors.Hex(0x2A2A35),
        ControlHover = Colors.Hex(0x3A3A48),
        ControlPressed = Colors.Hex(0x1E1E26),
        Selection = Colors.Hex(0x3D6FB8),
        Field = Colors.Hex(0x0E0E13),
        Placeholder = Colors.Hex(0x6A6A78),
        Handle = Colors.Hex(0xE6E6EE),
        Ink = Colors.Hex(0xFFFFFF),
        // Faint: just enough to follow a row, without banding the list.
        AlternateRowTint = 0.015f,
    };

    public static readonly ThemePalette Light = new()
    {
        Background = Colors.Hex(0xE9EAEF),
        TitleBar = Colors.Hex(0xD9DAE1),
        Panel = Colors.Hex(0xF6F6F9),
        PanelRaised = Colors.Hex(0xFFFFFF),
        PanelBorder = Colors.Hex(0xC4C6CF),
        Well = Colors.Hex(0xDFE0E7),
        Text = Colors.Hex(0x17181D),
        Muted = Colors.Hex(0x575A68),
        Faint = Colors.Hex(0x8F92A0),
        Accent = Colors.Hex(0xD45F0E),
        Focus = Colors.Hex(0x1F6BD1),
        Connected = Colors.Hex(0x1F9446),
        Warning = Colors.Hex(0xD3301D),
        Control = Colors.Hex(0xE0E2EA),
        ControlHover = Colors.Hex(0xD0D3DD),
        ControlPressed = Colors.Hex(0xC2C5D0),
        Selection = Colors.Hex(0x4A7FD0),
        Field = Colors.Hex(0xFFFFFF),
        Placeholder = Colors.Hex(0x8C8FA0),
        Handle = Colors.Hex(0x3A3C46),
        Ink = Colors.Hex(0x000000),
        InkStrength = 0.8f,
        ScrimStrength = 0.5f,
        IsLight = true,
    };

    public static readonly ThemePalette HighContrast = new()
    {
        Background = Colors.Hex(0x000000),
        TitleBar = Colors.Hex(0x000000),
        Panel = Colors.Hex(0x0A0A0D),
        PanelRaised = Colors.Hex(0x16171C),
        PanelBorder = Colors.Hex(0x70737F),
        Well = Colors.Hex(0x000000),
        Text = Colors.Hex(0xFFFFFF),
        Muted = Colors.Hex(0xC8CAD4),
        Faint = Colors.Hex(0x9295A2),
        Accent = Colors.Hex(0xFF8F33),
        Focus = Colors.Hex(0x8FCBFF),
        Connected = Colors.Hex(0x4CE073),
        Warning = Colors.Hex(0xFF5C48),
        Control = Colors.Hex(0x23242C),
        ControlHover = Colors.Hex(0x3C3E4A),
        ControlPressed = Colors.Hex(0x101014),
        Selection = Colors.Hex(0x3F78D0),
        Field = Colors.Hex(0x000000),
        Placeholder = Colors.Hex(0x9A9DAA),
        Handle = Colors.Hex(0xFFFFFF),
        Ink = Colors.Hex(0xFFFFFF),
        InkStrength = 1.6f,
    };

    /// <summary>
    /// The look in use.
    /// </summary>
    public static ThemePalette Current { get; private set; } = Dark;

    public static ThemeKind Kind { get; private set; }

    /// <summary>
    /// Makes <paramref name="kind"/> the look from now on. Elements already made keep the colours they took, so this
    /// is for before the screen's built.
    /// </summary>
    public static void Use(ThemeKind kind)
    {
        Kind = kind;
        Current = kind switch
        {
            ThemeKind.Light => Light,
            ThemeKind.HighContrast => HighContrast,
            _ => Dark,
        };
    }

    /// <summary>
    /// What each look's called, for choosing one.
    /// </summary>
    public static string NameOf(ThemeKind kind) =>
        kind switch
        {
            ThemeKind.Light => "Light",
            ThemeKind.HighContrast => "High contrast",
            _ => "Dark",
        };

    public static float4 Background => Current.Background;
    public static float4 TitleBar => Current.TitleBar;
    public static float4 Panel => Current.Panel;
    public static float4 PanelRaised => Current.PanelRaised;
    public static float4 PanelBorder => Current.PanelBorder;
    public static float4 Well => Current.Well;

    public static float4 Text => Current.Text;
    public static float4 Muted => Current.Muted;
    public static float4 Faint => Current.Faint;

    public static float4 Accent => Current.Accent;
    public static float4 Focus => Current.Focus;

    /// <summary>
    /// Something live and well: output being sent, other software connected.
    /// </summary>
    public static float4 Connected => Current.Connected;

    /// <summary>
    /// Something wrong that needs seeing to, or a song about to run out.
    /// </summary>
    public static float4 Warning => Current.Warning;

    /// <summary>
    /// A button's fill, and as it's hovered and pressed.
    /// </summary>
    public static float4 Control => Current.Control;
    public static float4 ControlHover => Current.ControlHover;
    public static float4 ControlPressed => Current.ControlPressed;

    /// <summary>
    /// A chosen row or selected text, a lit button, and the usual confirming button.
    /// </summary>
    public static float4 Selection => Current.Selection;

    /// <summary>
    /// Where text is typed, and the hint in it before anything is.
    /// </summary>
    public static float4 Field => Current.Field;
    public static float4 Placeholder => Current.Placeholder;

    /// <summary>
    /// A slider's handle: what stands out from its track.
    /// </summary>
    public static float4 Handle => Current.Handle;

    /// <summary>
    /// What goes on a fill of a strong colour (a deck's, the accent's): near black on every look.
    /// </summary>
    public static float4 OnColour => Colors.Hex(0x0B0C0F);

    public static bool IsLight => Current.IsLight;

    /// <summary>
    /// The tint on a list's every other row.
    /// </summary>
    public static float4 AlternateRow => Tint(Current.AlternateRowTint);

    /// <summary>
    /// A faint tint over the panels at <paramref name="alpha"/>, for hovered rows, tracks and dividers: white on a dark
    /// look, black on a light one, as strong as the look needs.
    /// </summary>
    public static float4 Tint(float alpha) => Fade(Current.Ink, Math.Clamp(alpha * Current.InkStrength, 0, 1));

    /// <summary>
    /// Black over the screen behind a dialog, dimming it by <paramref name="alpha"/> as the look has it.
    /// </summary>
    public static float4 Scrim(float alpha) => Colors.Hex(0x000000, alpha * Current.ScrimStrength);

    /// <summary>
    /// A button that does something to be careful of, such as deleting: its fill, hovered and pressed.
    /// </summary>
    public static float4 Danger => Colors.Hex(0xB83A3A);
    public static float4 DangerHover => Colors.Hex(0xD04848);
    public static float4 DangerPressed => Colors.Hex(0x962E2E);

    /// <summary>
    /// <paramref name="color"/> with its alpha multiplied by <paramref name="alpha"/>.
    /// </summary>
    public static float4 Fade(float4 color, float alpha) => color * new float4(1, 1, 1, alpha);

    /// <summary>
    /// Mixes from <paramref name="from"/> to <paramref name="to"/> by <paramref name="t"/>, from 0 to 1.
    /// </summary>
    public static float4 Mix(float4 from, float4 to, float t) => from + (to - from) * Math.Clamp(t, 0, 1);

    /// <summary>
    /// A colour from a hue, saturation and value, each from 0 to 1, worked out in sRGB (as picked) and made linear
    /// for the swapchain.
    /// </summary>
    public static float4 Hsv(float hue, float saturation, float value)
    {
        float Channel(float n)
        {
            var k = (n + hue * 6) % 6;
            return value - value * saturation * MathF.Max(0, MathF.Min(MathF.Min(k, 4 - k), 1));
        }
        float Linear(float c) => c <= 0.04045f ? c / 12.92f : MathF.Pow((c + 0.055f) / 1.055f, 2.4f);
        return new float4(Linear(Channel(5)), Linear(Channel(3)), Linear(Channel(1)), 1);
    }
}
