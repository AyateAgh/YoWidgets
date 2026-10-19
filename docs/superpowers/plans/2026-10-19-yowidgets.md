# YoWidgets Implementation Plan

> **For agentic workers:** Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Deliver a runnable Windows 11 widget utility with measured shader glass, real data/actions, reversible per-user installation and honest unsupported states.
**Architecture:** WPF management UI and independent glass widget windows share state/data services. Wallpaper sampling and HLSL refraction are isolated behind GlassSurface; DesktopHost never uses undocumented virtual desktop pinning. Native Direct3D is the fallback only if the shader prototype fails visual/performance checks.
**Tech Stack:** Local .NET 10 SDK, WPF, Win32/COM/WinRT, HLSL ps_3_0, dependency-light C# tests.
**Spec:** ../../design-proposal.md; ../../cleanup-contract.md; ../../technical-review.md.

## Global Constraints

- Windows 11; PerMonitorV2; Russian UI; no drivers, injection, shell replacement, undocumented PinWindow or hidden system configuration.
- Real refraction is required. Acrylic alone does not satisfy glass.
- Local SDK, per-user app data, explicit optional notification/AI integration. No global installation or certificate trust changes during development.
- User explicitly authorized implementation on 19 October. Execute inline in the requested workspace.

## Review Focus

- Desktop/monitor changes: never strand windows or sample the wrong coordinates.
- Cleanup: never follow junctions or delete outside app-owned roots.
- Async/Snooze: cancelled responses never restart services or overwrite a new request.
- Media capability differences: unsupported operations remain visibly unavailable.
- Empty/denied notification and AI states: no fabricated content or permission bypass.

## Task 1: Foundation and optical prototype

Files: src/YoWidgets.Core/{Models,Placement,OwnedPaths}.cs, src/YoWidgets/{App,MainWindow,GlassSurface,WidgetWindow,WallpaperProvider,Native}.cs, shaders/Glass.hlsl; tests/YoWidgets.Tests.
Interfaces: AppSettings; WidgetState; Placement.Clamp; OwnedPaths.Validate; GlassSurface.Configure(background,rect,parameters).
- [ ] Test placement on negative-coordinate/mixed-size screens, setting validation, cleanup path rejection; observe failures, implement and rerun.
- [ ] Install SDK locally, compile shader with D3DCompile, build native app; inspect refracted checkerboard and wallpaper, record hardware rendering tier.

## Task 2: Widget data and interactions

Files: src/YoWidgets/Services/{MediaService,WeatherService,LyricsService,FocusService,NotificationService,AiService,SystemControls}.cs; Widgets/{Clock,Calendar,Weather,Music,Photo,Focus,QuickControls,Notifications,Assistant}.cs.
Interfaces: services expose immutable snapshots, change events, cancellation and Dispose/Suspend; WidgetContentFactory.Create(kind).
- [ ] Tests for lyrics timestamps, stale async generation, weather mapping and Snooze cancellation; observe red then green.
- [ ] Connect real WinRT media, Open-Meteo, local files and Core Audio; disable unavailable actions. Add opt-in notification listener and AI setup/cancellation without identity or credentials fabrication.
- [ ] Verify compact/expanded player, keyboard actions, loading/empty/error/offline states.

## Task 3: Management, persistence and desktop host

Files: MainWindow.cs, Theme.xaml, SettingsStore.cs, WidgetManager.cs, DesktopHost.cs; DESIGN.md; UX-CONTRACT.md.
Interfaces: SettingsStore.SaveAtomic/Load; WidgetManager.Enable/DisableAll/Snooze/Restore/Dispose; DesktopHost.TryAttach/Detach.
- [ ] Tests for corrupt/invalid settings and recovery, enable/disable persistence and display clamping.
- [ ] Build catalog, appearance/settings, instance controls and tray. Support per-monitor wallpaper COM and explicit failed desktop attachment.
- [ ] Verify restart, Win+D, user input, reduced motion and DPI. Report virtual desktops unverified if not exercised.

## Task 4: Reversible delivery

Files: scripts/{build,install,uninstall,register-dev-identity,unregister-dev-identity}.ps1; src/YoWidgets/Cleanup.cs; packaging/AppxManifest.xml.
Interfaces: lifecycle journal records exact ownership; cleanup returns checked residuals; per-user installer defaults to LocalAppData Programs/YoWidgets.
- [ ] Tests for foreign roots, reparse paths, repeat removal, corrupt journal and previous ownership.
- [ ] Deliver local portable build and explicit per-user install/uninstall commands. Optional sparse identity has exact reciprocal unregister; never silently enable developer mode/import trust.
- [ ] Verify installation/removal in a dedicated owned test directory without deleting source/examples.

## Task 5: Verification and review

Files: docs/verification.md; README.md; artifacts/screenshots; docs/progress.md.
- [ ] Build Release, run full test suite, inspect UI and glass screenshots; measure idle/Snooze CPU/memory and runtime errors.
- [ ] Run applicable Premium design audit; native equivalents for web-only checks are documented.
- [ ] Fresh whole-project review under executing-plans; fix important findings and rerun affected verification.
- [ ] Publish local runnable build, source and known limitations. Never label untested notification consent/virtual desktop behavior as passed.
