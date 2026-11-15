using Brutal.Numerics;

namespace Cue.Framework.UI;

/// <summary>
/// A row of menu titles across the window ("File", "Edit"), each opening its drop-down menu below it on a
/// <see cref="ContextMenu"/> when clicked. While one's open, moving the mouse onto another title opens that one
/// instead; clicking the open title, or anywhere else, puts it away. Each menu's items are asked for afresh each
/// time it opens, so they can be enabled, ticked or left out by what's going on. Set <see cref="Width"/> for the bar
/// to run across the window.
/// </summary>
public sealed class MenuBar : Panel
{
    readonly Font _font;
    readonly ContextMenu _menu;
    readonly Dictionary<Button, Func<IReadOnlyList<MenuItem>>> _menus = [];

    /// <param name="menu">Where the drop-downs open: the app's one context menu, added to the root after
    /// everything it should sit over.</param>
    public MenuBar(Font font, ContextMenu menu)
    {
        _font = font;
        _menu = menu;
        Direction = Direction.Horizontal;
        Padding = 2;
        Spacing = 0;
        Background = Theme.Panel;
        menu.PointerMoved += point =>
        {
            if (OpenTitle is not null && Titles().FirstOrDefault(title => title.Contains(point)) is { } other && !ReferenceEquals(_menu.Owner, other))
                Open(other);
        };
    }

    /// <summary>
    /// How wide the bar is drawn; its titles sit at the left.
    /// </summary>
    public float Width { get; set; }

    /// <summary>
    /// Adds a menu called <paramref name="title"/> at the end of the bar, whose items <paramref name="items"/> gives
    /// each time it opens.
    /// </summary>
    public Button AddMenu(string title, Func<IReadOnlyList<MenuItem>> items)
    {
        var button = Add(
            new Button(_font, title)
            {
                Padding = 6,
                Background = Colors.Transparent,
                HoverBackground = Theme.Fade(Theme.Accent, 0.25f),
                PressedBackground = Theme.Fade(Theme.Accent, 0.35f),
                LitBackground = Theme.Fade(Theme.Accent, 0.35f),
                FocusColor = Theme.Focus,
            }
        );
        _menus[button] = items;
        button.Clicked += _ => Open(button);
        return button;
    }

    IEnumerable<Button> Titles() => Children.OfType<Button>().Where(button => button.Visible && _menus.ContainsKey(button));

    /// <summary>
    /// The menus' titles, in order.
    /// </summary>
    public IReadOnlyList<string> MenuTitles => Titles().Select(button => button.Text).ToList();

    /// <summary>
    /// The title of the menu open now, or null if none of the bar's is.
    /// </summary>
    public string? OpenTitle => _menu.Visible && _menu.Owner is Button button && _menus.ContainsKey(button) ? button.Text : null;

    /// <summary>
    /// Opens the menu called <paramref name="title"/>, as clicking its title would.
    /// </summary>
    public void Open(string title) => Open(Titles().First(button => button.Text == title));

    void Open(Button title) =>
        _menu.Show(new float2(title.ScreenPosition.X, ScreenPosition.Y + Size.Y - 1), _menus[title](), owner: title);

    public override float2 Measure()
    {
        // The open menu's title stays lit while its menu's up.
        foreach (var title in Titles())
            title.Lit = ReferenceEquals(_menu.Owner, title) && _menu.Visible;
        var size = MeasureChildren() + new float2(2 * Inset, 2 * Inset);
        return Size = new float2(MathF.Max(Width, size.X), size.Y);
    }

    public override void Draw(Batch batch)
    {
        DrawFrame(batch);
        batch.AddRect(ScreenPosition + new float2(0, Size.Y - 1), new float2(Size.X, 1), Theme.PanelBorder);
        DrawChildren(batch);
    }
}
