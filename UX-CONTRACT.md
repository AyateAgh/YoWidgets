# YoWidgets interaction contract

Locale: ru-RU, OS timezone. Native WPF UI, not a browser application. Canonical owner is explicit for each recurring behavior.

| Capability | Canonical owner | Source of truth | Allowed variants | Verification |
|---|---|---|---|---|
| Form | Ui.Input, PasswordBox, MainWindow settings | This contract | ordinary / secret | native UI state inspection |
| Select/Listbox | Windows native context menu | DESIGN.md | widget S/M/L | keyboard and menu |
| Date | WidgetContentFactory.Calendar | This contract | display only | month/day layout |
| Scrollbar | Controls.xaml + native ScrollViewer | DESIGN.md | vertical page / widget text | screenshots and keyboard |
| Toast | MainWindow.Status | This contract | info / error text | UI Automation live region |
| CRUD | WidgetManager, SettingsStore | cleanup-contract.md | enable / add / disable | tests + restart |

Enable/add immediately opens the widget and persists state. Disable closes only that widget. Disabling all also stops data activity and removes owned autostart. Snooze hides widgets and cancels data work; resume refreshes only enabled services. Closing management retains the tray; explicit Exit disposes the application. Full uninstall is enabled only in a validated installed copy; source workspace deletion is blocked.

Missing weather has no invented temperature. Failed refresh preserves previous values and reports error. Media actions use source capabilities and report rejection. Missing notifications consent/identity is visible; local hiding never clears Windows notification history. AI requires configured endpoint/model/key and manual send; cancellation and timeout are visible. Input and key text are never logged.

Async generations and cancellation prevent obsolete work applying after suspension. Settings write via a temporary file and keep a recovery copy. Bad JSON is preserved as .corrupt. Widgets are independent floating windows, with no Explorer parent. They are clamped to an attached work area; reset positions uses a centered grid. Duplicate instance IDs normalize to distinct IDs so cleanup cannot lose a window.

Keyboard: Tab/native controls, native context menus, arrow/Shift+arrow positioning, Escape opens management. File picker popup is intentionally OS-owned. No browser alert/confirm/prompt. The separate removal page describes exactly what the terminal delete button removes before execution. Common system parameter rollback is explicit and conflict-aware.

Native equivalents replace HTML/ARIA-only audit items: accessible WPF names, AutomationProperties.LiveSetting, native control focus and keyboard actions. Unsupported hardware/consent scenarios must be labelled unverified in docs/verification.md.
