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
    /// kept to the sizes there are.
    /// </summary>
    public static float Use(float? chosen, float windows)
    {
        var scale = chosen ?? (windows > 0 && float.IsFinite(windows) ? windows : 1);
        return Math.Clamp(scale, Choices[0], Choices[^1]);
    }
}
