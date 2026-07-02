# Developing RoRoRo Ur AFK

> Audience: whoever's hacking on Ur AFK itself, not people writing a competing plugin. For the plugin contract, capability vocabulary, and general "how do I write a RoRoRo plugin" reference, see RoRoRo's own [`docs/plugins/AUTHOR_GUIDE.md`](../../ROROROblox/docs/plugins/AUTHOR_GUIDE.md) (sibling repo) — this doc only covers what's specific to Ur AFK.

## Build & test

Always use the pinned SDK via the explicit host — bare `dotnet` on `PATH` may resolve to a different SDK than `global.json` pins.

```powershell
& "$env:LOCALAPPDATA\Microsoft\dotnet\dotnet.exe" build rororo-ur-afk.csproj
& "$env:LOCALAPPDATA\Microsoft\dotnet\dotnet.exe" test tests/rororo-ur-afk.Tests/
```

## Local dev-install path

RoRoRo loads a plugin from `%LOCALAPPDATA%\ROROROblox\plugins\<plugin-id>\` — for Ur AFK that's:

```
%LOCALAPPDATA%\ROROROblox\plugins\626labs.ur-afk\
```

To run a dev build against a real RoRoRo instance:

1. Build the project (`dotnet build` above, or `dotnet publish` for a self-contained drop).
2. Copy the build output — `626labs.ur-afk.exe` and its dependencies — plus `manifest.json` and `icon.png` from the repo root into that folder.
3. Launch RoRoRo. It picks up plugins from that directory on startup (or via RoRoRo → Plugins → refresh, depending on the host version).
4. Walk the consent sheet for the four capabilities on first connect.

There's no hot-reload — stop Ur AFK, recopy the build output, relaunch it.

## Release shape (for when v0.1 ships)

Per the AUTHOR_GUIDE's distribution contract, a shipped release is three fixed artifacts sitting together at a GitHub Releases download URL:

```
manifest.json          — this repo's manifest.json, exactly as committed
manifest.sha256         — single-line lowercase SHA-256 of plugin.zip
plugin.zip              — build output (626labs.ur-afk.exe + manifest.json + icon.png + deps)
```

RoRoRo's installer fetches `manifest.json`, then `manifest.sha256`, then `plugin.zip`, and refuses to extract if the SHA doesn't match — so the hash has to be computed against the exact `plugin.zip` you're publishing, not a stale one.

Users install by pasting the **directory URL** — the parent path containing those three files — into RoRoRo → Plugins → Install. For example, if the release lives at `https://github.com/estevanhernandez-stack-ed/rororo-ur-afk/releases/download/v0.1.0/`, that's the URL they paste; RoRoRo appends the three filenames itself.

`plugin.zip`, `manifest.sha256`, and `artifacts/`/`publish/` build output are gitignored — never commit them. Build and hash them fresh for each release.

## The ProjectReference → PackageReference(0.3.0) swap

`rororo-ur-afk.csproj` currently references the plugin contract as a **ProjectReference** against the sibling checkout, because `ROROROblox.PluginContract` 0.3.0 (the version carrying `GetAccountActivity`) isn't published to nuget.org yet — only 0.1.0 is:

```xml
<ProjectReference Include="..\ROROROblox\src\ROROROblox.PluginContract\ROROROblox.PluginContract.csproj" />
```

**Before v0.1 ships**, this has to be swapped for the real package reference. Checklist:

- [ ] Confirm `ROROROblox.PluginContract` 0.3.0 is published to nuget.org (or wherever ur-AFK's build will pull packages from).
- [ ] In `rororo-ur-afk.csproj`, remove the `ProjectReference` block (and its dev-time comment) and add:
  ```xml
  <PackageReference Include="ROROROblox.PluginContract" Version="0.3.0" />
  ```
- [ ] Rebuild clean (`dotnet build` after `dotnet nuget locals all --clear` if you want to be paranoid about a stale cache) and confirm `GetAccountActivityAsync` still resolves.
- [ ] Re-run the full test suite — the contract types flow through `PluginClient`, `HostActivityQuery`, and the contract sanity test.
- [ ] Remove the sibling-checkout dependency from any CI/build docs that assumed `..\ROROROblox\` was present on the build machine.

## `minHostVersion` reminder

`manifest.json` currently pins `minHostVersion: "1.8.0.0"` — a placeholder for the RoRoRo release that ships Part A (`GetAccountActivity`, the activity-awareness work from ROROROblox PR #31). **Before v0.1 ships**, confirm the actual version tag of that RoRoRo release and update `minHostVersion` to match exactly. If it drifts, RoRoRo's handshake gate will either block installs on a host that actually has the query, or (worse) let installs through on a host that doesn't — both are user-facing bugs, not cosmetic ones.

## Repo layout

See the root [`README.md`](../README.md) for the pitch, and `docs/superpowers/specs/` and `docs/superpowers/plans/` for the design spec and the task-by-task implementation plan this build followed.
