# YoWidgets verification — 19 October 2026

Actual evidence for version 0.1. Untested conditions are not passes.

## Build and optical material

- Local Microsoft SDK 10.0.402 downloaded and SHA512 verified. No global SDK/runtime installation. Self-contained Windows x64 publish succeeds; a shared .NET installation is not needed.
- The SDK unexpectedly created a first-run HTTPS development certificate. Its exact new thumbprint/timestamp were checked and that certificate alone was removed. Later commands disable certificate generation. No trust imported, Developer Mode enabled or Explorer restarted.
- Release build: 0 warnings, 0 errors. HLSL compiled by D3DCompile to PS 3.0, 2252 bytes. Runtime WPF hardware tier 2 on this laptop (RTX 3050 Laptop, Windows 11 build 26200).
- Native checkerboard/wallpaper inspection shows curved-edge refraction, RGB separation, lens displacement and highlights. Wallpaper source uses IDesktopWallpaper. This verifies optics, not pixel-perfect Apple equivalence or arbitrary live-window refraction.
- Premium project audit: strict, 0 errors/0 warnings. DESIGN lint: 0 errors/9 schema/token-reference warnings; native semantic mappings are documented in DESIGN.md. These audits do not establish runtime performance or visual equivalence.

## Behavior and regression checks

- Core behavioral tests: **20/20 pass**. Coverage includes negative-coordinate placement, oversized widgets, path containment, LRC timestamps, stale-work rejection, settings normalization/recovery, wallpaper Fit geometry, timer policy, duplicate IDs, conflict-aware rollback, integration opt-out, fallback weather and future city-local forecast hours. New regression cases were observed failing before fixes.
- Native UI inspected with screenshots/UI Automation. Named switches enable/persist widgets; nine catalog types are present. Decorative previews expose one image each instead of duplicate calendar/player controls. Clock/calendar use actual OS date/time. Music metadata was read from a real Chrome media session; unsupported commands are disabled.
- Open-Meteo geocoding worked, but its forecast endpoint returned HTTP 503. Bounded wttr.in fallback returned actual Kyiv weather and created a local cache. No demonstration numbers substituted. Provider/updated time are exposed by the weather tooltip.
- Snooze/resume exercised through the UI: surfaces disappear and return. Later optimization removes optical catalog previews while paused. Disable-all clears private integration opt-ins in regression tests.
- Installed desktop clock reports successful parent attachment (desktop: На рабочем столе). This is attachment evidence, not a completed Win+D/virtual-desktop visual test.

## Installation and removal

- Lifecycle tests: **8/8 pass**: preserve foreign install file/report partial removal; retry successfully; repeat safely; reject descendant junctions; preserve unknown data and retry files; reject junction ancestors before writing; recover after locks on installation.json and uninstall.ps1. Test installations were removed through the ownership-checking cleaner.
- Real default per-user install → run → uninstall completed. Install root, data root and HKCU uninstall registration were checked absent; source/examples remained present. A fresh installation is then left for use with widgets initially off.
- Fresh whole-project review found recovery/async/device issues; these were fixed. A bounded follow-up found no remaining blockers in reviewed terminal-uninstall and journal-baseline fixes. Review was code inspection, not device testing.
- No notification identity, autostart, drivers, hooks, certificate trust or system DND modifications enabled. Windhawk unchanged. Hardware volume/brightness writes were not exercised; rollback policy has behavioral tests.

## Resource measurements

Process CPU divided by 8 logical processors. Working set is resident process memory; GPU/DWM consumption was not measured. Short samples do not prove long-run stability.

| Scenario | Duration | Average CPU | Working set | Evidence |
|---|---:|---:|---:|---|
| Five base widgets, management not foreground, seconds off | 60.1 s | 0.029% | 219.5 MiB | artifacts/idle-clean-metrics.json |
| Original visible UI under native automation | 300.5 s | 8.938% | 223.9 MiB | artifacts/idle-metrics.json |
| Final catalog under native automation, includes clock toggle | 30.1 s | 4.305% | 205.1 MiB | artifacts/catalog-accessibility-metrics.json |
| Final Snooze, management open under native automation | 30.1 s | 4.698% | 199.2 MiB | artifacts/snooze-open-final-metrics.json |
| Final Snooze, management closed to tray | 30.1 s | 0.000% | 202.4 MiB | artifacts/snooze-tray-final-metrics.json |

Visible-window measurements include frequent UI Automation provider calls observed in a dotnet-trace profile (ElementUtil/ElementProxy/Dispatcher). Removing decorative preview children reduces the accessible tree; Snooze removes shaders entirely. Visible UI under this automation environment still has measurable overhead. Do not claim the active gaming/GPU target has passed. Tray result means no CPU increment detectable in this short measurement, not zero process/DWM cost.

## Not yet verified

Notification package deployment and real consent/feed; external AI provider (requires a key and may incur charges); audio/brightness writes and physical rollback; player command compatibility and 100 expansion cycles; mixed-DPI screens; Win+D visual persistence, virtual desktop switches, Explorer restart, wallpaper changes across virtual desktops, sleep/resume, full-screen games, sustained memory/CPU/GPU targets, RDP/software rendering and High Contrast.

Source implementation does not prove these scenarios. Official IVirtualDesktopManager has no PinWindow API; no private pinning COM used. Wi-Fi/DND open supported Windows settings. Notification listener does not suppress system toast delivery. AI performs explicit configured-provider requests only.
