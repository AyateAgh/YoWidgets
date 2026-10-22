# YoWidgets Assistant Bridge

This optional Chrome/Edge companion is separate from the SoundCloud bridge. It lists open HTTP/HTTPS tabs and reads only the tab explicitly attached in YoWidgets. Tool clicks/typing require approval in the desktop app. Password inputs are excluded; no browser cookies/password database is read. The extension needs tabs/scripting and website access to inspect rendered page content. It runs only in your local browser, and communicates with a paired loopback desktop listener at 127.0.0.1:37892.

In YoWidgets choose **+ → Browser tabs → Connect browser**. Open chrome://extensions or edge://extensions, enable Developer mode, choose Load unpacked and select the exported companion folder. Review the browser's permissions. Open the extension popup, paste the pairing code copied from the desktop setup, and connect. Do not share the code. The app's API key is never sent to this extension. Browser-internal pages, extension stores, closed tabs, canvas-only content and password inputs cannot be read as ordinary page text. Long pages can be read in successive text slices. Frames from other origins are not traversed.

To disconnect remove/disable this extension or clear its pairing data. YoWidgets Snooze/exit closes the local listener. Uninstalling the Windows app does not remove the browser extension.
