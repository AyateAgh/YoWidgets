# Browser and computer agent sessions

User scope: immediate local browser-tab discovery, actual browser navigation/input/testing tools, and visible computer control with a model-labelled cancellable banner, animated screen-edge glow and pointer movement. Keep provider-native function tools and the existing glass/mention design. Do not import browser cookies, enable debugging flags or install extensions silently.

1. Add bounded on-demand Windows UI Automation discovery for Chrome/Edge tab titles. Explicitly attaching a tab selects it and records its real address; page content enters API context only after Send. Keep the paired extension as a richer alternative.
2. Expand the owned WebView2 browser into a persistent tabbed host with real DOM snapshots, navigation, click/fill/scroll and screenshot tools (native keyboard via Computer use). Models may access only user-attached tabs or tabs they opened in this task. Inspect after each action; reject stale/protected controls and arbitrary model JavaScript.
3. Add task-scoped, target-specific control consent and a shared visible control session. Display model name, browser/computer activity, a Stop button and global Escape while control is active. All edge surfaces are input-transparent; the banner alone is interactive. Stop revokes the session and cancels pending input/API work.
4. Extend native desktop tools with bounded scroll/drag and smooth pointer motion, fresh target checks and visible progress. Keep protected apps/password controls excluded. Use native accessibility data alongside screenshots so text-only providers get a usable fallback.
5. Test against owned browser/desktop fixtures, including denied consent, stale snapshots, navigation, actual field input and cancellation. Never send personal browser data or control user apps during QA. Run the existing suite, publish, update the managed installation and restart it with the saved API connection.

Limits: no guarantee that any model can perform every GUI task; protected/inaccessible browser content and elevated/secure desktops can fail. Signed-in browser sessions stay in their original browser. No shell tool or automatic background work after app exit is introduced. Provider response speed remains provider-controlled.


Completed 2026-10-21. Evidence is recorded in docs/verification.md, including the full build, native owned Chrome input fixture, real public WebView read, global Escape inspection and matching published/installed EXEs. No production account request was made for this revision.
