# Execution ledger — plan: docs/superpowers/plans/2026-10-19-yowidgets.md

Ruling: User explicitly requested starting implementation; proceed inline without asking to authorize the same work again.
Ruling: This is a new non-Git directory with only examples/docs; initialize a feature branch here. No existing branch or user code needs worktree isolation.
Ruling: Official IVirtualDesktopManager has no PinWindow method. Do not use private IVirtualDesktopPinnedApps or fabricate an API; validate desktop behavior and document platform limits.
Pre-flight: Task 1 produces settings/placement/glass used by Tasks 2–4; service suspension in Task 2 is consumed by WidgetManager in Task 3 and cleanup in Task 4. Final verification covers all tasks.

Implementation: native WPF application, nine widget types, HLSL optics, wallpaper COM, local settings, cancellable services, per-user ownership installer/cleaner and optional sparse-identity scripts created. Service/content files are flat under src/YoWidgets rather than the preliminary Services/Widgets folders.

Verification: Release/self-contained publish succeeded; 20/20 core tests and 8/8 lifecycle cases pass. Native UI, real weather fallback, real media metadata, Snooze/resume and actual desktop attachment exercised. Real default per-user install/uninstall removes files/data/registration and preserves source/examples.

Review: fresh final reviewer raised seven important paths. Fixed ownership preservation, ancestor reparse rejection, retry finalization, private opt-out, delayed service restarts, desktop loss reporting and per-device originals. Follow-up identified two more recovery cases, now regression-covered; final bounded read-only review reports no blockers in those paths.

Resource ruling: do not label an automation-heavy foreground sample as normal idle. Recorded both high visible/UIA readings and low background/tray readings in docs/verification.md. Snooze removes widget surfaces and preview shaders; no zero-resource guarantee.

Deferred hardware/external validation: signed notification identity/consent, AI credentials, hardware mutations/rollback, mixed DPI/virtual desktops, Win+D, Explorer restart, GPU/game measurements and long-run stress. Explicitly unverified. No private PinWindow or system security prerequisite changes.
