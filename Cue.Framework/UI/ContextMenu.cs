using Brutal.Numerics;

namespace Cue.Framework.UI;

/// <summary>
/// One choice on a <see cref="ContextMenu"/>. One with a <see cref="Submenu"/> opens it beside the menu, when the
/// mouse comes over it or it's clicked, rather than being chosen itself. <see cref="Checked"/> marks the one that's
/// in effect now. <see cref="Shortcut"/> is the key that does the same, written muted at the right ("Ctrl+S"); the
/// menu only shows it, so the app handles the key itself.
/// </summary>
public sealed record MenuItem(
    string Text,
    Action Chosen,
    bool Enabled = true,
    IReadOnlyList<MenuItem>? Submenu = null,
    bool Checked = false,
    string? Shortcut = null
)
{
    public MenuItem(string text, IReadOnlyList<MenuItem> submenu, bool enabled = true)
        : this(text, () => { }, enabled && submenu.Count > 0, submenu) { }

    /// <summary>
    /// A line between groups of items. Put this one in the list: it's told apart by being this very item. One at
    /// either end, or next to another, is left out, so items an app leaves out needn't take theirs with them.
    /// </summary>
    public static MenuItem Separator { get; } = new("", () => { }, false);
}

/// <summary>
/// A short list of things to do, shown where something was right-clicked, with a submenu for an item that has one.
/// Choosing one, clicking anywhere else, or <see cref="Close"/> (for Escape) puts it away. It covers the whole screen,
/// invisibly, to catch the click that closes it, so add it to the root after everything it should sit over.
/// </summary>
public sealed class ContextMenu : Panel
{
    const float EDGE = 4;
    const float ARROW = 4;
    const float SHORTCUT_GAP = 24;

    readonly Font _font;
    readonly Panel _box;
    readonly Panel _subBox;
    readonly Dictionary<Button, MenuItem> _items = [];
    float2 _at;

    // The item whose submenu is open in _subBox, if any.
    Button? _openItem;

    // The least width the menu's items take, so a dropdown's list is at least as wide as it.
    float _minWidth;

    public float2 ScreenSize { get; set; }

    /// <summary>
    /// What opened the menu showing now, as given to <see cref="Show"/>; null while it's put away. A
    /// <see cref="MenuBar"/> knows by it whether the menu's still one of its own.
    /// </summary>
    public object? Owner { get; private set; }

    /// <summary>
    /// The mouse moved while the menu's open, anywhere on the screen (the menu covers it all): for a
    /// <see cref="MenuBar"/> to open the menu of the title it's moved onto.
    /// </summary>
    public event Action<float2>? PointerMoved;

    public ContextMenu(Font font)
    {
        _font = font;
        Visible = false;
        _box = Add(NewBox());
        _subBox = Add(NewBox());
        _subBox.Visible = false;
        Clicked += _ =>
        {
            if (!_pressedInBox)
                Close();
        };
    }

    static Panel NewBox() =>
        new()
        {
            Padding = 4,
            Spacing = 1,
            Background = Theme.PanelRaised,
            BorderColor = Theme.PanelBorder,
            BorderWidth = 1,
        };

    // A press on the menu itself (a disabled item, or between items) mustn't count as a click away from it.
    bool _pressedInBox;

    public override void PointerDown(float2 point, Modifiers modifiers) =>
        _pressedInBox = _box.Contains(point) || (_subBox.Visible && _subBox.Contains(point));

    public override bool IsInteractive => true;
    public override Cursor Cursor => Cursor.Arrow;

    public override void PointerMove(float2 point) => PointerMoved?.Invoke(point);

    // A right-click elsewhere just closes it, rather than opening another under it.
    public override void SecondaryClick(float2 point) => Close();

