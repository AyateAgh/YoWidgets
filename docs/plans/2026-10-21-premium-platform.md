# YoWidgets Premium — accepted implementation plan

## Goal and constraints

Deliver a real React catalog, local library, downloads and structured settings over the current native Windows widget engine. Preserve the existing working tree's assistant, voice, documents, media, compositor and saved data. Use an original empty-ring mark and `Yo` wordmark without a period. Publish only reviewed source and official content to the user-authorized public AyateAgh/YoWidgets repository; never publish local credentials, profiles, build tools or personal files.

The approved conversation is the product specification. This file consolidates its decisions and the integration review. No 10 MB RAM, 0 ms launch, fixed WebView memory or unmeasured enterprise-quality claims.

## Architecture

- One executable, two process roles: native engine/tray and independently launched React/WebView2 management host. No widget engine in the management process. Local compiled React, no address bar, external web navigation blocked.
- Hidden management suspends with TrySuspendAsync. Stop web media, requests and event subscriptions before sleep. One retry after five seconds; release the host if it still fails. Exit after 15 minutes hidden or on low-memory/deep-Snooze. Reopening invalidates pending sleep/exit callbacks.
- No EmptyWorkingSet, working-set tricks or forced GC as performance claims. Cold launch paints a native black bootstrap before WebView's first ready frame. Profile is owned Cache/UI, separate from assistant browser data. Soft 100 MiB cache-maintenance threshold, no deletion of a live profile.
- Current-user asynchronous management pipe. Requests have IDs, cancellation/deadlines and errors; native operations never depend on JavaScript acknowledgements. Bounded/coalesced event notifications. Reconnect reads a fresh snapshot. Existing WidgetBus remains compatible.
- Current and new host use PerMonitorV2. WPF DIP/CSS pixels and physical desktop pixels are converted once. Add targets the monitor containing the largest area of the management window, not the pointer; place inside its work area.
- Runtime check is lazy on opening management. Handle environment creation failures with native recovery UI; official Microsoft Evergreen installation is explicit. Widgets/tray survive unavailable UI runtime.

## Design and behavior

- 72 DIP black rail continuous with the window frame. Home, Library, Downloads; Snooze and Settings below. Original liquid active pill; icon labels/tooltips and keyboard focus.
- Shared near-black tokens: frame #080808, background #101010, surface #181818, hover #242424, text #F5F5F5, muted #A3A3A3. Segoe UI Variable; 14–15 DIP body, 24–28 headings; 12 DIP controls, 20 DIP cards.
- 1180x800 default, 860x600 minimum; responsive grid, fixed media aspect, no loading layout shifts. Native drag/resize/maximize/Snap. No marketing slogans or invented popularity.
- Home: search, category, kind, pack/style and installed filters; title/date/size sorting, details and real install actions. Library reads local JSON without contacting the feed. Cached Home survives outages. Empty future categories clearly say no content exists.
- Install individual widgets or a whole pack; deduplicate style dependencies. Real downloaded packages, integrity/compatibility validation, bounded extraction, staging and atomic registration. Install does not activate. Add to desktop is explicit. Active removal disables its instances. Offline removal does not depend on a feed.
- Native source owns saved state and credentials (DPAPI); React never receives saved secrets. General, Appearance, Widgets, Connections, Permissions and About preserve existing settings and provider operations.
- One-shot semantic icon motion, 160–240 ms transitions; reduced motion and high contrast. Optional original short sounds for navigation, placement and completed installs, off by default, own volume, no overlap/burst, no sounds in Snooze/fullscreen/voice sessions.

## Interfaces and content

CatalogDocument/CatalogItem/ContentPackage, InstalledContent, InstallProgress and runtime capability registry. Native ContentStore owns persistent install state. Official HTTPS feed contains versioned metadata and hash-addressed declarative package archives. Authoring/publishing script produces the feed and packages without private state. The development feed may be served explicitly on loopback; production uses the public repository. No downloaded scripts, assemblies or arbitrary native code.

Existing settings migrate built-in content locally once; new profiles install nothing. Existing widget positions, sizes, providers and opt-ins survive. A new content variation needs only a compatible package; new module behavior needs a reviewed engine update. Pets, docks, taskbar and Alt+Tab engines are out of this release.

## Tasks and acceptance

