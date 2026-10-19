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

## Исправления 0.2 — фактическая проверка видимости

Предыдущая проверка успешного SetParent не доказывала видимость и оказалась недостаточной. Этот путь удалён: в рабочем коде больше нет SetParent/WorkerW-host. Старые настройки DesktopMode мигрируют в false.

- Новый видимый clock HWND: parent=0; X=711, Y=373, Width=498, Height=260 физических пикселей; Topmost=False; AllowsTransparency=True. Проверен реальный кадр всего экрана, а не только статус HWND: artifacts/screenshots/visible-widget-desktop.png. Текст окна под часами виден сквозь материал, скруглённые углы прозрачны. Снимок содержит рабочий стол с открытым Codex.
- HLSL теперь возвращает premultiplied alpha 0 за пределами SDF, около 0,30–0,55 внутри. Яркая белая кромка ослаблена. Это реальная альфа-прозрачность плюс преломление текстуры обоев; живые чужие окна не преломляются и не захватываются для материала.
- Принудительный fallback проверен запуском с YOWIDGETS_GLASS_FALLBACK=1: независимые часы видимы на X=100,Y=100; режим transparent-gradient-fallback. Обработчик InvalidPixelShaderEncountered переводит живые поверхности в тот же полупрозрачный градиент при отказе render-thread. Аппаратный отказ GPU не симулировался.
- Управление проверено на экране: 5px скруглённый thumb, без стрелок и белого трека; собственный тёмный заголовок, кнопки свернуть/закрыть; полупрозрачная граница карточек. Кнопка сброса есть в трее и оформлении; несколько виджетов располагаются центральной сеткой. Плавающие окна подчиняются обычному Z-order Windows.
- Core: 25/25; lifecycle: 10/10. Новые проверки: миграция режима окон, центрирование на отрицательном мониторе, отсутствие наложения при сбросе двух окон, корректный crop 4K и Fit letterbox, точный runtime-cache cleanup и отказ на чужую cache-папку.
- artifacts/single-exe содержит ровно один YoWidgets.exe, 82020787 байт; SHA256 фиксируется в artifacts/fix-visibility/result.json. Все три требуемых publish-флага включены. Дополнительно IncludeAllContentForSelfExtract=true: runtime распаковывается в temp/.net/YoWidgets/<bundle-id> для уменьшения RAM. Это режим совместимости зафиксированного .NET 10, требующий повторной проверки при смене runtime; см. документацию Microsoft о single-file deployment.
- A/B на одном компьютере с одними видимыми часами: обычный compressed bundle 297,6 MiB working set / 145,6 MiB private; extract-all вариант примерно 154 / 78 MiB. Управляемая куча после GC около 1,6–1,7 MB, временный crop cache=0. Полные кадры обоев не удерживаются; сохраняются независимые вырезки максимум 900×650, до 8 записей, сбрасываемые после инициализации/закрытия управления. Принудительная GC однократная, не в таймере и не во время перетаскивания. Working set искусственно не урезается.
- Обновление существующей per-user установки проверено scripts/update.ps1: пользовательские настройки и две ранее включённые музыкальные карточки сохранены, старые принадлежащие программе DLL удалены, установлено единое EXE. Режим Topmost проверен переключением в интерфейсе.

Это проверка пяти исправлений на данном компьютере; смешанные физические мониторы, игровые/GPU и долгие стресс-сценарии по-прежнему не выданы за пройденные.

Финальный замер готового EXE с одними часами, без окна управления: 30,1 с; CPU 0,338% от 8 логических ядер; working set 139,7 MiB; private 62,3 MiB. Сырой результат — artifacts/fix-visibility/final-memory.json. Реальный запуск тестовой установленной копии и удаление проверены: InstallGone=True, RuntimeCacheGone=True. Это короткий замер конкретного сценария, а не гарантия для всех виджетов и систем.

Финальная проверка установленной 0.2 через интерфейс: Snooze скрывает поверхности; включение часов переключателем создаёт видимый независимый clock; wheel и drag тонкого scrollbar работают; сброс позиций разместил три окна без наложения (music X=188/Y=418 и X=710/Y=418, clock X=1233/Y=373). Окно управления закрыто в трей, виджеты оставлены работающими. SHA256 установленного EXE совпадает с artifacts/single-exe/YoWidgets.exe; в single-exe ровно один файл; временных lifecycle-установок осталось 0. Материал не захватывает экран в фоне: диагностический кадр снят отдельно только по запросу пользователя.

