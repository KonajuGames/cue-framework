using Brutal.Numerics;

namespace Cue.Framework.UI;

public enum TitleIcon
{
    /// <summary>
    /// A picture, the button's <see cref="IconButton.Picture"/>: the options button's cog.
    /// </summary>
    Picture,
    Minimize,
    Maximize,
    Restore,
    Close,
}

/// <summary>
/// A flat button with an icon, for the title bar: the options' cog and the window's own buttons.
/// </summary>
public sealed class IconButton(TitleIcon icon) : Element
{
    public TitleIcon Icon { get; set; } = icon;

    /// <summary>
    /// The picture for <see cref="TitleIcon.Picture"/>, white for tinting with <see cref="IconColor"/>.
    /// </summary>
    public Image? Picture { get; init; }
    public float2 FixedSize { get; set; } = new(46, 34);
    public float4 IconColor { get; set; } = Theme.Text;
    public float4 HoverBackground { get; set; } = Colors.Hex(0xFFFFFF, 0.1f);
    public float4 PressedBackground { get; set; } = Colors.Hex(0xFFFFFF, 0.05f);

    public override bool IsInteractive => true;
    public override Cursor Cursor => Cursor.Arrow;

    public override float2 Measure() => Size = FixedSize;

    public override void Draw(Batch batch)
    {
        if (IsHovered || IsPressed)
            batch.AddRect(ScreenPosition, Size, IsPressed && IsHovered ? PressedBackground : HoverBackground);

        // A 10 pixel icon, on whole pixels so its lines stay crisp.
        var x = MathF.Round(ScreenPosition.X + Size.X / 2 - 5);
        var y = MathF.Round(ScreenPosition.Y + Size.Y / 2 - 5);
        switch (Icon)
        {
            case TitleIcon.Picture when Picture is { } picture:
                batch.AddImage(picture, ScreenPosition + (Size - new float2(picture.Size.X, picture.Size.Y)) / 2, IconColor);
                break;
            case TitleIcon.Minimize:
                batch.AddRect(new float2(x, y + 5), new float2(10, 1), IconColor);
                break;
            case TitleIcon.Maximize:
                batch.AddBorder(new float2(x, y), new float2(10, 10), 1, IconColor);
                break;
            case TitleIcon.Restore:
                batch.AddBorder(new float2(x, y + 2), new float2(8, 8), 1, IconColor);
                batch.AddRect(new float2(x + 2, y), new float2(8, 1), IconColor);
                batch.AddRect(new float2(x + 9, y), new float2(1, 8), IconColor);
                break;
            case TitleIcon.Close:
                for (var i = 0; i < 10; i++)
                {
                    batch.AddRect(new float2(x + i, y + i), new float2(1, 1), IconColor);
                    batch.AddRect(new float2(x + 9 - i, y + i), new float2(1, 1), IconColor);
                }
                break;
        }
    }
}

/// <summary>
/// Empty space of a set width (or height, in a column), to push things apart in a row.
/// </summary>
public sealed class Spacer(float length) : Element
{
    public override float2 Measure() => Size = new float2(length, length);

    public override void Draw(Batch batch) { }
}
