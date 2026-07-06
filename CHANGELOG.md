# Changelog

All notable changes to RoRoRo Ur AFK are documented here. Format roughly follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/); versioning follows [SemVer](https://semver.org/).

## 0.4.0 — 2026-07-06

### Added

- **Drag the pill anywhere.** Grab it by the body and put it where your eyes actually are — ultrawide corners are a long way away. The position sticks between sessions; picking a corner in Notifications snaps it back to the preset. Positions clamp to the screen, so a resolution change can never strand the pill somewhere invisible.
- **Pill size slider.** 0.75× to 2× under Notifications, applied live.
- **Stats on the pill.** A second line under the status: worst idle time across your enabled accounts and how many grabs have fired this session ("idle 7m · 4 grabs").

Same host requirement — RoRoRo 1.8.0.0+.

## 0.3.0 — 2026-07-06

### Added

- **You can see it coming — and see it land.** The status surfaces now escalate through the whole takeover: an amber **pulsing** dot with a live countdown before a grab ("Grabbing X in 3… · F8 skips"), solid magenta while it fires, then a cyan "✓ Kept X active" confirmation held for ~3 seconds before returning to watching. Shown in a new full-width status banner in the main window and mirrored on the floating pill.
- **The floating pill is now the compact mode.** Hover it and quick controls appear: keep-active on/off (state-colored), skip the next grab, and open the main window. Close the main window (it hides to the tray) and the pill is the whole UI — an indicator light with just the buttons needed to manage it.
- **Flipping Keep-active now answers instantly.** The toggle previously only persisted a setting; nothing visibly changed until the next poll cycle woke up, which read as a dead button. The pill and banner now react the moment you flip it.

### Changed

- **Full family chrome.** Custom navy title bar (drag, minimize, close-to-tray) replaces the native white one, and every control is themed — toggle, text box, checkboxes, dropdowns, expander, slider, menus, scrollbars. The tray menu is themed too, and the tray icon finally uses the real Ur AFK mark instead of the v0.1 vector placeholder.

Same host requirement as 0.2.0 — RoRoRo 1.8.0.0+.

## 0.2.0 — 2026-07-06

### Added

- **Ur AFK follows your RoRoRo theme — live.** The plugin reads the host's active theme (built-ins and custom theme files alike) straight from RoRoRo's settings on disk and re-paints itself, including while both apps run: switch themes in RoRoRo and Ur AFK follows in about two seconds. The activity pill's status dot follows too — its colors were previously frozen hard-coded hexes that could never re-theme. Amber (the consent-revoked state) tracks the host theme's accent slot. No RoRoRo installed → the plugin keeps its brand look. One visual note under the default theme: muted text lightens to the family brand values — intentional, Ur AFK now paints with the exact same palette as RoRoRo, Ur Task, and Ur OCR.
- **Crash diagnostics.** The plugin now leaves evidence when things go wrong: an append-only rolling log at `%LOCALAPPDATA%\626labs.ur-afk\logs\ur-afk.log` with a session header, per-step startup breadcrumbs, connection events, and log-then-crash-loud exception handlers. A startup watchdog reports exception-free windowless hangs — the failure mode handlers structurally can't see. The tray menu gains **Open log folder**; a clean session always ends with an "exiting cleanly" line, so its absence *is* the evidence. The very first instrumented run already earned its keep, surfacing a previously invisible "plugin not installed" handshake rejection.

### Changed

- **Plugin contract bumped 0.3.0 → 0.4.0.** Additive game-identity fields (place id + name per account) — nothing about today's behavior changes, but it stages game-aware keep-alive options and keeps the whole plugin family on one contract.
- README's Install section now points at GitHub Releases (it still claimed no release existed).

Same host requirement as 0.1.x — RoRoRo 1.8.0.0+.

## 0.1.1 — 2026-07-03

### Fixed

- **Real plugin icon.** v0.1.0 shipped with Ur Task's icon (a byte-identical placeholder that never got swapped before release). Ur AFK now has its own mark: the family hexagon with a heartbeat pulse over a keyboard, in the 626 Labs cyan/magenta on the shared swoosh. Part of the unified Ur family icon set (Ur OCR = scan, Ur Task = record/replay, Ur AFK = keep-alive). No code changes, same host requirement as 0.1.0 (RoRoRo 1.8.0.0+).

## 0.1.0 — 2026-07-02

### Added

- **First release.** Keeps idle RoRoRo-managed accounts alive: watches per-account idle time via the host's `GetAccountActivity` query (RoRoRo 1.8.0.0+), and right before Roblox's ~20-minute idle kick, briefly focuses that account's window, taps Space, and hands focus back. Countdown activity pill (main window + floating badge) so a focus-steal is never a surprise; F8 skips a pending grab. Four disclosed capabilities on the consent sheet: synthesize keyboard input, watch account launch/exit, read account idle time. Never touches the mouse, never reads keystrokes, never touches a captcha.
