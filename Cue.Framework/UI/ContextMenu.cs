using Brutal.Numerics;

namespace Cue.Framework.UI;

/// <summary>
/// One choice on a <see cref="ContextMenu"/>. One with a <see cref="Submenu"/> opens it beside the menu, when the
/// mouse comes over it or it's clicked, rather than being chosen itself. <see cref="Checked"/> marks the one that's
/// in effect now. <see cref="Shortcut"/> is the key that does the same, written muted at the right ("Ctrl+S"); the
/// menu only shows it, so the app handles the key itself. One with a <see cref="Swatch"/> is shown as a square of
/// that colour rather than its text, which names it for the keys and tests; swatches next to each other in the list
/// make a grid, as near square as they'll go (nine are three by three).
/// </summary>
public sealed record MenuItem(
    string Text,
    Action Chosen,
    bool Enabled = true,
    IReadOnlyList<MenuItem>? Submenu = null,
    bool Checked = false,
    string? Shortcut = null,
    float4? Swatch = null
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
/// Choosing one, clicking anywhere else, or <see cref="Close"/> puts it away. It covers the whole screen, invisibly,
/// to catch the click that closes it, so add it to the root after everything it should sit over.
/// <para>
/// While it's open it has first say on the keys: Up and Down (and Home and End) move the highlight over the items
/// that can be chosen, Enter or Space chooses the highlighted one, Right opens its submenu and Left closes it, a
/// letter moves to the next item starting with it, and Escape closes the submenu, then the menu. Left or Right with
/// nowhere to go raises <see cref="SteppedOut"/>, for a <see cref="MenuBar"/> to open the next menu along. The mouse
/// moves the highlight too.
/// </para>
/// </summary>
public sealed class ContextMenu : Panel
{
    const float EDGE = 4;
    const float ARROW = 4;
    const float SHORTCUT_GAP = 24;
    const float SWATCH = 30;
    const float SWATCH_GAP = 6;

    readonly Font _font;
    readonly Panel _box;
    readonly Panel _subBox;
    readonly Dictionary<Button, MenuItem> _items = [];
    float2 _at;

    // The item whose submenu is open in _subBox, if any.
    Button? _openItem;

    // The item highlighted, by the keys or the mouse, which Enter chooses; in _box or _subBox.
    Button? _highlighted;

    static readonly float4 HIGHLIGHT = Theme.Fade(Theme.Accent, 0.35f);

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

    /// <summary>
    /// Left (-1) or Right (+1) was pressed with no submenu to close or open: for a <see cref="MenuBar"/> to open the
    /// menu beside the open one.
    /// </summary>
    public event Action<int>? SteppedOut;

    /// <summary>
    /// The highlighted item's text, or null if none is.
    /// </summary>
    public string? HighlightedText => _highlighted is { } button ? TextOf(button) : null;

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

    /// <summary>
    /// The mouse moved to <paramref name="point"/>, over the menu or not (<see cref="Root"/> tells an open menu of
    /// every move, as one over an item reaches only the item). Over an item, it's highlighted, and its submenu opened
    /// or the open one closed.
    /// </summary>
    internal void MouseMoved(float2 point)
    {
        var over = ButtonsIn(_subBox).Concat(ButtonsIn(_box)).FirstOrDefault(button => button.Enabled && button.Contains(point));
        if (over is not null && over != _highlighted)
        {
            Highlight(over);
            if (Buttons(_box).Contains(over) && over != _openItem)
            {
                if (_items[over].Submenu is not null)
                    OpenSubmenu(over);
                else
                    CloseSubmenu();
            }
        }
        PointerMoved?.Invoke(point);
    }

    static IEnumerable<Button> ButtonsIn(Panel box) => box.Visible ? Buttons(box) : [];

    // Every item's button in a box, in order, including the swatches in its grids.
    static IEnumerable<Button> Buttons(Panel panel) =>
        panel.Children.SelectMany(child => child switch
        {
            Button button => [button],
            Panel inner => Buttons(inner),
            _ => Enumerable.Empty<Button>(),
        });

    bool IsSwatch(Button button) => _items.TryGetValue(button, out var item) && item.Swatch is not null;

    string TextOf(Button button) => _items.TryGetValue(button, out var item) ? item.Text : button.Text;

    void Highlight(Button? button)
    {
        if (_highlighted is { } old)
        {
            // A swatch keeps its colour and is ringed instead.
            if (IsSwatch(old))
                (old.BorderColor, old.BorderWidth) = (SwatchEdge, 1);
            else
                old.Background = old.HoverBackground = Colors.Transparent;
        }
        _highlighted = button;
        if (button is not null)
        {
            if (IsSwatch(button))
                (button.BorderColor, button.BorderWidth) = (Theme.Text, 2);
            else
                button.Background = button.HoverBackground = HIGHLIGHT;
        }
    }

    static float4 SwatchEdge => Theme.Fade(Theme.Text, 0.25f);

    /// <summary>
    /// Highlights the first item that can be chosen, as when a menu's opened from the keyboard.
    /// </summary>
    public void HighlightFirst() => Highlight(ButtonsIn(_box).FirstOrDefault(button => button.Enabled));

    /// <summary>
    /// Highlights the item called <paramref name="text"/>, if there's one that can be chosen (a dropdown's choice).
    /// </summary>
    public void HighlightItem(string text)
    {
        if (ButtonsIn(_box).FirstOrDefault(button => button.Enabled && TextOf(button) == text) is { } button)
            Highlight(button);
    }

    public override bool PreviewKeyDown(Key key, Modifiers modifiers)
    {
        if (!Visible)
            return false;
        // Shortcuts held with Ctrl or Alt go on to the app (Alt+a letter to a menu bar).
        if ((modifiers & (Modifiers.Control | Modifiers.Alt | Modifiers.Super)) != 0)
            return key == Key.Escape && Escape();
        var inSub = _highlighted is not null && Buttons(_subBox).Contains(_highlighted);
        var box = inSub ? _subBox : _box;
        var choices = ButtonsIn(box).Where(button => button.Enabled).ToList();
        if (StepInGrid(key, choices))
            return true;
        switch (key)
        {
            case Key.Escape:
                return Escape();
            case Key.Down or Key.Up:
                if (choices.Count > 0)
                {
                    var at = _highlighted is null ? -1 : choices.IndexOf(_highlighted);
                    var step = key == Key.Down ? 1 : -1;
                    Highlight(choices[at < 0 ? (step > 0 ? 0 : choices.Count - 1) : (at + step + choices.Count) % choices.Count]);
                }
                return true;
            case Key.Home or Key.End or Key.PageUp or Key.PageDown:
                if (choices.Count > 0)
                    Highlight(key is Key.Home or Key.PageUp ? choices[0] : choices[^1]);
                return true;
            case Key.Right:
                if (!inSub && _highlighted is { } parent && _items[parent].Submenu is not null)
                {
                    OpenSubmenu(parent);
                    Highlight(ButtonsIn(_subBox).FirstOrDefault(button => button.Enabled));
                }
                else
                    SteppedOut?.Invoke(1);
                return true;
            case Key.Left:
                if (inSub && _openItem is { } item)
                {
                    CloseSubmenu();
                    Highlight(item);
                }
                else
                    SteppedOut?.Invoke(-1);
                return true;
            case Key.Enter or Key.Space:
                if (_highlighted is { } chosen)
                {
                    if (!inSub && _items[chosen].Submenu is not null)
                    {
                        OpenSubmenu(chosen);
                        Highlight(ButtonsIn(_subBox).FirstOrDefault(button => button.Enabled));
                    }
                    else
                        chosen.RaiseClicked();
                }
                return true;
            case Key.Tab:
                return true; // focus stays where it is behind the menu
            default:
                if (LetterOf(key) is not { } letter)
                    return false;
                // The next item along starting with the letter, round to the top.
                var from = _highlighted is null ? -1 : choices.IndexOf(_highlighted);
                for (var i = 1; i <= choices.Count; i++)
                {
                    var next = choices[(from + i + choices.Count) % choices.Count];
                    if (TextOf(next).StartsWith(letter, StringComparison.OrdinalIgnoreCase))
                    {
                        Highlight(next);
                        break;
                    }
                }
                return true;
        }
    }

    // On a swatch, the arrows move round its grid: along the row, or up and down its column, and on out of the grid
    // above or below it to the items there. Off the grid's side, Left and Right do as they do anywhere.
    bool StepInGrid(Key key, List<Button> choices)
    {
        if (key is not (Key.Left or Key.Right or Key.Up or Key.Down) || _highlighted is not { } at)
            return false;
        if (FindGrid(at) is not { } grid)
            return false;
        var cells = grid.Cells;
        var index = cells.IndexOf(at);
        var (row, column) = (index / grid.Columns, index % grid.Columns);
        var target = key switch
        {
            Key.Left when column > 0 => index - 1,
            Key.Right when column < grid.Columns - 1 && index + 1 < cells.Count => index + 1,
            Key.Up => index - grid.Columns,
            Key.Down => index + grid.Columns,
            _ => -1,
        };
        if (key is Key.Left or Key.Right)
        {
            if (target < 0)
                return false;
            if (cells[target].Enabled)
                Highlight(cells[target]);
            return true;
        }
        if (target >= 0 && target < cells.Count)
        {
            if (cells[target].Enabled)
                Highlight(cells[target]);
            return true;
        }
        // Down from the bottom row (or onto a gap in it) or up from the top, to the next item outside the grid.
        if (key == Key.Down && row < (cells.Count - 1) / grid.Columns && target >= cells.Count)
        {
            Highlight(cells[^1]);
            return true;
        }
        var outside = choices.Where(button => !cells.Contains(button)).ToList();
        var edge = choices.IndexOf(key == Key.Up ? cells.First(cell => cell.Enabled) : cells.Last(cell => cell.Enabled));
        var next = key == Key.Up
            ? outside.LastOrDefault(button => choices.IndexOf(button) < edge) ?? outside.LastOrDefault()
            : outside.FirstOrDefault(button => choices.IndexOf(button) > edge) ?? outside.FirstOrDefault();
        if (next is not null)
            Highlight(next);
        return true;
    }

    SwatchGrid? FindGrid(Button button) =>
        new[] { _box, _subBox }.SelectMany(box => box.Children.OfType<SwatchGrid>()).FirstOrDefault(grid => grid.Cells.Contains(button));

    // Escape closes the submenu the highlight's in, else the menu.
    bool Escape()
    {
        if (_highlighted is { } item && Buttons(_subBox).Contains(item) && _openItem is { } parent)
        {
            CloseSubmenu();
            Highlight(parent);
        }
        else
            Close();
        return true;
    }

    // The letter a key types, or null if it isn't one.
    static string? LetterOf(Key key)
    {
        var name = key.ToString();
        return name.Length == 1 && char.IsLetter(name[0]) ? name : null;
    }

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
        Highlight(null);
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
        _subBox.Visible ? Buttons(_subBox).Select(TextOf).ToList() : null;

    /// <summary>
    /// The texts of the menu's items, for tests.
    /// </summary>
    public IReadOnlyList<string> ItemTexts => Buttons(_box).Select(TextOf).ToList();

    /// <summary>
    /// Chooses the item with <paramref name="text"/>, as clicking it would, for tests.
    /// </summary>
    public void Choose(string text) => Buttons(_box).First(button => TextOf(button) == text).RaiseClicked();

    void Fill(Panel box, IEnumerable<MenuItem> items)
    {
        foreach (var button in Buttons(box).ToList())
            _items.Remove(button);
        box.Children.Clear();

        // Where any item is ticked, every item's text moves along to leave a column for the ticks, so they line up.
        var list = WithoutStraySeparators(items);
        var tickColumn = list.Any(item => item.Checked && item.Swatch is null) ? _font.Advance("•") + 6 : 0;
        for (var i = 0; i < list.Count; i++)
        {
            var item = list[i];
            if (IsSeparator(item))
            {
                box.Add(new Divider(0));
                continue;
            }
            Button button;
            if (item.Swatch is { } colour)
            {
                // Swatches next to each other share a grid, as near square as they'll go.
                if (i == 0 || list[i - 1].Swatch is null)
                {
                    var count = list.Skip(i).TakeWhile(next => next.Swatch is not null).Count();
                    box.Add(new SwatchGrid((int)MathF.Ceiling(MathF.Sqrt(count))));
                }
                var grid = (SwatchGrid)box.Children[^1];
                if (grid.Cells.Count % grid.Columns == 0)
                    grid.Add(new Row(SWATCH_GAP) { Padding = 0 });
                button = ((Panel)grid.Children[^1]).Add(
                    new Button(_font, "")
                    {
                        Padding = 0,
                        MinSize = new float2(SWATCH, SWATCH),
                        Enabled = item.Enabled,
                        Background = colour,
                        HoverBackground = colour,
                        PressedBackground = Theme.Mix(colour, Colors.White, 0.25f),
                        BorderColor = SwatchEdge,
                        BorderWidth = 1,
                    }
                );
                grid.Cells.Add(button);
            }
            else
            {
                button = box.Add(
                    new Button(_font, item.Text)
                    {
                        TextIndent = tickColumn,
                        Padding = 6,
                        Enabled = item.Enabled,
                        // Lit only while highlighted (see Highlight), so the mouse resting on one the keys have
                        // moved off doesn't leave two lit.
                        Background = Colors.Transparent,
                        HoverBackground = Colors.Transparent,
                        PressedBackground = Theme.Fade(Theme.Accent, 0.5f),
                        FocusColor = Theme.Focus,
                    }
                );
            }
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
        if (_highlighted is not null && Buttons(_subBox).Contains(_highlighted))
            Highlight(null);
        foreach (var button in Buttons(_subBox).ToList())
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
        Highlight(null);
    }

    public override float2 Measure()
    {
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
        foreach (var grid in box.Children.OfType<SwatchGrid>())
            grid.MinSize = new float2(width, 0);
        box.Measure();
    }

    /// <summary>
    /// A menu's swatches, in rows of <see cref="Columns"/>, centred across the menu's width (its
    /// <see cref="Panel.MinSize"/>), with a gap above and below.
    /// </summary>
    sealed class SwatchGrid : Panel
    {
        float _contentWidth;

        public SwatchGrid(int columns)
        {
            Columns = Math.Max(1, columns);
            Spacing = SWATCH_GAP;
        }

        public int Columns { get; }

        public List<Button> Cells { get; } = [];

        public override float2 Measure()
        {
            var content = MeasureChildren();
            _contentWidth = content.X;
            return Size = float2.Max(content + new float2(0, 2 * SWATCH_GAP), MinSize);
        }

        public override void Arrange(float2 position)
        {
            ArrangeSelf(position);
            ArrangeChildren(position + new float2(MathF.Round(MathF.Max(0, Size.X - _contentWidth) / 2), SWATCH_GAP));
        }
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
            // A ticked swatch: a dot in its middle, light or dark to stand out on the colour.
            foreach (var button in Buttons(box))
            {
                if (_items[button] is not { Checked: true, Swatch: { } colour })
                    continue;
                var light = 0.2126f * colour.X + 0.7152f * colour.Y + 0.0722f * colour.Z > 0.35f;
                var centre = button.ScreenPosition + button.Size / 2;
                batch.AddSmoothCircle(centre, 5, light ? Theme.OnColour : Colors.White);
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
