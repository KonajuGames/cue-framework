# cue-framework

The window, rendering and UI code the Cue apps share: [Cuepoint](../cuepoint-dj-controller) (DJ software) and
[Cuedeck](../cuedeck-lighting-controller) (a lighting controller). It's a C#/.NET 10 class library on BRUTAL's GLFW,
Vulkan and FreeType bindings, Windows-only like the apps.

## What's in it

`Cue.Framework/`, one project, one namespace for each part:

| Namespace | What |
| --- | --- |
| `Cue.Framework.Window` | `GlfwUiInput` (GLFW's mouse, keys and cursors to the UI's `Root`), `WindowChrome` (the borderless window's Win32 caption and resizing), `WindowsShell` (console, crash box, file dialogs, Recycle Bin, keep-awake) |
| `Cue.Framework.Rendering` | the Vulkan context, swapchain, `Renderer` (clears a window and draws the UI over it, two frames in flight), GPU buffers and images, barriers, the GLSL loader, and `IPrePass` for an app's own drawing before the UI |
| `Cue.Framework.UI` | the retained-mode UI: `Element`, `Panel`, `Label`, `Button`, `Root` (input routing, focus, tooltips, drag and drop), `Batch` and `OverlayRenderer` (drawing), `FontAtlas` and `FreeType`, `Png`, the shared `Theme`, keys and modifiers, and the controls: `TextBox`, `Slider`, `Knob`, `Checkbox`, `Dropdown`, `ListView`, `TreeView`, `ScrollPanel`, `ContextMenu`, `MenuBar` (see below), `ConfirmDialog`, `NamePrompt`, `LevelMeter`, `TitleBar` (see below), `IconButton`, `Spacer`, `Picture`, `TabView`, `NumberBox` and `ColourPicker` (with `Srgb`, the colour it picks); and layout: `Row`, `Ends`, `Flow`, `FieldGrid`, `Divider`, `FixedWidth`, and `Region`, `ScrollingPage` and `Placeholder` for screens that fill the room they're given |
| `Cue.Framework` | `ErrorLog` |

`Shaders/ui.vert` and `ui.frag` are copied next to the app's executable, into its `Shaders` folder, and compiled at
startup.

## Using it from an app

The apps reference the project from a sibling checkout, so clone this beside them:

```
F:\git\cue-framework
F:\git\cuepoint-dj-controller
F:\git\cuedeck-lighting-controller
```

```xml
<ProjectReference Include="..\..\cue-framework\Cue.Framework\Cue.Framework.csproj" />
```

and import its namespaces everywhere, as the apps do, with `<Using Include="Cue.Framework.UI" />` and the rest. The
BRUTAL packages come with it, so an app lists only the ones it uses on top (SDL, logging).

What stays in each app:

- its main screen, `Images` (the logo and icons are embedded in the app, read by name) and `AppTheme` (the colours
  only it uses, beside the shared `Theme`);
- what it puts in the title bar (below);
- its own GPU drawing, given to the renderer as an `IPrePass` (Cuedeck's 3D visualizer is its `ScenePass`).

### The title bar

`TitleBar` is the window's own bar: the logo, the app's name as a wordmark in a colour for each part
(`new TitleBar(body, bold, logo, cog, ("Cue", Theme.Accent), ("deck", Theme.Text))`) and an optional `Subtitle` on
the left; the clock, the options button and the window's buttons on the right. An app adds its own widgets to:

- `Centre`: the middle, centred between the sides or moved left to keep clear of the right;
- `Status`: indicators before the clock (labels there have their capitals centred on the bar, as the clock's are);
- `Tools`: icon buttons before the options button.

`FitCentre` is called with the room the middle has, to shorten what's in it on a crowded bar, and `Arranged` is raised
once everything's placed, for last nudges (`TitleBar.CentreCapitals`). The app updates its widgets each frame beside
the bar's own `Update`. Cuedeck leaves them empty; Cuepoint's `TitleBarItems` fills them.

### Menus

`MenuBar` is a row of menu titles with drop-downs, which open on the app's one `ContextMenu` (the same one its
dropdowns and right-click menus use, added to the root last). Each menu is a title and a function giving its items,
called each time it opens, so items can be enabled, ticked or left out by what's going on:

```csharp
var bar = new MenuBar(font, contextMenu) { Width = screenWidth };
bar.AddMenu("File", () => [
    new("Save", save, Shortcut: "Ctrl+S"),
    MenuItem.Separator,
    new("Exit", exit),
]);
```

Clicking a title opens its menu below it; with one open, moving onto another title opens that one; clicking the open
title or anywhere else puts it away. A `MenuItem`'s `Shortcut` is only shown, at the right: the app handles the key.
`MenuItem.Separator` draws a line (one at either end or beside another is left out). `ContextMenu.Owner` says what
opened the menu showing, and `PointerMoved` reports the mouse while it's open, which is how the bar follows it.
Cuedeck's bar sits under its title bar (`UI/AppMenus.cs`).

The menus work from the keyboard with no wiring by the app. Alt and a title's access letter (its first letter no
title before it has taken) opens that menu, and F10 the first, with the first item that can be chosen highlighted.
The access letters are underlined while Alt is held on its own, and while a menu the keys opened is open: `Root`
learns what's held from `ModifiersHeld`, which `GlfwUiInput` calls on every key, and tells each element's
`ModifiersChanged`. In any open `ContextMenu` (a bar's, a dropdown's or a
right-click one), Up, Down, Home and End move the highlight, a letter moves it to the next item starting with it,
Enter or Space chooses, Right and Left open and close a submenu, and Escape closes it. Left and Right past the ends
of a bar's menu (`ContextMenu.SteppedOut`) open the menu beside it. A dropdown opens with its choice highlighted.

This works through `Element.PreviewKeyDown`, which `Root` asks every visible element (a panel asks its children),
topmost first, before the focused element gets a key, so a menu has its keys whatever has focus, a text box
included. `Key` now has every letter and F10.

### Names

A name in the global namespace beats one a `using` brings in, so in an app with its own type called `Cue` (Cuedeck's
lighting cues), `Cue` means this namespace. A file that uses that type aliases it after its namespace line:
`using Cue = Cuedeck.Show.Cue;`.

## Where it came from

The code was copied from Cuedeck at `04a3cdd`, whose copy was Cuepoint's (at `6f1ee3e`) with additions; Cuedeck's
[FRAMEWORK.md](../cuedeck-lighting-controller/FRAMEWORK.md) lists them. Changed on the way:

- Types are `public`, being in a library now, as are a few members the apps and their tests use
  (`Element.RaiseClicked`, `Element.VisibleChildren`, `Font.GetGlyph`, `ContextMenu.ItemTexts` and `Choose`).
- `Renderer` no longer draws Cuedeck's 3D scene itself: it records an `IPrePass` before the UI, if it's given one.
- `Theme` keeps the colours both apps use; the rest moved to each app's `AppTheme`.
- `Images.cs` stayed in the apps, apart from `Picture`. The title bar came over after, as the frame both apps' bars
  shared with places for each app's widgets; the clock's capitals are centred now in Cuedeck too, as Cuepoint's were.
- The `Input` namespace merged into `Cue.Framework.UI`, and the window code moved into `Cue.Framework.Window`.
- `WindowsShell.ShowFatalError` titles its box with the app's product name.
- Cuedeck's general-purpose widgets came over after (`Layout.cs`, `TabView`, `NumberBox`, `ColourPicker`). The picker
  dealt in Cuedeck's show colour, `Rgb`; it deals in `Srgb` now, with the same Godot sums, and Cuedeck's `Rgb`
  converts to and from it. Its hex box takes hex codes only (Cuedeck's took a six-letter colour name too).

## Building

```
dotnet build Cue.Framework.slnx
```

It has no tests of its own; the apps' tests drive it through their screens.
