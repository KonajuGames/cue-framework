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
| `Cue.Framework.UI` | the retained-mode UI: `Element`, `Panel`, `Label`, `Button`, `Root` (input routing, focus, tooltips, drag and drop), `Batch` and `OverlayRenderer` (drawing), `FontAtlas` and `FreeType`, `Png`, the shared `Theme`, keys and modifiers, and the controls: `TextBox`, `Slider`, `Knob`, `Checkbox`, `Dropdown`, `ListView`, `TreeView`, `ScrollPanel`, `ContextMenu`, `ConfirmDialog`, `NamePrompt`, `LevelMeter`, `IconButton`, `Spacer`, `Picture`, `TabView`, `NumberBox` and `ColourPicker` (with `Srgb`, the colour it picks); and layout: `Row`, `Ends`, `Flow`, `FieldGrid`, `Divider`, `FixedWidth`, and `Region`, `ScrollingPage` and `Placeholder` for screens that fill the room they're given |
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

- its title bar, main screen, `Images` (the logo and icons are embedded in the app, read by name) and `AppTheme`
  (the colours only it uses, beside the shared `Theme`);
- its own GPU drawing, given to the renderer as an `IPrePass` (Cuedeck's 3D visualizer is its `ScenePass`).

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
- `TitleBar.cs` and `Images.cs` stayed in the apps, apart from `IconButton`, `TitleIcon`, `Spacer` and `Picture`.
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
