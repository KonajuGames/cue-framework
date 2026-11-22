namespace Cue.Framework.UI;

/// <summary>
/// How big everything on the screen is drawn: chosen in the options, as Windows has it for the screen by default,
/// and taken on as the app starts (see <see cref="Root.Scale"/>).
/// </summary>
public static class ScreenScale
{
    /// <summary>
    /// The sizes there are to choose from, as a share of the size the screen's designed at.
    /// </summary>
    public static readonly IReadOnlyList<float> Choices = [1, 1.25f, 1.5f, 1.75f, 2];

    /// <summary>
    /// What to call a size on the list: "150%", or for none chosen, Windows' own for the screen.
    /// </summary>
    public static string Name(float? scale, float windows) =>
        scale is { } chosen ? Percent(chosen) : $"As Windows has it ({Percent(windows)})";

    static string Percent(float scale) => $"{MathF.Round(scale * 100):0}%";

    /// <summary>
    /// The size to draw at: the one <paramref name="chosen"/>, or else <paramref name="windows"/>' own for the screen,
    /// kept to the sizes there are and no bigger than <paramref name="largest"/>, what the screen has room for (see
    /// <see cref="Largest"/>).
    /// </summary>
    public static float Use(float? chosen, float windows, float largest = float.MaxValue)
    {
        var scale = chosen ?? (windows > 0 && float.IsFinite(windows) ? windows : 1);
        return Math.Clamp(MathF.Min(scale, largest), Choices[0], Choices[^1]);
    }

    /// <summary>
    /// The biggest size a screen <paramref name="width"/> by <paramref name="height"/> pixels has room for, an app
    /// needing <paramref name="smallest"/> of the UI's units to lay out without drawing things over each other: on a
    /// 1920 by 1080 screen, 150% for one needing 1280 by 720. Never under the smallest size there is.
    /// </summary>
    public static float Largest(int width, int height, Brutal.Numerics.float2 smallest) =>
        MathF.Max(Choices[0], MathF.Min(width / smallest.X, height / smallest.Y));

    /// <summary>
    /// What to call a size on the list, saying when the screen hasn't room for it and it's drawn smaller to fit:
    /// "200%, drawn at 150% to fit".
    /// </summary>
    public static string Name(float? scale, float windows, float largest)
    {
        var name = Name(scale, windows);
        var drawn = Use(scale, windows, largest);
        return drawn < Use(scale, windows) ? $"{name}, drawn at {Percent(drawn)} to fit" : name;
    }
}