- [x] 1. Core catalog/install state, real package creation and fail-path tests (integrity, cancellation, traversal, dependency sharing, offline removal, migration).
- [x] 2. Native platform commands/settings, monitor placement, asynchronous IPC and shell process lifecycle/runtime/cache handling.
- [x] 3. React design system, Home/filter/detail, Library, Downloads and complete structured Settings; live integration, original branding and sounds.
- [x] 4. Build/install/update/uninstall integration, sanitized public repository and official content endpoint.
- [x] 5. Native/frontend regression tests, real-host visual and interaction checks, cold/suspend observations, final independent review and fixes. Unmeasured hardware/performance acceptance remains listed below and in docs/verification.md.

Verify DPI 100/150/200%, long labels, keyboard/focus, reduced motion, network failures, missing runtime, install while UI closes, resume after state changes and monitor removal. Run current core/assistant suites. Measure all process/private/working-set memory and report GPU/DWM separately if measured. Never label unmeasured hour/100-cycle performance as passed.

## Execution ledger

- Baseline: original current source core tests 87/87 passed; Node 24.19.0, npm 11.17.0. Dedicated managed worktree and codex/premium-platform branch; 259 changed/untracked source paths copied in, original working tree preserved.
- Ruling: use the same EXE with a separate --shell-host process role rather than an extra independently published .NET host; this preserves single-file delivery and shared packaged assets without an extra runtime distribution.
- Ruling: existing uncommitted sources are required input, not changes to discard or revert. Only sanitized source will be committed after a secrets review.
- Core evidence: 13/13 content checks and 6/6 native platform checks passed. Frontend checks: 8/8. A cross-provider credential regression and malformed persisted view handling were verified RED to GREEN.
- Ruling: a hidden management host with an unsaved draft remains alive until Save or Discard, preserving entered settings. Drafts are never persisted to disk; this exception costs additional host memory while a draft exists.
- Ruling: production content uses schema-versioned catalog/v1 in the authorized public repository. Old local generated catalog paths are excluded from publication.
- Live evidence: actual WebView2 Home and Library, nine installed Glass widgets and a native clock added from a downloaded descriptor. First new-profile cold ready was 2989.7 ms. Hardware mixed DPI, hour-long memory and 100-cycle acceptance are not measured.
- Final independent review: no Critical, three Important, no deferred minors. Fixed malformed registry/cache recovery, removal/install conflict changing active instances, and automatic eviction dropping visible/dirty UI. All five reproductions failed before the patch, then core 16/16 and native 8/8 passed.
- Review boundaries: publishing credentials were audited locally without logging values; prior assistant internals are preserved and covered by their regression suites; hardware mixed-DPI, sustained memory and live suspend-failure injection remain manual checks. Code policy for draft protection is tested. A second real new-profile cold-ready observation was 1201.7 ms.
- Full build before review fixes passed core 87/87, assistant 75/75, browser bridge tests, native voice/composer/media/control/document checks and 100 compositor bind/dispose cycles. These are compositor cycles, not WebView suspend cycles.
- Final build after review fixes passed all default local stages and published a self-contained executable. Isolated install/uninstall safety checks passed 12/12. The public HTTPS feed was fetched by the real host, Glass installed nine widgets and completed successfully after management closed. TrySuspendAsync returned true; reopening reused the management process. A production-feed new-profile cold ready was 1137.8 ms.
- Delivery: public source and catalog at https://github.com/AyateAgh/YoWidgets, binary and SHA-256 at releases/tag/v0.6.0-preview. The normal Desktop project executable and sources were updated after checking for newer conflicting edits; existing encrypted connections and widget IDs were verified unchanged. The original executable is retained under ignored artifacts/rollback for recovery.
- Hosted-CI ruling: deterministic core/frontend/transport/platform checks run with SkipVisualTests, because hosted desktop animation settings vary. Default local builds still execute the full visual suites. The live-orb test now asserts the actual Windows motion preference rather than assuming animation is enabled. The first public full CI run passed; a later hosted visual run exposed that assumption before the CI split.
- Final integration correction: owned cleanup now recognizes management Cache, declarative Content, catalog-cache and view state. Unknown content is preserved; an active engine sharing the data root blocks removal even from a different executable path. New cleanup/active-root cases failed first, then the lifecycle suite passed 15/15. Hosted CI for the application source passed after the visual-test split.