    /// <summary>
    /// Shows <paramref name="items"/> with the menu's top-left corner at <paramref name="at"/>, moved in to stay on
    /// the screen, at least <paramref name="minWidth"/> wide. <paramref name="owner"/> is what opened it, kept as
    /// <see cref="Owner"/> while it shows.
    /// </summary>
    public void Show(float2 at, IEnumerable<MenuItem> items, float minWidth = 0, object? owner = null)
    {
        Owner = owner;
        _minWidth = minWidth;
        _items.Clear();
        CloseSubmenu();
        Fill(_box, items);
        _at = at;
        Visible = true;
    }

    /// <summary>
    /// The texts of the open submenu's items, or null if none is open.
    /// </summary>
    public IReadOnlyList<string>? SubmenuTexts =>
        _subBox.Visible ? _subBox.Children.OfType<Button>().Select(button => button.Text).ToList() : null;

    /// <summary>
    /// The texts of the menu's items, for tests.
    /// </summary>
    public IReadOnlyList<string> ItemTexts => _box.Children.OfType<Button>().Select(button => button.Text).ToList();

    /// <summary>
    /// Chooses the item with <paramref name="text"/>, as clicking it would, for tests.
    /// </summary>
    public void Choose(string text) => _box.Children.OfType<Button>().First(button => button.Text == text).RaiseClicked();

    void Fill(Panel box, IEnumerable<MenuItem> items)
    {
        foreach (var button in box.Children.OfType<Button>())
            _items.Remove(button);
        box.Children.Clear();

        // Where any item is ticked, every item's text moves along to leave a column for the ticks, so they line up.
        var list = WithoutStraySeparators(items);
        var tickColumn = list.Any(item => item.Checked) ? _font.Advance("•") + 6 : 0;
        foreach (var item in list)
        {
            if (IsSeparator(item))
            {
                box.Add(new Divider(0));
                continue;
            }
            var button = box.Add(
                new Button(_font, item.Text)
                {
                    TextIndent = tickColumn,
                    Padding = 6,
                    Enabled = item.Enabled,
                    Background = Colors.Transparent,
                    HoverBackground = Theme.Fade(Theme.Accent, 0.35f),
                    PressedBackground = Theme.Fade(Theme.Accent, 0.5f),
                    FocusColor = Theme.Focus,
                }
            );
            _items[button] = item;
            button.Clicked += _ =>
            {
                if (item.Submenu is not null)
                {
                    OpenSubmenu(button);
                    return;
                }
                Close();
                item.Chosen();
            };
        }
    }

    static bool IsSeparator(MenuItem item) => ReferenceEquals(item, MenuItem.Separator);

    // The items with no separator at either end or next to another.
    static List<MenuItem> WithoutStraySeparators(IEnumerable<MenuItem> items)
    {
        var list = new List<MenuItem>();
        foreach (var item in items)
        {
            if (!IsSeparator(item) || (list.Count > 0 && !IsSeparator(list[^1])))
                list.Add(item);
        }
        if (list.Count > 0 && IsSeparator(list[^1]))
            list.RemoveAt(list.Count - 1);
        return list;
    }

    void OpenSubmenu(Button button)
    {
        if (_openItem == button || _items[button].Submenu is not { } submenu)
            return;
        Fill(_subBox, submenu);
        _openItem = button;
        _subBox.Visible = true;
    }

    void CloseSubmenu()
    {
        foreach (var button in _subBox.Children.OfType<Button>())
            _items.Remove(button);
        _subBox.Children.Clear();
        _openItem = null;
        _subBox.Visible = false;
    }

    public void Close()
    {
        Visible = false;
        Owner = null;
        CloseSubmenu();
    }

    public override float2 Measure()
    {
        // The mouse over an item with a submenu opens it; over any other item of the menu, it closes the one open.
        if (_box.Children.OfType<Button>().FirstOrDefault(button => button.IsHovered) is { } hovered && hovered != _openItem)
        {
            if (_items[hovered].Submenu is not null)
                OpenSubmenu(hovered);
            else
                CloseSubmenu();
        }
        MeasureBox(_box, _minWidth);
        if (_subBox.Visible)
            MeasureBox(_subBox, 0);
        return Size = ScreenSize;
    }

