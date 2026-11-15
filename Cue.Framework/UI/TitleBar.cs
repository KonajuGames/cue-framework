using Brutal.Numerics;

namespace Cue.Framework.UI;

/// <summary>
/// The bar across the top of the window, in place of Windows' own. On the left, the logo, the app's name (its
/// wordmark, in a colour for each part) and an optional <see cref="Subtitle"/>; on the right, the clock, the options
/// button and the window's buttons. The app adds its own widgets to three places: <see cref="Centre"/>, centred
/// between the sides (or moved left to keep clear of the right); <see cref="Status"/>, before the clock; and
/// <see cref="Tools"/>, icon buttons before the options button. Its empty space is where the window can be dragged
/// from (see <see cref="IsCaption"/>). Call <see cref="Update"/> once a frame.
/// </summary>
public sealed class TitleBar : Panel
{
    public const float HEIGHT = 34;

    /// <summary>
    /// The least gap between the middle and either side.
    /// </summary>
    public const float GAP = 16;

    readonly Panel _left;
    readonly Picture _logo;
    readonly Panel _wordmark;
    readonly Label _subtitle;
    readonly Panel _right;
    readonly Spacer _statusGap;
    readonly Label _clock;
    readonly IconButton _maximize;
    readonly Font _body;
    readonly Font _bold;

    public float Width { get; set; } = 1280;

    /// <summary>
    /// Whether the window is maximised, which the middle button shows and toggles.
    /// </summary>
    public Func<bool> IsMaximized { get; set; } = () => false;

    /// <summary>
    /// What follows the app's name, muted (a show's name, say); hidden while empty.
    /// </summary>
    public string Subtitle { get; set; } = "";

    /// <summary>
    /// The app's controls in the middle of the bar, centred between the sides, or moved left to keep clear of the
    /// right, which always shows.
    /// </summary>
    public Panel Centre { get; }

    /// <summary>
    /// The app's indicators before the clock, side by side with no spacing of their own (put <see cref="Spacer"/>s
    /// between them). Labels directly in it have their capitals centred on the bar, as the clock's are.
    /// </summary>
    public Panel Status { get; }

    /// <summary>
    /// The app's <see cref="IconButton"/>s, before the options button.
    /// </summary>
    public Panel Tools { get; }

    /// <summary>
    /// The right-hand side: the status, the clock and the buttons, for tests.
    /// </summary>
    public Element Right => _right;

    /// <summary>
    /// Called each <see cref="Measure"/>, once the sides are measured, with the room the middle has between them (less
    /// <see cref="GAP"/> each side): for an app to shorten what's in <see cref="Centre"/> on a crowded bar. The
    /// middle's measured again after.
    /// </summary>
    public Action<float>? FitCentre { get; set; }

    /// <summary>
    /// Raised at the end of <see cref="Arrange"/>, for an app to nudge its own widgets into line (see
    /// <see cref="CentreCapitals"/>).
    /// </summary>
    public event Action? Arranged;

    public event Action? OptionsClicked;
    public event Action? MinimizeClicked;
    public event Action? MaximizeClicked;
    public event Action? CloseClicked;

    /// <param name="logo">The app's logo, shown at its own size.</param>
    /// <param name="cog">The options button's picture, white for tinting.</param>
    /// <param name="wordmark">The app's name, in parts, each in its colour, drawn as one word in
    /// <paramref name="bold"/>; the close button's tooltip names it too.</param>
    public TitleBar(Font body, Font bold, Image logo, Image cog, params (string Text, float4 Color)[] wordmark)
    {
        (_body, _bold) = (body, bold);
        Background = Theme.Panel;

        _left = Add(new Panel { Direction = Direction.Horizontal, Spacing = 10 });
        _logo = _left.Add(new Picture(logo));
        _wordmark = _left.Add(new Panel { Direction = Direction.Horizontal, Spacing = 0 });
        foreach (var (text, color) in wordmark)
            _wordmark.Add(new Label(bold, text) { Color = color });
        _subtitle = _left.Add(new Label(body) { Color = Theme.Muted, Visible = false });

        Centre = Add(new Panel { Direction = Direction.Horizontal, Spacing = 8 });

        _right = Add(new Panel { Direction = Direction.Horizontal, Spacing = 0 });
        Status = _right.Add(new Panel { Direction = Direction.Horizontal, Spacing = 0 });
        _statusGap = _right.Add(new Spacer(16) { Visible = false });
        _clock = _right.Add(new Label(body) { Color = Theme.Text });
        _right.Add(new Spacer(12));
        Tools = _right.Add(new Panel { Direction = Direction.Horizontal, Spacing = 0 });
        var options = _right.Add(new IconButton(TitleIcon.Picture) { Picture = cog, Tooltip = "Options" });
        options.Clicked += _ => OptionsClicked?.Invoke();
        _right.Add(new Spacer(8));
        var minimize = _right.Add(new IconButton(TitleIcon.Minimize) { Tooltip = "Minimise" });
        minimize.Clicked += _ => MinimizeClicked?.Invoke();
        _maximize = _right.Add(new IconButton(TitleIcon.Maximize));
        _maximize.Clicked += _ => MaximizeClicked?.Invoke();
        var name = string.Concat(wordmark.Select(part => part.Text));
        var close = _right.Add(
            new IconButton(TitleIcon.Close) { HoverBackground = Colors.Hex(0xE81123), PressedBackground = Colors.Hex(0xF1707A), Tooltip = $"Close {name}" }
        );
        close.Clicked += _ => CloseClicked?.Invoke();
        Update();
    }

