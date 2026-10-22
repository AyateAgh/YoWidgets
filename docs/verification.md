# YoWidgets 0.6.7 verification


## Transparent widget instruments, 2026-10-22

Focus and Controls now inherit pack glass and adaptive ink instead of a second dark panel. Photo content has zero inset and no image shadow; its action fades on hover or keyboard focus and remains visible for empty/unavailable images. Corner resizing persists per-instance DIP dimensions and keeps the opposite edge anchored. Shared gesture/persistence bounds prevent accepted sizes changing on restart. S/M/L selection clears custom dimensions and updates the live window.

The new opaque-surface regression failed before the fix. Final native platform checks passed 30/30, including thumb drag, transparent/adaptive surfaces, anchored photo resize, displayed-dimension persistence and reopen. Frontend checks passed 36/36; catalog 23/23 and the existing core/assistant/voice/desktop visual/composer/media/control/document/compositor suites also passed during this revision. After final photo action refresh and brightness-write reconciliation adjustments, native 30/30 was rerun and the final single-file publish succeeded. Owned install/removal passed 15/15 on the final executable.

Actual Windows pointer input on isolated fixtures verified continuous rail drag (40 to 75.84 without changing system audio) and photo corner resize (398.4 by 208 to 318.4 by 173.6 DIP, with the same saved dimensions). Tab focused and revealed Choose photo; screenshots verified edge-to-edge images and the transparent Focus surface. Controls volume writes coalesce at 40 ms and brightness at 150 ms; UI remains local and responsive while the latest value is applied. Broad hardware/performance claims, including all-device frame-rate guarantees, remain unmeasured.

## Native widgets, Focus and Library, 2026-10-22

The WPF parent-ownership error was reproduced in both action-pill construction and native widget creation tests, then fixed by assigning each label to only its final parent. Failed window creation now rolls back Add/Enable and cannot create an enabled/countable ghost. The actual packaged host created Photos, Focus, Controls and Notifications without that error; its desktop count changed 4 → 3 → 4 when Controls was hidden/restored.

Final frontend checks passed 36/36 and native platform checks 27/27. They cover actual visible-window filtering, Hidden separation, pause/resume, failed construction rollback, Focus expiry/stop/restart/failed-start rollback, partial recovery, late sessions, ambiguous multi-instance recovery and Disable all. Owned install/removal checks passed 15/15. A full default local build passed the existing visual/voice/composer/media/control/document/compositor suites before the audio scope was refined; the final per-app audio integration was checked separately afterward.

Native explicit checks set/read/restored Windows DND on the current Windows build. Owned silent PCM child processes verified per-app mute leaves a separate music session unchanged, new-session callbacks arrive, and original mute survives chat-app exit, restoration and relaunch. No user's saved API keys or communications accounts were used by these fixtures. Focus uses the Windows shell QuietHours compatibility contract; future OS compatibility is not guaranteed. Music players and browsers are excluded from call-app quieting, and shared browser/app audio cannot be separated into music versus calls.

Actual widget screenshots verified the Focus duration popover, one Start/End action, Controls contrast/sliders, and the Notifications capability state. The current portable build has no Windows package identity; its Notifications widget appears and exposes Windows settings, but cannot read notification content until a packaged identity and permission are supplied. A signed packaged distribution is not delivered by this update. Hardware-device disconnect/crash variations beyond the included fixtures remain unmeasured.

## Native caption dragging, 2026-10-22

The packaged 0.6.4 caption failed an actual pointer drag. Its asynchronous RPC called WPF DragMove although mouse input belongs to the browser HWND. Version 0.6.5 enables WebView2 non-client regions before navigation and marks the caption as draggable, explicitly excluding controls. The old RPC mouse path is removed; native caption double-click retains the existing zoom controller.

Frontend 33/33 and native platform 18/18 passed; the packaged build succeeded. Actual packaged WebView checks at 125% scale confirmed horizontal movement from both blank caption and application-name text, drag-to-restore from maximized, animated caption double-click, and minimize/reopen. The source audit found no secrets. These checks used an isolated profile; no user credentials were exercised.

## Compositor window transition, 2026-10-22

