using System.Globalization;
using Brutal.Numerics;

namespace Cue.Framework.UI;

/// <summary>
/// A colour as people pick it: red, green, blue and alpha from 0 to 1, in sRGB. What the <see cref="ColourPicker"/>
/// deals in; drawn as the UI's linear <c>float4</c> with <see cref="ToLinear"/>. Its sums are Godot's <c>Color</c>'s,
/// to the last bit, so an app reading Godot's files (Cuedeck) picks the colours Godot would.
/// </summary>
public readonly record struct Srgb(float R, float G, float B, float A = 1)
{
    /// <summary>
    /// Godot's <c>Color.from_hsv()</c>, in 32-bit floats as Godot works it out: hue, saturation and value from 0 to 1.
    /// </summary>
    public static Srgb FromHsv(double hue, double saturation, double value, double alpha = 1)
    {
        float h = (float)hue, s = (float)saturation, v = (float)value;
        if (s == 0)
            return new Srgb(v, v, v, (float)alpha);
        h *= 6f;
        h %= 6f;
        var i = (int)MathF.Floor(h);
        var f = h - i;
        var p = v * (1f - s);
        var q = v * (1f - s * f);
        var t = v * (1f - s * (1f - f));
        var (r, g, b) = i switch
        {
            0 => (v, t, p),
            1 => (q, v, p),
            2 => (p, v, t),
            3 => (p, q, v),
            4 => (t, p, v),
            _ => (v, p, q),
        };
        return new Srgb(r, g, b, (float)alpha);
    }

    /// <summary>
    /// The hue, 0 to 1 round the colour wheel from red (Godot's <c>Color.h</c>); 0 for a grey.
    /// </summary>
    public float Hue
    {
        get
        {
            var max = Math.Max(R, Math.Max(G, B));
            var min = Math.Min(R, Math.Min(G, B));
            var delta = max - min;
            if (delta == 0)
                return 0;
            float h;
            if (R == max)
                h = (G - B) / delta;
            else if (G == max)
                h = 2 + (B - R) / delta;
            else
                h = 4 + (R - G) / delta;
            h /= 6;
            return h < 0 ? h + 1 : h;
        }
    }

    /// <summary>
    /// "rrggbb", lower case, without a hash (Godot's <c>to_html(false)</c>).
    /// </summary>
    public string ToHex() => $"{Channel(R):x2}{Channel(G):x2}{Channel(B):x2}";

    // Godot rounds halves away from zero, unlike C#'s default.
    static int Channel(float c) => Math.Clamp((int)Math.Round(c * 255.0, MidpointRounding.AwayFromZero), 0, 255);

    /// <summary>
    /// Reads a hex code, "rrggbb" or "rrggbbaa", with or without a hash.
    /// </summary>
    public static bool TryParseHex(string text, out Srgb colour)
    {
        colour = default;
        var hex = text.StartsWith('#') ? text[1..] : text;
        if (hex.Length is not (6 or 8) || !hex.All(char.IsAsciiHexDigit))
            return false;
        float Pair(int i) => int.Parse(hex.AsSpan(i, 2), NumberStyles.HexNumber) / 255f;
        colour = new Srgb(Pair(0), Pair(2), Pair(4), hex.Length == 8 ? Pair(6) : 1);
        return true;
    }

    /// <summary>
    /// The colour as the UI draws it: linear, opaque.
    /// </summary>
    public float4 ToLinear() => Colors.FromSrgb(R, G, B);
}