    /// <summary>
    /// Whether the window can be dragged from <paramref name="point"/>: inside the bar, but not on a button.
    /// </summary>
    public bool IsCaption(float2 point) => Contains(point) && HitTest(point) is null;

    /// <summary>
    /// Brings the subtitle, the clock and the maximise button up to date. The app updates its own widgets.
    /// </summary>
    public void Update()
    {
        _subtitle.Text = Subtitle;
        _subtitle.Visible = Subtitle.Length > 0;
        _clock.Text = DateTime.Now.ToString("HH:mm:ss");
        _maximize.Icon = IsMaximized() ? TitleIcon.Restore : TitleIcon.Maximize;
        _maximize.Tooltip = IsMaximized() ? "Restore" : "Maximise";
    }

    public override float2 Measure()
    {
        _statusGap.Visible = Status.Children.Any(child => child.Visible);
        MeasureChildren();
        if (FitCentre is { } fit)
        {
            fit(Width - (10 + _left.Size.X + GAP) - (_right.Size.X + GAP));
            Centre.Measure();
        }
        return Size = new float2(Width, HEIGHT);
    }

    public override void Arrange(float2 position)
    {
        ArrangeSelf(position);
        float Middle(Element child) => MathF.Round(position.Y + (HEIGHT - child.Size.Y) / 2);
        _left.Arrange(new float2(position.X + 10, Middle(_left)));
        var centreMin = position.X + 10 + _left.Size.X + GAP;
        var centreMax = position.X + Width - _right.Size.X - GAP - Centre.Size.X;
        var centreX = Math.Clamp(position.X + (Width - Centre.Size.X) / 2, centreMin, MathF.Max(centreMin, centreMax));
        Centre.Arrange(new float2(MathF.Round(centreX), Middle(Centre)));
        _right.Arrange(new float2(position.X + Width - _right.Size.X, Middle(_right)));
        AlignMiddle(_left);
        AlignMiddle(Centre);
        AlignMiddle(_right);
        AlignMiddle(Status);
        AlignMiddle(Tools);

        // The name and the subtitle's capitals centred on the logo, and the status's and the clock's on the right:
        // centring their lines would drop them, as a line keeps room below for descenders.
        var logoMiddle = _logo.ScreenPosition.Y + _logo.Size.Y / 2;
        _wordmark.Arrange(new float2(_wordmark.ScreenPosition.X, MathF.Round(logoMiddle - _bold.Ascender + _bold.CapHeight / 2)));
        CentreCapitals(_subtitle, logoMiddle);
        var rightMiddle = _right.ScreenPosition.Y + _right.Size.Y / 2;
        foreach (var label in Status.Children.OfType<Label>())
            CentreCapitals(label, rightMiddle);
        CentreCapitals(_clock, rightMiddle);
        Arranged?.Invoke();
    }

    /// <summary>
    /// Puts <paramref name="label"/>'s capitals, rather than its line box, centred on <paramref name="middle"/>.
    /// </summary>
    public static void CentreCapitals(Label label, float middle) =>
        label.Arrange(new float2(label.ScreenPosition.X, MathF.Round(middle + label.Font.CapHeight / 2 - label.Font.Ascender)));

    /// <summary>
    /// Centres a row's children on its middle, since a row lines them up along the top.
    /// </summary>
    static void AlignMiddle(Panel row)
    {
        foreach (var child in row.Children)
            child.Arrange(new float2(child.ScreenPosition.X, MathF.Round(row.ScreenPosition.Y + (row.Size.Y - child.Size.Y) / 2)));
    }

    public override void Draw(Batch batch)
    {
        DrawFrame(batch);
        batch.AddRect(ScreenPosition + new float2(0, HEIGHT - 1), new float2(Width, 1), Theme.PanelBorder);
        DrawChildren(batch);
    }
}
