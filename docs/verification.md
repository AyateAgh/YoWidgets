# YoWidgets verification — 19 October 2026

This file records actual evidence; untested conditions are not passes.

- Local SDK 10.0.402 downloaded from Microsoft and SHA512 verified. No global runtime/SDK installation.
- SDK's first-run HTTPS dev certificate was unexpectedly created, identified by exact new thumbprint and timestamp, then removed. No trust imported; later SDK commands disable certificate generation.
- Core tests observed failing before implementation; current 12/12 pass: offscreen/oversized placement, path bounds, lyrics parsing, cancellation generation, numeric normalization, JSON recovery, wallpaper fit, activity interval and duplicate widget IDs.
- PS 3.0 shader compiled with D3DCompile: 2252 bytes. WPF runtime reports hardware tier 2. Native window inspection shows checkerboard refraction at curved edges; this is evidence of the optical prototype, not pixel-perfect Apple equivalence.
- Native WPF UI built Release with 0 warnings/0 errors and inspected via UI Automation and window screenshots. Toggle switches expose accessible names and changed clock enabled count from 0 to 1; settings JSON persists it. All nine types are implemented; external data and command paths require additional scenario checks below.
- Self-contained win-x64 publish succeeded; installed shared .NET is not required by that folder build.
- PowerShell lifecycle tests: 4/4 passed. Foreign file survives a reported partial removal, cleanup resumes to completion, repeated cleanup is safe, junction is refused without traversing its target. All dedicated test installations removed. Source/examples preserved.
- DESIGN.md lint: 0 errors; semantic token reference warnings are informational for the native runtime mapping. Premium project audit is being reconciled with the native capability map.

## Not yet verified

Actual notification consent/identity deployment; external AI provider (requires the user's key and may incur provider charges); changing audio/brightness; mixed-DPI physical screens, virtual desktop switches, Explorer restart, sleep/resume, full screen games, long-run memory/CPU/GPU targets and 100 player expansion cycles. Runtime tests will be appended as performed.

Source-level implementation does not prove these scenarios. Unsupported media commands are disabled. There is no official IVirtualDesktopManager.PinWindow API; no private pinning COM is used.