The old live-resize regression reproduced 63 Window.SizeChanged events in one maximize. The new controller commits live bounds once and animates a fixed-HWND cached image instead. Native tests cover passive/no-activate overlay styles, fixed overlay bounds during WPF transform/fade, disposal after normal completion and cancellation, fallback after a simulated render failure, and maximized/restored/minimized/rapid-toggle geometry. Frontend checks cover hidden-to-visible deferred presentation, stable ownership across requested-page updates, reduced motion, native-resize suppression, initial observer callbacks and browser recovery.

Frontend 33/33 and native platform 18/18 passed on the final source. The default full build also passed content platform 23/23, core 87/87, assistant transport 75/75 and the native visual/voice/composer/media/control/document/compositor suites. The final focused checks were rerun after compositor handoff and cancellation changes.

The actual packaged 0.6.4 WebView host was checked in both directions at 125% desktop scale, including transparent-center PNG capture, close-to-tray/reopen and minimize/reopen. Completed transitions left no overlay window. Observed capture-to-handoff totals were 707–853 ms, including capture/decoding, initial overlay presentation, 250 ms motion, readiness and 100 ms fade; these timings are not frame-rate measurements. Opaque captures took 117–131 ms and a transparent capture took 266 ms. Native edge dragging was also checked at 852 × 480 logical pixels, with Settings reachable. A read-only review identified the cancelled live-frame task race; the completion source is now local and its task is observed on every exit.

Physical mixed-DPI monitor transitions and sustained frame-rate acceptance across GPU classes remain unmeasured; no universal frame-rate guarantee is made.


## Window bounds and responsive motion, 2026-10-22

Frontend 27/27, content platform 23/23 and native platform 15/15 passed. The full default local Windows build also passed core 87/87, assistant transport 75/75 and all native visual/voice/composer/media/control/document/compositor suites. The additional frontend startup recovery test and expanded native minimize/OS-restore assertions passed after the full run. Owned install/removal checks passed 15/15.

Native geometry tests exercise the real WindowChrome/layered window: work-area maximize, normal restore, Windows restore, rapid interrupted toggles, and maximized-state preservation through minimize. Pure geometry cases cover 100/125/150/200% scales, small work areas and negative monitor coordinates. Motion tests cover repeated reveal cancellation, suspend/disposal cleanup, reduced motion and resize interruption. The initial native snapshot failure test verifies ui.present exposes the retry screen.

An isolated actual 0.6.3 host rendered Settings with its lower rail button visible at 640 × 420 logical pixels. Maximize produced a 1536 × 806 logical window above the taskbar on the current 125% desktop, then restored the compact bounds. Minimize/reopen retained maximization, and process restart retained normal size/position/navigation. These are point-in-time UI checks; sustained frame rate, manual-resize motion quality across GPUs, and physical mixed-DPI monitor transitions remain unmeasured. A read-only review identified initial-error presentation and minimize-state loss; both were fixed and covered by focused regressions.
## Appearance, 2026-10-22

Frontend 23/23, content platform 22/22, native platform 14/14, core 87/87 and assistant 75/75 passed in the full default Windows build, including native visual suites. Appearance tests cover atomic native validation/persistence, one/two-color selection, transparency preserving the theme, color conversion, readable gradient endpoint text, object preference acknowledgements and preserving circular transitions across native acknowledgements.

Actual isolated host checks verified Light rendering and circular reveal, local custom color controls, adding/applying a second color, persistent custom mode/colors/transparency, and actual content underneath the transparent center with functioning controls. The theme audio command was exercised through the existing native sound policy. A one-pass independent review found no Critical/P1 issue and two P2 issues: recovery losing its opaque backdrop and weak Light keyboard focus. Both are corrected. Native bootstrap/recovery remains opaque and focus uses the semantic text color. GPU diversity and long-run measurements remain unmeasured.

## Catalog polish, 2026-10-22

Frontend 16/16, content platform 22/22 and native platform 12/12 passed. Core 87/87 and assistant transport 75/75 remain green. The default Windows build passed all native voice/composer/media/control/document suites, real browser bridges and the compositor probe.

