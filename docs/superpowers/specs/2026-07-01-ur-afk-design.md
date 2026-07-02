# RoRoRo Ur AFK — keep-active plugin design

---
**Date:** 2026-07-01
**Status:** Approved (brainstorm complete) — ready for implementation plan
**Author:** The Architect + Este
**Product:** `rororo-ur-afk` · display "RoRoRo Ur AFK" · plugin id `626labs.ur-afk` · new standalone repo
**Scope:** Part B of the "stay active" feature. Part A (core activity awareness) shipped in ROROROblox PR #31 — merged to main 2026-07-01.
**Related:** ROROROblox `docs/superpowers/specs/2026-07-01-rororo-activity-awareness-design.md` (Part A), ROROROblox `docs/plugins/AUTHOR_GUIDE.md`, template plugin `rororo-ur-task` (sibling repo)
---

## 1. Problem & context

Roblox clients idle-time-out at roughly 20 minutes. Accounts launched together by RoRoRo hit that timeout together, and their client-driven auto-reconnects fire as a synchronized wave — which trips Roblox's trust gate (captcha / 403 soft-lock). RoRoRo core cannot harden the reconnect (it has no hook into a running client), and by the project wall it will never synthesize input. The durable fix is keeping accounts *active* so the timeout never fires.

The work split in two:

- **Part A (shipped, ROROROblox core):** *awareness.* Core tracks per-account time-since-last-input (foreground + `GetLastInputInfo`, no keystroke hook), surfaces it in RoRoRo's UI, and exposes it to plugins via the consent-gated `GetAccountActivity` gRPC query (`host.queries.account-activity`, contract 0.3.0).
- **Part B (this spec):** *acting.* A consent-gated plugin that consumes that signal and, for each enabled account idle past a threshold, briefly focuses the account's window and taps Space (jump) — resetting the idle clock.

**Positioning:** ur-AFK is the no-frills sibling of `rororo-ur-task`. ur-task is the power-user macro suite (record/playback/sequences) whose blind keep-alive round-robins every assigned alt on demand. ur-AFK does exactly one thing — keep AFK accounts alive — driven by real idle data, with a two-controls-per-account UI. Power users stay on ur-task; everyone else installs ur-AFK.

**Wall framing:** this is the *sanctioned* input-automation path. The "no macros" wall is core-only; plugins declare `system.synthesize-*` capabilities, disclosed on the consent sheet and granted explicitly by the user. ur-AFK synthesizes exactly one input: Space. No mouse, no recording, no sequences, and never anything that evades a Roblox trust gate.

## 2. Goals & non-goals

**Goals:**

1. Keep enabled accounts from reaching the ~20-minute idle timeout, with one Space tap per account per idle period.
2. Never surprise the user: a visible activity pill announces state and counts down before any focus grab; the user's own focus is restored after each jump.
3. Desynchronize batch-launched accounts over time (sequential acting + per-account timing jitter) so the plugin never recreates the synchronized-input wave it exists to prevent.
4. Hold one hard safety invariant: input is only ever synthesized into a verified target Roblox window (§6).
5. All decision logic unit-testable behind fakes; Win32 interop stays thin.

**Non-goals (v0.1):**

- No mouse input, macro recording, playback, sequences, or OCR — that is ur-task's territory.
- No per-account thresholds (global threshold + per-account on/off only).
- No captcha interaction of any kind — auto-solving or assisted-solving is trust-gate evasion and is permanently out, not deferred.
- No launch de-staggering (a RoRoRo-core lever, tracked in ROROROblox followups §7).
- No background/`PostMessage` input injection — unreliable for games and shaped like evasion; foreground `SendInput` only.

## 3. How it works — the keep-active loop

Every **poll interval (60s)** the plugin calls `GetAccountActivity` and computes the **due set**: enabled accounts where `seconds_since_activity ≥ threshold + jitter[account]`.

The due set is processed **sequentially, one account at a time**:

