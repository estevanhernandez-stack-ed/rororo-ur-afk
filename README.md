# RoRoRo Ur AFK

> Keeps your idle Roblox alts from timing out while you're off doing literally anything else. A [RoRoRo](https://github.com/estevanhernandez-stack-ed/ROROROblox) plugin.

Roblox boots you out of a client after roughly 20 minutes of nobody touching it. If you've got a wall of alts open in RoRoRo and you're not actively tabbing through all of them, that timeout hits — and if a bunch of them launched together, they all reconnect at once and Roblox's trust gate throws a captcha at you. Ur AFK stops that before it starts: it watches how long each enabled account has been idle, and right as one's about to time out, it briefly jumps to that window and taps Space to keep it alive. Then it hands your focus straight back.

<!-- icon.png is a placeholder borrowed from rororo-ur-task. The real Ur AFK icon
     is a pre-ship gate through the 626labs-design skill — no programmatic
     placeholders ship to clan-facing surfaces. Swap before v0.1 goes out. -->

## What it actually does

One job, done quietly:

1. Every 60 seconds it asks RoRoRo how long each account has gone without activity.
2. Any enabled account that's crossed your threshold (15 minutes by default, plus a little random jitter per account so a batch of alts doesn't all get grabbed on the same beat) goes into the "due" list.
3. One account at a time: the pill counts down 3 seconds, then Ur AFK focuses that account's window, taps Space, and gives your focus right back.

That's it. No mouse input, no macros, no clicking around for you — that's [ur-task](https://github.com/estevanhernandez-stack-ed/rororo-ur-task)'s job if you want it. Ur AFK does exactly one thing: keeps your AFK accounts from logging themselves out.

## You'll always see it coming

The whole point of a tool that steals your focus for a second is that it should never feel sneaky. Ur AFK shows an **activity pill** — in the main window and as a small floating badge that stays on top even when you've minimized to tray — so you always know what it's about to do:

- **Cyan, "Active · watching N accounts"** — nothing due, just watching.
- **Magenta, counting down** — "Grabbing *AccountName* in 3… 2… 1" — this is your window to react.
- **Magenta, solid** — the jump is happening right now (about a second).

**Press F8 anytime during a countdown to skip that grab.** The account might idle out if you skip it — that's your call, not ours. (F8 is the skip key in v0.1; if it clashes with something else you run, you can change it in `settings.json` for now — a rebind screen is coming in a later version.)

## What it asks permission for

RoRoRo shows you a consent sheet the first time Ur AFK connects, listing exactly four things it wants to do. Nothing more, nothing hidden:

| It asks to… | Why |
|---|---|
| **Synthesize keyboard input** | The one key it ever sends is Space — that's the whole "keep alive" trick. |
| **See when accounts launch** | So it knows which windows exist and can build the account list. |
| **See when accounts exit** | So it stops tracking an account the moment you close it. |
| **Read account idle time** | This is the signal that drives everything — RoRoRo tells it *when* it last saw activity, never *what* you did. |

You grant all four once, and you can revoke them anytime from RoRoRo → Plugins. If you revoke mid-run, Ur AFK stops acting immediately and the footer tells you why.

Ur AFK never touches the mouse, never watches your keystrokes globally, and never does anything with a captcha — that's a permanent no, not a "not yet."

## Requirements

- **RoRoRo 1.8 or later.** Ur AFK needs the `GetAccountActivity` query that shipped in that release; older hosts will refuse the install with a clear "update RoRoRo" message.
- Windows 11.

## Install

Ur AFK hasn't shipped a signed release yet (v0.1 is still in development — see [`docs/DEV.md`](docs/DEV.md) for the release shape once it does). Until then, build it from source.

## Build from source

You'll need the [.NET 10 SDK](https://dotnet.microsoft.com/download) and Windows 11.

```powershell
& "$env:LOCALAPPDATA\Microsoft\dotnet\dotnet.exe" build rororo-ur-afk.csproj
```

Run the tests:

```powershell
& "$env:LOCALAPPDATA\Microsoft\dotnet\dotnet.exe" test tests/rororo-ur-afk.Tests/
```

For the local dev-install path (where RoRoRo actually loads plugins from) and the pre-ship checklist, see [`docs/DEV.md`](docs/DEV.md).

## License

MIT © 626 Labs LLC.

---

**A 626 Labs product · *Imagine Something Else*.**