## 0.3 — motion, material and player revision (19 October 2026)

- Reviewed both local videos using extracted timestamped frames: LiquidGlassAppleWidgets.mp4 (38.47 s) and LiquidMusicPlayer.mp4 (58.77 s). Material/interaction guidance checked against Apple's WWDC25 “Meet Liquid Glass”: https://developer.apple.com/videos/play/wwdc2025/219/.
- Native DragMove replaces the per-MouseMove reposition/refresh path. A shared monitor atlas (aspect-preserving, <=1600×1000, <=2 cached monitors) is prepared at idle. During dragging only the image Viewbox changes; no image decode, settings save, content reconstruction or forced collection runs per movement. Position persists on release. Expansion reuses the atlas during its 220 ms transition. Rendering callback is detached afterwards.
- Normal settled snapshots remain bounded to 900×650 / 8 entries. Snooze/closing releases motion references and caches. Cross-monitor motion retains the starting atlas until settling; mixed-DPI physical testing remains outstanding.
- Updated material has a broader lens edge, quieter center, restrained chromatic separation and directional highlight. Alpha remains zero outside SDF. This is a wallpaper-based material, not pixel-identical iOS 26 or live refraction of arbitrary application windows. No continuous screen capture was added.
- Shared vector controls and widget typography replace uneven text glyph buttons. Player has 54px rounded artwork, five aligned transport controls, separate timing columns and compact/expanded 184/410 DIP surfaces. Logo expansion is limited to 88px; actual track covers can use 190px. Focus fits S; notifications/assistant text scrolls inside constrained grids.
- Artwork resolution is asynchronous: valid system thumbnail, then a known website favicon (or native application identity logo), then a fully collapsed artwork column. Public origin is chosen from media metadata, without inspecting browser history/tabs. Transfers, decode size and timeouts are bounded; old track work is cancelled. Real Chrome/TikTok session loaded its website logo. Track-first, broken-cover fallback, no-image, cancellation and negative-coordinate motion tests pass; live cover handling across all music services is not claimed.
- Core tests **31/31 pass**. Lifecycle checks **10/10 pass**, including foreign-file protection, retry and runtime bundle-cache cleanup. PS 3.0 compilation succeeds at 2356 bytes; Release publish succeeds without warnings. Single-file packaging and owned cleanup contract are retained.

Final native UI check: clicking Expand changed the real player's outer height from 224 to 450 DIP (compact/expanded content 184/410). Its real TikTok website logo was visible; no placeholder artwork was substituted. Management closed to tray through its named accessible control. One-off full-desktop screenshot (1920×1080) is artifacts/visual-v3/desktop-all-widgets.png; it shows the diagnostic nine-widget profile, including honest unconfigured photo/notification/assistant states. No notification identity or AI provider was enabled for this check.

Native automated drag moved clock from physical (310,60) to (454,120): prepareMs=5, loopMs=19, frames=2, decodesDuringLoop=0. The initial uncached implementation took 187 ms including snapshot preparation; measurements are not directly comparable end-to-end frame benchmarks. These short two-point synthetic drags demonstrate the absence of decoder work during movement, not sustained 60/120 FPS. Raw trace and result: artifacts/visual-v3/app.log and result.json.

The installed 0.3 EXE SHA256 matches the published single EXE; settings file hash was unchanged by update. Exact single EXE size/hash are recorded in artifacts/visual-v3/final-memory.json. Installation/uninstallation scope is unchanged; the design update adds no disk artwork cache or system hooks. Runtime host/driver/device and long gaming stress tests remain unverified.

Final installed 0.3 idle sample: 30.01 s, two paused music cards and clock, management hidden; process CPU rounded to 0.000% across 8 logical processors, working set 160.6 MiB, private 88.2 MiB. This is a short idle sample; GPU/DWM and active playback were not benchmarked. Single EXE 82,034,757 bytes, SHA256 B061A791526888AF91ED27A8D209CEA67DFE6E4B587293329335A9CC0F1EC4A8. Raw result: artifacts/visual-v3/final-memory.json. Final lifecycle rerun: 10/10. Installed 0.3 is left running with the user's existing three widgets and saved options.