1. **Pre-grab countdown** — the activity pill flips to PRE-GRAB and counts down the configured lead time (default 3s). The skip hotkey during the countdown skips this one grab.
2. **Capture** the user's current foreground window (hwnd).
3. **Focus** the target account via `Win32Focus.AttachAndFocus(pid)` (AttachThreadInput + SetForegroundWindow — ur-task's proven helper).
4. **Settle** ~1s, then **verify** the foreground window now belongs to the target pid. If it does not — skip the input entirely (§6).
5. **Tap Space** — VK_SPACE down, 50ms hold, up, via `SendInput`.
6. **Restore** the captured foreground window (steal + restore).
7. **Re-roll** that account's jitter; ~1s gap; next due account.

Each jump focuses the window and sends input, so Part A's monitor stamps the account active and its `seconds_since_activity` resets — the account leaves the due set until it idles again. **`GetAccountActivity` is the single source of truth; the plugin keeps no idle timers of its own.**

**Desync model:** sequential pacing means each account resets ~2s after the previous, and the per-account jitter (random 0–90s on top of the threshold, re-rolled after every jump) actively spreads accounts that were launched together. Jitter is timing-only — it shifts *when* the one jump fires, never adds movement.

## 4. Activity pill & notifications

The focus grab must never feel like malware. The pill makes the plugin's state — and its imminent intent — always visible.

**Two placements:** a status pill in the main window header, and a small **always-on-top floating pill** (configurable screen corner), because the plugin's whole point is running while the user is elsewhere — the warning has to reach them with the window minimized to tray.

**States (626 palette, semantic):**

| State | Color | Copy |
|---|---|---|
| OFF (master off) | grey | "Keep-active off" |
| WATCHING (on, nothing due) | cyan | "Active · watching 6 accounts" |
| PRE-GRAB (countdown) | magenta, pulsing | "Grabbing *DisplayName* in 3… 2… 1" |
| GRABBING (mid-jump, ~1-2s) | magenta solid | "Keeping *DisplayName* active…" |

Cyan = safe/active, magenta = about to touch your screen. After restore, the pill drops back to cyan.

**Customizable notifications (settings panel):**

- **Warning lead time:** 0–10s before a grab (0 = no countdown; default 3s).
- **Pill mode:** always visible / only on pre-grab / off.
- **Floating pill position:** screen corner picker.
- **Sound on grab:** on/off soft blip when focus is taken.
- **Skip hotkey:** bindable key that skips the imminent grab during its countdown (that account may then idle out — user's informed choice; default behavior is warn-then-grab so the plugin's one job still gets done).

## 5. Architecture & components

New standalone WPF EXE, cloned from ur-task's plugin skeleton, macro suite dropped entirely.

**Cloned from `rororo-ur-task` (minimal tweaks):**

- `PluginClient` — named-pipe gRPC channel (`rororo-plugin-host`), handshake (`contract_version: "1.0"`), `GetRunningAccounts` seed, `SubscribeAccountLaunched`/`Exited` streams.
- `HeaderInjectingCallInvoker` — injects `x-plugin-id` so the host's `CapabilityInterceptor` gates every call.
- `Win32Focus.AttachAndFocus(pid)` — the AttachThreadInput focus dance.
- `AccountRegistry` — pid ↔ account mapping, fed by the seed + event streams.
- `ForegroundWatcher` — foreground pid resolution, used for the §6 verify step.
- App startup shape (`App.xaml.cs`: runtime → ViewModel → window → async connect, connection errors surfaced without blocking).

**New (small, single-purpose):**

- **`KeystrokeSender`** — `TapSpace()` only: VK_SPACE down → 50ms → up via `SendInput`. Extracted from ur-task's inline keep-alive; the only input synthesis in the codebase.
- **`FocusRestorer`** — capture foreground hwnd → act → restore. Owns steal-and-restore.
- **`KeepActiveService`** — the engine (replaces ur-task's 484-line `PluginRuntime` with a focused loop). Owns: enabled-account map, global threshold, per-account jitter, the poll → due-set → sequential-act cycle, and the pill state machine. All dependencies injected (`IHostActivityQuery`, `IWindowFocus`, `IKeystrokeSender`, `IForegroundProbe`, `IClock`) so every decision is unit-testable.
- **`PillViewModel` + floating pill window** — renders the §4 state machine.
- **Settings store** — JSON in `%LOCALAPPDATA%\626labs.ur-afk\settings.json`: master toggle, threshold, per-account enables, notification prefs, hotkey.

**Dropped from ur-task (not carried):** MacroRecorder/Store/Player, sequences, SequencePlayer, hotkey suite (Ctrl+Shift+R/P/A), IPC action-bridge, OCR integration, AutoStopCoordinator, the 9-row recorder UI.

## 6. The safety invariant

**Never synthesize input unless the verified foreground window belongs to the target account's pid.**

After `AttachAndFocus` + settle, re-resolve the foreground pid (`ForegroundWatcher`). If it is not the target — the user clicked mid-grab, Windows refused focus, the window closed — **skip the Space entirely**, log it, restore the user's focus, and move on. A skipped jump self-heals next cycle; a spacebar landing in the user's Discord/browser/editor is never acceptable. This check is the plugin's most important line of code and gets first-class test coverage.

## 7. UI

One window, deliberately minimal:

- **Master toggle** — "Keep-active ON/OFF." Defaults **OFF on first run** (never acts until explicitly enabled); restart persistence per the paragraph below.
- **Global threshold** — minutes control, default 15.
- **Account list** — one row per managed account: display name · live "idle 12m" readout (last poll) · per-account on/off toggle · "kept active 2m ago" line.
- **Activity pill** in the header (+ the floating pill).
- **Settings panel** — the §4 notification prefs.
- **Connection footer** — connected / disconnected / consent revoked / host too old.

Runs in the tray (Hardcodet, like ur-task). 626 brand chrome and icon go through the `626labs-design` skill before ship — clan-facing surface, no programmatic placeholders.

**Running vs acting:** `autostartDefault: on` launches the *process* with RoRoRo, but the **master toggle defaults OFF** — ur-AFK never synthesizes input until the user explicitly enables it. Whether the master toggle's state persists across restarts or always resets to OFF is an implementation-plan decision leaning **persist** (an AFK tool you must re-arm every launch fails its one job); the pill makes the armed state visible either way.

## 8. Manifest & consent

```json
{
  "schemaVersion": 1,
  "id": "626labs.ur-afk",
  "name": "RoRoRo Ur AFK",
  "version": "0.1.0",
  "contractVersion": "1.0",
  "publisher": "626 Labs LLC",
  "description": "Keeps idle Roblox accounts alive — focuses each and taps space before the ~20-minute idle timeout.",
  "capabilities": [
    "system.synthesize-keyboard-input",
    "host.events.account-launched",
    "host.events.account-exited",
    "host.queries.account-activity"
  ],
  "icon": "icon.png",
  "autostartDefault": "on",
  "minHostVersion": "1.8.0.0",
  "entrypoint": "626labs.ur-afk.exe"
}
```

- **Exactly four capabilities** — the jump key, the account lifecycle events, and the idle signal. No mouse, no `watch-global-input`, no `host.ui.*`.
- **`minHostVersion: 1.8.0.0`** gates ur-AFK to a RoRoRo build that serves `GetAccountActivity` (adjust to the actual version tag of the release carrying Part A).
- The host's consent sheet shows all four capabilities with their honest descriptions on first connect; grant once.
- NuGet dependency: `ROROROblox.PluginContract` **0.3.0** (ur-task is on 0.1.0 — ur-AFK needs the new query; see §12 on how the package is sourced).

## 9. Tuning defaults

All user-adjustable; these are the shipped values.

| Knob | Default | Why |
|---|---|---|
| Poll interval | 60s | One cheap unary call; a 20-minute-scale signal needs no more |
| Idle threshold | 15 min | ~5-minute margin before the ~20-min timeout; matches core's warn default |
| Jitter | random 0–90s per account, re-rolled after each jump | Timing-only desync; never extra movement |
| Focus settle | ~1s | ur-task's proven cadence |
| Space hold | 50ms | ur-task's proven value |
| Inter-account gap | ~1s | Sequential pacing that drives natural desync |
| Pre-grab lead | 3s (0–10s) | Enough warning to skip, short enough to not delay the job |

**Documented interplay:** with both defaults at 15 min, RoRoRo's idle toast and ur-AFK's jump land around the same moment. Once ur-AFK is trusted, mute idle alerts in RoRoRo (that's exactly what the mute setting is for). Not a bug; noted so nobody "fixes" it.

## 10. Error handling

- **Pipe dead / host exits:** pill → grey "disconnected," acting stops, reconnect with backoff. Resume cleanly when the host returns.
- **`PermissionDenied` mid-run (consent revoked):** stop acting immediately, footer shows "consent revoked — re-grant in RoRoRo → Plugins."
- **Host too old:** handshake/`minHostVersion` gate; footer explains "requires RoRoRo ≥ 1.8."
- **Focus refused / foreground verify fails:** skip the input (§6), restore, log, retry next cycle.
- **Account exits mid-cycle:** registry drops it; due-set skips it.
- **`SendInput` rejected:** log, no immediate retry; next cycle.
- **Focus-restore fails:** best effort, log once — never fight the user for focus in a loop.

## 11. Testing

- **Unit (xUnit, hand-rolled fakes):** due-set computation (threshold + jitter boundary), jitter re-roll after jump, sequential scheduler ordering and pacing, pill state machine (OFF/WATCHING/PRE-GRAB/GRABBING transitions + skip), countdown/skip behavior, **the §6 safety invariant** (foreground-verify fails → no keystroke sent), consent-revoked and disconnected transitions, settings round-trip.
- **Not unit-tested:** the thin Win32 layer (`AttachAndFocus`, `SendInput`, foreground probe) — verified by manual smoke on real accounts on a live desktop.
- **No automation against live roblox.com** (repo-family policy — bot-flag risk).
- Manual smoke script: 2+ accounts, enable one, idle past threshold, observe countdown → grab → space → restore → idle readout resets; verify skip hotkey; verify the invariant by stealing focus mid-grab.

## 12. Open questions / follow-ups

- **Contract package sourcing:** ROROROblox bumped `ROROROblox.PluginContract` to 0.3.0 in-repo, but whether it is published to a feed ur-AFK can reference (nuget.org vs a local package source vs ProjectReference during development) is a plan-phase decision. Development can start on a local package/ProjectReference; publishing is required before ur-AFK ships to the clan.
- **`minHostVersion` value:** pinned as 1.8.0.0 pending the actual version tag of the RoRoRo release that carries Part A.
- **Master-toggle persistence:** leaning persist-across-restarts (§7); confirm in plan.
- **Jitter range (0–90s) and poll (60s)** are reasoned starting points, not field-measured; revisit with real usage.
- **Per-account thresholds:** deferred until someone actually asks.
- **Distribution:** GitHub release with the host's SHA-verified install flow (manifest.json + manifest.sha256 + plugin.zip, per AUTHOR_GUIDE). Release engineering lands in the plan.
- **626 dashboard project:** new repo — offer to create/bind a dashboard project at first real work session (zero-match rule: ask, never auto-create).

## 13. Decisions & rationale

| Decision | Choice | Why |
|---|---|---|
| Name | ur-AFK (`626labs.ur-afk`) | Este's call — names the exact use case, fits the Ur-family (ur-task, Ur-OCR) |
| Idle source | `GetAccountActivity` poll, no local timers | Part A is the single source of truth; the jump's own input closes the feedback loop |
| Anti-collision | Sequential acting + per-account timing jitter (0–90s, re-rolled) | One-at-a-time pacing self-desyncs; jitter covers the first post-batch cycle. Jitter is timing-only — never moves the player beyond the one jump |
| Focus behavior | Steal + restore | Reliable input needs foreground; restoring the user's window makes it a ~1s flicker instead of a hijack |
| Grab transparency | Activity pill + pre-grab countdown + customizable notifications | A focus-steal with no warning feels like malware; visible intent + a skip hotkey keeps the user in control |
| Input scope | Space only, foreground only | Minimum viable "I'm still here"; no PostMessage/background injection |
| Safety | Foreground-verified-or-skip invariant | A stray keystroke into the wrong app is never acceptable; a skipped jump self-heals |
| UI | Two controls per account | The whole product thesis vs ur-task's macro suite |

## References

- Part A design (ROROROblox): `docs/superpowers/specs/2026-07-01-rororo-activity-awareness-design.md`
- Plugin author guide (ROROROblox): `docs/plugins/AUTHOR_GUIDE.md` — includes the `host.queries.account-activity` entry
- Template plugin: `rororo-ur-task` (sibling repo) — `PluginClient.cs`, `HeaderInjectingCallInvoker.cs`, `Win32Focus.cs`, `AccountRegistry.cs`, `ForegroundWatcher.cs`, `manifest.json`, and `AssignmentRunner.cs`'s keep-alive sequence (the extracted primitive)
- OCR sibling (reference only): `Ur-OCR`
