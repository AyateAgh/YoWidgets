# YoWidgets 0.6 verification

## Automated checks

- Core regression suite: 87/87. Assistant transport suite: 75/75.
- Content platform: 16/16, including actual ZIP resources, SHA-256, traversal, cancellation, dependencies, offline removal, custom collections, migration, malformed registry backup recovery and automatic eviction policy.
- Native platform: 8/8, including secret-free snapshots, provider key separation, async pipe events/reconnect, invalid settings rollback, actual installed widget instances, failed removal preserving the desktop and null catalog fallback.
- Frontend: 8/8, including combined filters/search, partial collections, dependency sizes and malformed saved-view recovery.
- Full build also runs browser bridges, native voice, composer, generated media, browser/control scope and DOCX/PDF/XLSX/PPTX checks. These use isolated fixtures and do not consume a user's saved API keys.
- Native compositor probe: 100 bind/property/animation/disposal cycles. These are compositor cycles, not WebView suspend cycles.

## Real host observations

Home and Library rendered in the actual WebView2 host using an isolated profile. Installing Glass from a development HTTP feed produced nine installed widgets, and Add to desktop created a live native clock from the downloaded descriptor. First new-profile cold-ready observations were 2989.7 ms and 1201.7 ms in separate sessions. Timing varies with runtime/profile/system state; no fixed latency guarantee is made.

## Independent review

The final read-only review reported no Critical and three Important findings. Each was reproduced before fixing: malformed persisted JSON, failed uninstall changing active instances, and automatic UI eviction dropping a draft. The new core/native tests then passed. No minor findings were deferred by that review.

## Manual acceptance remaining

Hardware mixed-DPI transitions at 100/150/200%, monitor removal, missing-runtime installation under restrictive policy, hour-long process/GPU/DWM measurements and 100-cycle WebView suspend/resume remain unmeasured. Draft eviction policy is covered by code tests; live suspension-failure injection has not been performed. Native Windows notification identity and external services depend on the user's permissions and configuration.

The executable is self-contained for .NET, but management needs WebView2 Evergreen. It is not publisher-code-signed. SHA-256 package validation is not a publisher signature. No enterprise certification, universal application/game compatibility, fixed RAM usage or zero-latency claim is made.