    void MeasureBox(Panel box, float minWidth)
    {
        // Every item as wide as the widest, so the highlight runs the full width, with room for a submenu's arrow
        // and the shortcuts' column; the separators as wide too.
        var buttons = box.Children.OfType<Button>().ToList();
        foreach (var button in buttons)
            button.MinSize = float2.Zero;
        box.Measure();
        var shortcuts = buttons.Select(button => _items[button].Shortcut is { } keys ? _font.Advance(keys) : 0).DefaultIfEmpty(0).Max();
        var width = buttons.Count > 0 ? buttons.Max(button => button.Size.X) + 4 * ARROW + (shortcuts > 0 ? shortcuts + SHORTCUT_GAP : 0) : 0;
        width = MathF.Max(width, MathF.Max(160, minWidth - 2 * (box.Padding + box.BorderWidth)));
        foreach (var button in buttons)
            button.MinSize = new float2(width, 0);
        foreach (var divider in box.Children.OfType<Divider>())
            divider.Length = width;
        box.Measure();
    }

    public override void Arrange(float2 position)
    {
        ArrangeSelf(position);
        var x = Math.Clamp(_at.X, EDGE, MathF.Max(EDGE, ScreenSize.X - _box.Size.X - EDGE));
        var y = Math.Clamp(_at.Y, EDGE, MathF.Max(EDGE, ScreenSize.Y - _box.Size.Y - EDGE));
        _box.Arrange(position + new float2(MathF.Round(x), MathF.Round(y)));

        if (_subBox.Visible && _openItem is { } item)
        {
            // To the right of its item, level with it; to the left if there's no room on the right.
            var right = _box.ScreenPosition.X + _box.Size.X - 2;
            var subX = right + _subBox.Size.X + EDGE <= position.X + ScreenSize.X ? right : _box.ScreenPosition.X - _subBox.Size.X + 2;
            var subY = Math.Clamp(
                item.ScreenPosition.Y - _subBox.Padding - _subBox.BorderWidth,
                position.Y + EDGE,
                MathF.Max(position.Y + EDGE, position.Y + ScreenSize.Y - _subBox.Size.Y - EDGE)
            );
            _subBox.Arrange(new float2(MathF.Round(MathF.Max(position.X + EDGE, subX)), MathF.Round(subY)));
        }
    }

    public override void Draw(Batch batch)
    {
        base.Draw(batch);

        // The ticks, in their column.
        foreach (var box in new[] { _box, _subBox })
        {
            if (!box.Visible)
                continue;
            foreach (var button in box.Children.OfType<Button>())
            {
                if (!_items[button].Checked)
                    continue;
                var at = button.ScreenPosition + new float2(button.Padding + button.BorderWidth, MathF.Round((button.Size.Y - _font.LineHeight) / 2));
                batch.AddText(_font, "•", at, button.TextColor);
            }
            // The shortcuts, muted, at the right.
            foreach (var button in box.Children.OfType<Button>())
            {
                if (_items[button].Shortcut is not { } keys)
                    continue;
                var color = button.Enabled ? Theme.Muted : Theme.Muted * new float4(1, 1, 1, 0.4f);
                var x = button.ScreenPosition.X + button.Size.X - button.Padding - button.BorderWidth - _font.Advance(keys);
                var y = button.ScreenPosition.Y + MathF.Round((button.Size.Y - _font.LineHeight) / 2);
                batch.AddText(_font, keys, new float2(MathF.Round(x), y), color);
            }
        }
        // A small arrow at the right of each item with a submenu.
        foreach (var button in _box.Children.OfType<Button>())
        {
            if (_items[button].Submenu is null)
                continue;
            var color = button.Enabled ? button.TextColor : button.TextColor * new float4(1, 1, 1, 0.4f);
            var right = button.ScreenPosition.X + button.Size.X - 8;
            var middle = button.ScreenPosition.Y + MathF.Round(button.Size.Y / 2);
            for (var i = 0; i < ARROW; i++)
                batch.AddRect(new float2(right - ARROW + i, middle - ARROW + i), new float2(1, 2 * (ARROW - i)), color);
        }
    }
}
