# Changelog

All notable changes to RoRoRo Ur AFK are documented here. Format roughly follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/); versioning follows [SemVer](https://semver.org/).

## 0.1.1 — 2026-07-03

### Fixed

- **Real plugin icon.** v0.1.0 shipped with Ur Task's icon (a byte-identical placeholder that never got swapped before release). Ur AFK now has its own mark: the family hexagon with a heartbeat pulse over a keyboard, in the 626 Labs cyan/magenta on the shared swoosh. Part of the unified Ur family icon set (Ur OCR = scan, Ur Task = record/replay, Ur AFK = keep-alive). No code changes, same host requirement as 0.1.0 (RoRoRo 1.8.0.0+).

## 0.1.0 — 2026-07-02

### Added

- **First release.** Keeps idle RoRoRo-managed accounts alive: watches per-account idle time via the host's `GetAccountActivity` query (RoRoRo 1.8.0.0+), and right before Roblox's ~20-minute idle kick, briefly focuses that account's window, taps Space, and hands focus back. Countdown activity pill (main window + floating badge) so a focus-steal is never a surprise; F8 skips a pending grab. Four disclosed capabilities on the consent sheet: synthesize keyboard input, watch account launch/exit, read account idle time. Never touches the mouse, never reads keystrokes, never touches a captcha.
