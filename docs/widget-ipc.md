# Widget commands and live voice actions

YoWidgets widgets share one process, so internal commands go directly to `WidgetCommandHub`. Other local processes can use the same allowlisted dispatcher over a Windows named pipe. Shared memory is unnecessary for these small command/result messages.

The pipe name is `YoWidgets.WidgetBus.` plus the first 20 uppercase hex characters of SHA-256 over the uppercase absolute data-directory path. The default root is `%LOCALAPPDATA%\YoWidgets`. The server uses `PipeOptions.CurrentUserOnly`: only the same Windows user/elevation can connect. No TCP listener, API key, arbitrary shell, executable launch or agent task is exposed through this pipe. Apps running as the same user can issue the listed widget commands.

One UTF-8 JSON request per connection, terminated by a newline, at most 16,384 characters. Responses are one JSON line: `{ "ok": true, "result": ... }` or `{ "ok": false, "error": "..." }`. A connection expires after 12 seconds. Widget work runs on the UI dispatcher. The listener closes when YoWidgets quits.

Use PowerShell 7 from the repository:

```powershell
./scripts/widget-command.ps1
./scripts/widget-command.ps1 -Action media -ArgumentsJson '{"command":"next"}'
./scripts/widget-command.ps1 -Action calendar_add -ArgumentsJson '{"title":"Review match replay","due":"2026-10-22T18:00:00+03:00"}'
./scripts/widget-command.ps1 -Action status -ArgumentsJson '{"text":"Ready for your next task"}'
```

The packaged client is `widget-command.ps1` beside the installed executable. It accepts an optional `-DataRoot` for an isolated profile.

| Action | Arguments | Result |
| --- | --- | --- |
| `state` | `{}` | Enabled widget types, current media/focus/local calendar state; no credentials/settings |
| `media` | `command`: next, previous, pause, resume, play (toggle), seek; optional `seconds` | Actual system media transport acceptance/state or an error; no fake success when no player exists |
| `calendar_list` | `{}` | Local YoWidgets tasks |
| `calendar_add` | `title`, `due` (ISO date with explicit timezone) | Saved local task; shown by the calendar task count and date indicators |
| `calendar_complete` | `id` from list | Persisted completion |
| `focus` | `minutes`: 1–240, or 0 to stop | Actual focus end |
| `status` | `text`: 1–200 characters | Desktop status overlay for eight seconds |

Calendar tasks live in the encrypted-key-independent settings file. This calendar is local to YoWidgets, not Google Calendar/Outlook. Ambiguous dates are rejected. Persistence failures restore the previous list.

Live Gemini/OpenAI sessions can declare `widget_command` and `execute_task` when **Settings → Live voice → Allow actions** is enabled. `execute_task` delegates browser/computer/file/plugin work to the normal chat agent with its workspace, actual tool results, the saved control confirmation preference and control overlay. It uses the configured chat model/key as well as the live voice connection, so ordinary chat API usage also applies. End call cancels in-flight work; already completed changes can remain and are shown in chat. Existing file Undo remains available in that chat.

Ukrainian, English and Auto are available; Auto is the default. Saved Russian preferences migrate to Auto. Gemini native audio does not accept an explicit language-code setting: the host anchors language in session instructions. OpenAI additionally receives the chosen ISO transcription language and a spelling/context hint. Speech transcription remains probabilistic; unclear speech must prompt clarification, and names or statistics must be verified using real sources.


Browser/app control runs directly by default. **Settings → Live voice → Ask before browser and app control** restores confirmations for both text and voice tasks when enabled. Stop/Esc and the active control overlay remain available in either mode. Physical keyboard and right/middle mouse tools support discrete game/app interactions; this does not guarantee real-time Dota 2 play.
