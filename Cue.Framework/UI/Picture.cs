using Brutal.Numerics;

namespace Cue.Framework.UI;

/// <summary>
/// An image on its own, at its own size, tinted (white shows it as it is).
/// </summary>
public sealed class Picture(Image image) : Element
{
    public Image Image { get; set; } = image;
    public float4 Tint { get; set; } = Colors.White;

    public override float2 Measure() => Size = new float2(Image.Size.X, Image.Size.Y);

    public override void Draw(Batch batch) => batch.AddImage(Image, ScreenPosition, Tint);
}