New checks cover numeric update eligibility, existing legacy metadata repair, exact Library instance restoration, optimistic toggle rollback and saving across Settings remounts, city draft versus explicit save, terminal download navigation, local job retry, secret-free import snapshots, valid offline ZIP resources and rejected executable/path/checksum/foreign-style payloads. Reproductions for the foreign dependency override, stale settings queue and modal-picker timeout failed before their fixes. An independent review reported no Critical and three Important issues plus a local-import retry defect; all four are addressed. Writes remain owned by the application while navigating; a hidden host waits for in-flight preferences before suspension.

The actual 0.6.1 host was checked with an isolated profile: a native ZIP picker remained usable beyond the regular RPC deadline, showed metadata, installed an Aurora clock, navigated from Downloads to Library and enabled it with the card switch. General switch screenshots retained identical card geometry. Appearance rendered all eight accents and applied Violet to the active rail. IP lookup parsing and editable-draft behavior use fixtures; no user's current IP was queried during tests. Hardware/performance limitations below still apply.

The normal portable and owned installed copies have matching 0.6.1.0 binaries. Reopening the existing profile preserved encrypted connection fingerprints and widget IDs, positions, sizes and visibility. Home showed current migrated content as Installed/Add instead of Update. Owned install/removal checks passed 15/15.

## Automated checks

- Core regression suite: 87/87. Assistant transport suite: 75/75.
- Content platform: 16/16, including actual ZIP resources, SHA-256, traversal, cancellation, dependencies, offline removal, custom collections, migration, malformed registry backup recovery and automatic eviction policy.
- Native platform: 8/8, including secret-free snapshots, provider key separation, async pipe events/reconnect, invalid settings rollback, actual installed widget instances, failed removal preserving the desktop and null catalog fallback.
- Frontend: 8/8, including combined filters/search, partial collections, dependency sizes and malformed saved-view recovery.
- Full build also runs browser bridges, native voice, composer, generated media, browser/control scope and DOCX/PDF/XLSX/PPTX checks. These use isolated fixtures and do not consume a user's saved API keys.
- Native compositor probe: 100 bind/property/animation/disposal cycles. These are compositor cycles, not WebView suspend cycles.
- Owned installation/removal: 15/15, including new WebView cache, catalog/selection state, downloaded resources, unknown-file preservation and an active portable engine sharing the same data root. Platform cleanup and active-root protection were reproduced as failing cases before the script fixes.

## Real host observations

Home and Library rendered in the actual WebView2 host using an isolated profile. Installing Glass from a development HTTP feed produced nine installed widgets, and Add to desktop created a live native clock from the downloaded descriptor. First new-profile cold-ready observations were 2989.7 ms and 1201.7 ms in separate sessions. Timing varies with runtime/profile/system state; no fixed latency guarantee is made.

## Independent review

The final read-only review reported no Critical and three Important findings. Each was reproduced before fixing: malformed persisted JSON, failed uninstall changing active instances, and automatic UI eviction dropping a draft. The new core/native tests then passed. No minor findings were deferred by that review.

Hosted CI runs deterministic frontend/core/transport/platform checks with `-SkipVisualTests`. The default local build additionally runs the desktop visual suites. Hosted Windows animation/accessibility settings vary; a lack of animation under Windows reduced-motion preferences is valid behavior, not a failed visual effect.

## Manual acceptance remaining

Hardware mixed-DPI transitions at 100/150/200%, monitor removal, missing-runtime installation under restrictive policy, hour-long process/GPU/DWM measurements and 100-cycle WebView suspend/resume remain unmeasured. Draft eviction policy is covered by code tests; live suspension-failure injection has not been performed. Native Windows notification identity and external services depend on the user's permissions and configuration.

The executable is self-contained for .NET, but management needs WebView2 Evergreen. It is not publisher-code-signed. SHA-256 package validation is not a publisher signature. No enterprise certification, universal application/game compatibility, fixed RAM usage or zero-latency claim is made.

A new empty modern content registry is persisted at first startup, so saving preferences cannot make a later launch look like a legacy profile. The restart/migration regression failed before this fix and then passed.

Final executable acceptance also passed an actual two-process fresh-profile restart: no auto-installed packs, custom colors and transparency preserved. The layered host suspended successfully and restored its Light Home view from the tray. Owned installation/removal remained 15/15.
