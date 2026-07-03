# Theming sync with the RoRoRo host — implementation notes

Ur Task v0.5 made the plugin follow the host's active theme live, with **zero
plugin-contract changes and no pipe traffic** — everything reads from disk.
These notes carry that playbook to ur-afk. Reference implementation:
[`rororo-ur-task/src/Theming/HostThemeReader.cs` + `HostThemeService.cs`](https://github.com/estevanhernandez-stack-ed/rororo-ur-task/pull/18)
(+ `HostThemeReaderTests.cs`). The reader is UI-free and ports verbatim; the
apply layer needs the mapping below because ur-afk's v0.1 palette predates the
host slot names.

## What the host persists (and where)

| Thing | Path | Format |
|---|---|---|
| Active theme id | `%LOCALAPPDATA%\ROROROblox\settings.json` | camelCase — `"activeThemeId": "midnight"` |
| User themes | `%LOCALAPPDATA%\ROROROblox\themes\<id>.json` | snake_case slots (`muted_text`, `row_bg`, …); comments + trailing commas tolerated |
| Built-in themes | compile-time constants in `ROROROblox.Core` `ThemeStore.BuildBuiltIns()` | `brand`, `midnight`, `magenta-heat` — **must be mirrored in plugin code** |

## Slot mapping for ur-afk (v0.1 palette)

`src/App.xaml` currently carries five inline brushes with pre-slot names:

| App.xaml key | Host theme slot | Note |
|---|---|---|
| `NavyFieldBrush` | `bg` | direct hex match today (#0F1F31) |
| `CyanBrush` | `cyan` | 1:1 |
| `MagentaBrush` | `magenta` | 1:1 |
| `AmberBrush` | `row_expired_accent` | direct hex match today (#F1B232) |
| `MutedGreyBrush` | `muted_text` *(judgment call)* | current #4A5C70 is darker than host muted_text #9AA8B8 — decide at impl time whether it's a text role (map it) or a structural grey (leave static) |

**When the Task-12 chrome pass lands, adopt the host slot names outright**
(`BgBrush`, `CyanBrush`, `MagentaBrush`, `WhiteBrush`, `MutedTextBrush`,
`DividerBrush`, `RowBgBrush` + derived `RowHoverBrush`) so this table collapses
to the 1:1 mapping Ur Task and Ur OCR use.

## Resolve algorithm

1. Read `activeThemeId` from `settings.json`. Missing file / field / bad JSON → **Brand**.
2. Case-insensitive match against the mirrored built-ins → use the mirror.
3. Else parse `themes\<id>.json` (snake_case). Missing file / malformed / missing slot → **Brand**.

Every failure path lands on Brand — a hand-edited host file must never break
the plugin, and the plugin stays fully usable standalone (host not installed →
Brand, which matches the XAML defaults).

## Apply strategy — mutate brushes, don't sweep to DynamicResource

`App.xaml`'s brushes are plain **unfrozen** `SolidColorBrush`. Setting
`brush.Color = newColor` re-renders every `{StaticResource}` consumer — so the
entire XAML surface re-themes with **no DynamicResource sweep**. Preconditions:

- Keep the brushes plain `SolidColorBrush` — no `PresentationOptions:Freeze`.
- If an entry comes back frozen or non-brush, fall back to replacing the
  dictionary entry (replacement only propagates to DynamicResource consumers,
  so treat it as a defensive fallback, not the main path).
- Marshal applies to the dispatcher; skip slots whose hex fails to parse
  (keep the current color rather than painting black).
- ur-afk extra: `PillBrushConverter` (and the floating pill window) — if any
  converter returns brushes built from hard-coded hexes rather than the
  resource dictionary, route it through the dictionary brushes or it won't
  follow the theme.

## Live follow

One `FileSystemWatcher` on `%LOCALAPPDATA%\ROROROblox`, filter `*.json`,
`IncludeSubdirectories = true` — catches both `settings.json` and
`themes\*.json` in one subscription. Host saves are tmp-write + rename bursts,
so debounce ~300ms (DispatcherTimer) and marshal watcher events to the UI
thread. Watching is best-effort: folder missing → apply once and skip watching.

## Wiring points (this repo)

- Port `HostThemeReader` + `HostThemeService` into `Labs626.UrAfk.Theming`.
- `src/App.xaml.cs OnStartup`: start the service right after
  `base.OnStartup(e)`, before `MainWindow` / `FloatingPillWindow` construction
  so the first render is already themed.
- Dispose it in `OnExit`.

## Named risk — built-in mirror drift

The three built-in palettes are compile-time constants in `ROROROblox.Core`,
mirrored in plugin code. If the host's built-ins change, every plugin mirror
needs a matching bump. Cheap future fix host-side: publish built-ins as JSON in
the themes folder so plugins read them like user themes.

## Tests to port

`HostThemeReaderTests` from Ur Task: settings parse (camelCase), theme-file
parse (snake_case + comments), built-in resolve, user-file resolve, Brand
fallbacks (missing settings / unknown id / missing folder), blend determinism.
All pure — no WPF Application needed.
