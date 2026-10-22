# YoWidgets SoundCloud Bridge 0.5.2

This optional Chrome / Edge extension connects the **real SoundCloud miniplayer's Shuffle and Repeat buttons** to YoWidgets. It does not create another queue, change audio, copy login tokens or send playback data to a cloud service.

## Install once

1. Keep this folder at its current location, or extract `artifacts/browser-bridge.zip` to a permanent folder.
2. Open `chrome://extensions` (Edge: `edge://extensions`). Enable **Developer mode**.
3. Choose **Load unpacked** and select the folder containing `manifest.json`.
4. Reload the SoundCloud page / installed browser app. Start YoWidgets and enable its Music widget. The Settings page reports **SoundCloud bridge connected**.

The extension ID must be `fllmnphklooimbfkdomfjlgjnphkkncm`. Do not change the manifest's public key. There is no private signing key or account credential in this folder.

## What it can access

- Only `https://soundcloud.com/*` and the local YoWidgets connection at `127.0.0.1:37891`.
- Reads the visible miniplayer's track title, artist, duration, playing status and queue-mode buttons.
- Clicks only Shuffle or Repeat, only after a widget command for the same track and duration. Reads the source's resulting state before confirming.
- No history/cookies/password permissions, screen/audio capture, remote debugging, login automation or Spotify credentials.

One matching SoundCloud tab is required. If two tabs report the same track, duration and playback status, YoWidgets hides the controls rather than guessing. YouTube/TikTok do not acquire queue buttons. The extension reconnects within 30 seconds after YoWidgets resumes; it stops sending when the app is closed or snoozed. The browser may show a failed localhost connection while YoWidgets is closed; this is expected.

## Remove

In the browser's Extensions page, remove **YoWidgets SoundCloud Bridge**. Delete its extracted folder if desired. This manually loaded optional extension is a separate browser component; uninstalling the Windows app does not remove its browser entry. It stores no persistent user data. No browser preferences are modified by YoWidgets.

## Limits and checks

SoundCloud's website can change its markup. Missing/disabled controls are hidden; source refusal is reported without pretending the mode changed. Background browser scheduling may add latency. This adapter does not add Spotify Web API authorization, legacy iTunes COM or arbitrary video subtitles. Native player support depends on its Windows media-session capabilities. Lyrics are resolved separately by YoWidgets and are shown only when available; line timing is never inferred from unsynchronized text.

Run `node --test integrations/browser-bridge/provider.test.cjs integrations/browser-bridge/content.test.cjs` from the repository. C# transport integration tests run with the core test suite. Those tests are not a substitute for checking a user-installed extension against the current website.
