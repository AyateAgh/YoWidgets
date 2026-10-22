---
version: alpha
name: YoWidgets
description: Small optical glass instruments above a Windows desktop; quiet local controls in English.
colors:
  background: "#11151D"
  surface: "#1A202C"
  field: "#131A25"
  text: "#F4F7FB"
  muted: "#B4C0D1"
  primary: "#78ADFF"
  border: "#303B4C"
  hover: "#2B374A"
  danger: "#FFB4AE"
typography:
  display:
    fontFamily: "Segoe UI Variable Display, Segoe UI, sans-serif"
  body:
    fontFamily: "Segoe UI Variable Text, Segoe UI, sans-serif"
rounded:
  sm: "10px"
  md: "18px"
  lg: "32px"
spacing:
  control: "12px"
  panel: "24px"
  page: "34px"
components:
  button:
    rounded: "10px"
  panel:
    rounded: "18px"
  glass:
    rounded: "32px"
---
# YoWidgets design system

## Overview

Product register: a personal Windows utility, not a landing page. English UI; en-US dates and local system timezone. Apple/MacShell video references define the optical material, while management uses restrained Windows controls. The signature is a real live backdrop, restrained neutral tint and a directed optical edge. Desktop widgets never sample a system wallpaper file. Avoid neon gradients, fake weather/media, decorative graphs and endless animation.

Runtime ownership model B: src/YoWidgets/Ui.cs owns semantic palette, typography and shared controls; Controls.xaml consumes the same resources for native WPF templates. This file mirrors those tokens and explains intent. Glass geometry/optics live only in shaders/Glass.hlsl. Mapping: background→Background, surface→Panel, field→Field, text→Text, muted→Muted, primary→Accent, border→Line, hover→Hover, danger→Danger.

## Colors

Dark management shell, quiet slate panels and blue active controls. Widget content uses white Medium/Semibold over fixed pack-owned neutral tint, with a small shared text shadow. Glass and Liquid Glass own their material parameters; no global material/tint/blur switch exists. High Contrast must avoid relying on optics for meaning.

## Typography

Ui.BodyFont and Ui.DisplayFont own Segoe UI Variable Text/Display with Segoe UI fallback. Body and secondary copy use Medium (500); emphasis and buttons use Semibold (600), with no network font loading. Display heading 30px/semibold, navigation and body 13–15px, explanatory labels 11–12px. Clock type is 47–76px and scales inside constrained frames. Numbers and capability states are real data.

## Layout

210px navigation; natural vertical scrolling in the right content area. Minimum desktop window 860×600; 1100×790 default. Two-column pack library followed by a three-column catalog inside each pack; settings have one scroll owner. Separate widget windows use S/M/L footprints and 20px optical padding; content is inset 18px. Screen bounds are physical pixels, sizes are DIP. This deliberate native mapping avoids pretending web viewport rules apply to HWND coordinates.

## Elevation & Depth

Quiet management panels have a single hairline border. Both packs use a Windows Composition Backdrop companion surface beneath transparent WPF content. CompositionBlur implements the documented IGraphicsEffectD2D1Interop contract for a GPU Gaussian effect, using balanced optimization and hard borders. Sigma is 2 for Glass, 3 for Liquid Glass, rising to 4.5 while moving/expanding and easing back over 260 ms. The backdrop visual is fully opaque inside its rounded clip, replacing the sharp background with the live blurred result; WPF content above stays transparent and sharp. This avoids the excessive diffusion of HostBackdrop and the sharp duplicate caused by low-opacity mixing. Neutral optical tint is 10%/14%, with a narrow bright silhouette and a broader, interaction-responsive rim in Liquid Glass. Parameters live in YoWidgets.Core/WidgetPacks.cs, not settings sliders.

Glass.hlsl draws premultiplied-alpha tint/rim only, with zero coverage outside its SDF and no image sampler. There is no wallpaper reader, decoder, atlas, image fallback, screen-image capture or compositor-surface readback in the production rendering path. Unsupported composition falls back to neutral transparency; shader failure retains the live compositor with a simple rim. No continuously running application rendering loop is used at rest.

## Shapes

10px controls, 18px panels, 32px glass radius (clamped on small widgets). Rounded controls keep visible focus and pressed/disabled states. Management uses a custom 44px dark titlebar with native resize behavior and named minimize/close controls. Five-pixel rounded scrollbar thumbs use invisible tracks and no arrow buttons. Catalog panels have a 1px #20FFFFFF border.

## Components

Ui.Button owns hover/focus/disabled/primary behavior. Ui.Toggle and Controls.xaml own keyboard-accessible switches. Ui.Input and PasswordBox own local fields; sensitive values masked. OS-owned file dialogs are intentional. Authored narrow scrollbars remain visible with shared colors. Status text is a stable live region at the bottom, not a disappearing critical error.

The calendar displays dates rather than editing them. Native context menus own widget size selection; authored dropdown geometry is not needed. Optical transitions respect SystemParameters.ClientAreaAnimation. Player expansion is 220ms and interrupted animations must settle at the latest requested size. Snooze removes surfaces and stops periodic work.

## Do's and Don'ts

- Do use one optical shader for previews and desktop widgets, with a single pack parameter owner.
- Do distinguish disabled capability, missing permission and network failure.
- Do keep native keyboard access and non-drag positioning.
- Don't describe an untested virtual desktop mode as officially pinned.
- Don't claim Apple-native refraction or pixel parity: this is a Windows visual adaptation with system backdrop sampling and a bounded contrast estimate.

## Historical material and player revision 0.3 (material superseded by 0.5)

References: both local examples videos and Apple WWDC25 Meet Liquid Glass (https://developer.apple.com/videos/play/wwdc2025/219/). Adaptation uses a quiet center, broad refracting meniscus, directional highlights and restrained RGB dispersion. It is a wallpaper-based WPF material, not Apple native glass or refraction of arbitrary windows. Motion respects Windows reduced-animation preference.

WidgetVisuals owns vector symbols, dynamic widget ink, circular transport buttons and pill actions. WidgetCards owns aligned clock, weather and calendar layouts. MusicView uses a 54px rounded cover, truncating metadata, independent time columns and five evenly spaced transport controls. Cover column collapses completely when no image is available. Website logos remain 88px in the expanded player; track artwork is 190px. Compact and expanded surfaces are 184/410 DIP tall.

Native DragMove owns mouse tracking. WallpaperProvider prepares a bounded monitor atlas at idle; the motion path only changes its Viewbox. CompositionTarget.Rendering is subscribed only during motion/size animation and removed afterwards. A final cropped background is refreshed after settling. Drag diagnostics distinguish preparation time, loop time and actual decoder misses.

## Historical live material and contextual transport 0.4 (package selection superseded by 0.5)

Two built-in material packages share content/layout. Glass owns wallpaper optics. Live owns a no-redirection companion HWND, a documented Windows.UI.Composition DesktopWindowTarget, CreateHostBackdropBrush and rounded geometric clip. Its visual is positioned directly below the content HWND, follows physical bounds/DPI/Z-order and is disposed when hidden through Snooze or closed. Pixels never enter application memory. The normal WPF optical shader only paints tint/rim over this live surface; wallpaper sampling/preblur is bypassed. Windows owns the live blur radius and may change rendering under power/accessibility policy. Full arbitrary-window lens displacement and iOS pixel equivalence remain out of scope of this renderer.

The initial system-backdrop-type prototype was discarded after native inspection showed inactive opaque fallback. The shipped renderer uses host-backdrop composition. Live uses stronger regular tint and higher secondary-text contrast to remain legible over bright windows. Animation obeys reduced-motion settings; hidden controls immediately stop accepting input.

MusicView owns a 220 ms symmetric side-column reveal plus opacity/slide. Previous/play/next stay centered while the outer columns retract. Visibility follows native capability flags plus explicit video/source policy, not guessed website feature availability. A song mentioning a video brand is not automatically a video source. MediaClock owns timeline projection using source update age and monotonic elapsed time. Metadata/artwork refreshes are separate from transport events and the 10 Hz visible-player progress timer.

Live is the default for new profiles. WallpaperProvider owns a single debounced shell background/display listener. It clears both crop and movement caches and invalidates retained per-widget movement brushes before refreshing widgets and catalog previews. No wallpaper polling or desktop capture runs in the background. The lighter Glass package deliberately samples static Windows wallpaper; animated wallpaper/window content requires Live.

## Widget packs 0.5

MainWindow.BuildPackLibrary is the pack entry point; BuildWidgets owns a selected pack catalog and pack-scoped controls. Glass precedes Liquid Glass. The latter is labelled Inspired by Apple, not published by Apple. Both offer the same nine functional widget types, preserve independent enabled states and may coexist. PackPreview owns small authored gradient previews; they are explicitly style previews and never read desktop wallpaper. Library previews show static current-time samples, while actual clock widgets update on wall-clock minute boundaries.

WidgetState.Pack is persisted per instance. Legacy instances without Pack migrate to liquid regardless of the discarded global MaterialPackage. IDs, positions, sizes, integrations and user content are preserved. The appearance page owns only behavior/placement. Runtime font/color ownership remains Ui.cs/Controls.xaml. Future third-party workshop execution, downloads and publication are outside this release; only these built-in packs are available.

BackgroundContrast owns one shared 2-second timer and at most one background job. It reads four RGB color values in each visible widget's transparent outside margins to estimate light/dark ink; it never captures or stores an image. The compositor backdrop itself remains unreadable. Hysteresis plus a 220 ms ink transition prevents flicker. Moving windows are not sampled synchronously in drag/render callbacks; obsolete readings are discarded after movement, Snooze, close or disposal. The timer stops with no widgets or in Snooze. This is a margin-based contrast estimate, not per-pixel Apple vibrancy.

## Lyrics and native media 0.5.1

MediaService owns Windows GSMTC integration for both installed players and browsers. There is no browser requirement, tab scraper, extension or simulated keypress bridge. Native source capability flags remain authoritative; the observed SoundCloud session publishes Music with Previous/Next enabled and Shuffle/Repeat false, so those two commands remain hidden. iTunes/Apple Music identities are treated as music when a native session does not report a type; this is not a legacy iTunes COM adapter. Apps which do not publish GSMTC require a separate adapter. Native play/pause falls back to the separately advertised play/pause command when toggle is absent; late command completions cannot alter a replacement session.

LyricTrack/LyricLookup/LyricClient in YoWidgets.Core own metadata cleanup, strict title/artist matching, duration validation, a 12-entry cache and sequential LRCLIB requests. Exact lookup falls back to filtered search, including the primary artist for uploaded tracks with multiple artists. Modified-speed versions only use line timestamps when duration matches within two seconds. Otherwise they may show plain lyrics, explicitly labelled without timing. No fabricated word timings, audio capture or automatic transcription exists. Lookup respects Retry-After and cancellation; malformed/offline responses never leave an actionable Lyrics icon.

DataServices owns generation-checked lyric requests. Changes of track/source, settings disable and Snooze cancel and clear the previous result. Settings opt-in starts lookup immediately. Lyrics is Collapsed (including keyboard/accessibility hit targets and reserved space) until confirmed text is available. MusicView owns that capability gate; LyricsView owns the three-line layout and binary-search selection against MediaClock at the existing 10 Hz visible-player timer. The current line is Semibold 23px, neighbors 15px, with a 160ms reduced-motion-aware opacity transition. Untimed text has one scroll owner. Loading/failure text is never mistaken for available lyrics.

## Source control bridge 0.5.2

The previous 0.5.1 no-extension boundary is superseded by an optional, separately installed SoundCloud extension. Native Windows sessions remain the primary transport. BrowserMediaPolicy requires one unambiguous SoundCloud tab matching the current Chrome/Edge session's title, duration and playing status. BrowserMediaBridge owns a localhost-only WebSocket listener, fixed extension Origin validation, bounded states/messages and acknowledged source commands. MusicView reads confirmed source modes and the matched source artist; it never shuffles a local copy of the queue. The extension clicks only the actual miniplayer's Shuffle/Repeat controls, reads their new state, and relays it before acknowledgement. Unknown sites, native player identities and ambiguous tabs cannot route through this adapter. No OAuth credentials, website private stores or browser preferences are accessed. The Windows app cannot remove a manually loaded browser extension entry; that separate removal is explicit in its README and the cleanup contract.

Composite upload labels such as an edit prefix followed by the original artist use an indexed artist/title lookup. They may show matched plain lyrics but never borrow original timestamps. Null record duration is skipped safely when scoring other candidates. These paths have core regression tests. Native accessibility test launches may use --inspect-widget-windows to expose the same content windows in the taskbar; normal launches retain tool-window behavior.

## Assistant chat · October 2026

The desktop assistant keeps the established Segoe UI Variable / Cascadia Code type roles and Ui.Theme tokens: Background #11151D, Field #131A25, Panel #1A202C, Text #F4F7FB, Muted #B4C0D1, Accent #78ADFF, Line #303B4C. AssistantView owns the chat layout and a graphite inset (Field at 220/255 opacity) so changing wallpaper or adaptive widget ink never reduces message contrast. The outer GlassSurface remains the shared optical material. Header → scrolling conversation → anchored composer is the reading order. S/M/L content sizes are 360×420, 440×540 and 560×680 DIP. The four-point mark is the empty-state signature; the conversation itself stays quiet.

AssistantMarkdown owns safe native selectable prose, headings, lists, inline emphasis/code and fenced code surfaces. Syntax accents reuse Accent/Muted; code can scroll horizontally through the shared Controls.xaml scroll owner. Raw HTML is plain text. No assistant output runs automatically. Icon geometry extends WidgetVisuals, with accessible button names, focus and hover from its existing primitive.

New messages fade/slide over 180/220 ms; waiting dots pulse only while waiting for text. Animations obey ClientAreaAnimation and stop on unload. Scrolling follows the stream only while the reader stays near the bottom. Empty state contains one invitation; new-chat appears only once a conversation exists. Locale remains en-US while user text and AI answers retain their language.

## Compact assistant · current revision (supersedes the earlier graphite-inset chat)

The current assistant is a bare glass composer, with no nested dark panel, heading, provider/model label, settings button, welcome copy or permanent keyboard footer. The outer GlassSurface is the only material. M is 400×72 DIP of visible glass; S/L widths are 360/480 DIP. Composer controls use 32/34 DIP circles, 14 DIP Segoe UI Variable, and adaptive WidgetInk. Plus is left; microphone immediately precedes Send on the right. Multiline editing grows to 160 DIP and the response state to 540 DIP. Outer 20 DIP margins remain reserved for shared optics/shadow; content inset is 8 DIP.

AssistantLayout owns physical-coordinate anchoring to the current monitor work area. Expansion chooses the side that fits, pins the composer's outer edge, and clamps on a small display. WidgetWindow animates Height and Top together over 260 ms, with reduced-motion fallback. The composer stays at the top when expanding downward and at the bottom when expanding upward. User collapse preserves conversation; the native context menu can reveal it again. Default visibility is always the compact composer on a new application session.

Text, icons, selectable responses and code now derive from WidgetInk. AssistantButtonInk is its dark/light inverse; AssistantCodeInk is #144986 on light glass or #BEDBFF on dark glass; AssistantInset and AssistantLine are low-opacity tonal surfaces, never an opaque second panel. The shared contrast sampler and glyph shadow stay authoritative. Processing shows a customizable label and three staggered dots only while the conversation is visible and generating. No animation timer runs for hidden processing.

Attachments use a horizontal chip row, native multiselect picker and whole-composer file drop target. Each chip has a filename, size tooltip and accessible remove button. Errors remain inline. Attachment and voice activity add temporary states, without permanent explanatory text in the idle composer.


## Voice and motion refinement (current)

AssistantView keeps the compact titleless glass composer. The chosen service's bundled SVG mark appears beside the input and moves into the response header when opened. AssistantSettingsView uses the shared dark native ComboBox template with SVG items, focus, keyboard navigation and scrolling; it never exposes a provider URL. PremiumIcon owns bundled vector glyphs throughout navigation, management, catalog previews and widget controls. Lucide outlines and Iconimate's gear replace Unicode icon glyphs. HoverMotion runs one 650 ms gesture on hover and repeats every 5 seconds only while hovered; cooldown survives re-entry/content replacement, and leave/unload/hidden/reduced-motion stop it. Idle icons have no timers. VoiceGlow and ThinkingOrb render at up to 30 fps only while active and visible, with static reduced-motion fallback. Audio levels come from the recording device (System.Speech AudioLevelUpdated on its local path), never random animation values in production.

Gemini dictation uses the dedicated gemini-3.5-transcribe model through the fixed Google Interactions endpoint with inline WAV and store:false, independently of the chat model. Response status must be completed; only model_output text is accepted. No remote file resources are created. OpenAI keeps its dedicated transcription endpoint. The recorder stops after 800 ms of detected silence instead of 2.5 seconds and retains its 30-second cap. Transcription has no application deadline and can be cancelled with the microphone button or Escape; cancellation/session guards prevent stale draft updates. No automatic billable retries are made. The 3–5-second goal is a latency target, not a guarantee against provider/network delays.

Expanded conversations animate the compositor's live backdrop blur to 8 px over 260 ms, returning to the pack's 2/3 px base when collapsed. Text is never blurred. Readability state survives material recreation and combines with drag/resize blur. No screenshot or wallpaper image is used as a glass background.


## Current attachment and conversation refinement (supersedes earlier editor / glow layouts)

VoiceGlow is attached to WidgetWindow.surface, spans the outer glass and animates its full spectral rim during recording, transcription and AI generation. It sits beneath text, never inside the composer field. The drop target draws a visible 2 DIP adaptive-radius accent outline at the same outer surface and clears on drop/leave/unload. ThinkingOrb and the custom processing label are the last child of the scrolling timeline, after the prompt and currently streaming assistant message. Empty assistant placeholders are hidden.

AssistantView measures the input's native text layout and animates field plus window height over 260 ms. Up to four complete visual lines are shown; further lines scroll at the same capped height. Clearing/sending returns to 34 DIP input height. ComposerInput owns the borderless native TextBox template, without duplicate padding or a default focus rectangle. The outer compact glass remains 72 DIP; attachments add 96 DIP for image/file tiles.

AttachmentPreview owns draft and sent-message tiles. Image/GIF/video posters are decoded from the in-memory snapshot, thumbnails are bounded and weakly cached, names remain visible, and hover/focus reveals removal only in the draft. User prompts and their attachments share a right-aligned bubble. Language files use bundled SVG marks (TS/TSX, JS/JSX, Python, Rust, Go, HTML/CSS, C/C++/C#, Java, Ruby, PHP, Swift, Kotlin, Vue, Svelte, JSON, Markdown); other documents use native vector file icons. GIF/video thumbnails are static posters, not autoplay players. HoverReveal fades and lifts copy/regenerate/code-copy controls only over the associated message/code block; keyboard focus on the controls also reveals them.

AttachmentMedia prepares GIFs locally as up to eight composited chronological JPEG frames, honoring frame offsets/disposal and enforcing dimension/decoded-pixel/frame-count/output-byte limits. Raw GIF bytes are never sent to the legacy Gemini Generate Content endpoint. Video uses Windows MediaComposition to create six chronological visual frames and a poster; Windows codec availability is required. Original files are not modified; a changed video is rejected. Gemini receives the original video. Other vision-capable service/model adapters receive sampled video frames with explicit labels stating that these are samples without audio. This is not full video/audio understanding. DeepSeek's text-only adapter rejects visual files visibly; model capabilities and provider quotas remain real constraints and no file is silently omitted. Retried turns reuse snapshots and frames; prepared bytes count toward the aggregate 12 MB context budget.

Chat generation, response headers and transcription now have no local cancellation deadline. Stop/Escape, account change, new chat, Snooze, widget removal and process shutdown still cancel work. UI updates are batched for rendering; API reads are not delayed. No automatic billable retry is performed. The old 30-second header, two-minute generation and 15-second transcription deadlines are removed.

## Local file agent · October 2026

The plus menu offers Attach files and Agent · choose working folder. Selection starts a new chat with a removable Agent folder chip. File access is off by default and is not persisted across restarts or provider changes. Native adapters declare seven file tools and run a streaming tool loop, preserving provider tool IDs, argument fragments and opaque signatures. The UI reports actual tool activity rather than private model reasoning. No shell tool is present.

AgentRunView displays incremental file stages and a final list with total and per-file line additions/removals. A 500 ms hover opens a bounded diff or raster before/after preview. The pointer may enter the preview; leaving both surfaces closes it after a 180 ms bridge delay and a 140 ms fade. Opening fades over 160 ms; reduced motion is immediate. Clicking opens a larger review. Counts compare original and final UTF-8 text; binary files have no text line changes. Large diff sections use a bounded comparison and previews identify truncation. Partial changes remain reviewable after cancellation or provider errors.

AgentWorkspace scopes relative paths to the chosen folder, rejects traversal, Git metadata, reparse points and hard links, and verifies read hashes before changing existing files. Undo uses original byte snapshots and refuses to overwrite newer external edits. Snapshots live only in this chat/process; resetting the chat, switching folders or quitting discards recovery history without undoing files. These tools are not an OS security sandbox against another local process racing path changes. Per-run limits: 1 MB per file, 40 changed files, 16 MB snapshots, 32 model rounds and 96 actions. Generation has no elapsed-time deadline.

## Unified agent status and review · current revision

AssistantStatusView combines the actual action label and one adaptive dotted orb. It lives in the agent journal during file work; the generic timeline status is suppressed for agent turns. Reading, searching, editing, reviewing tool results and streaming have distinct native vector motion, with 220 ms state crossfades and static reduced-motion frames. Transient tool operations never delay the API; while the model processes a tool result, the status describes reviewing that file. These are operation states, not private model reasoning. Visual reference: [AI Orb by Dudelnik](https://21st.dev/@konsstepnov/components/ai-orb), whose thinking-orbs states inform the native treatment; no browser/React runtime is bundled.

During generation the journal provides stable per-action expanders for inline diffs and raster previews, with full disk paths in themed 500 ms tooltips. Expanded content survives subsequent tool events. On completion, stop or error, the journal moves into a closed activity disclosure inside the single final changes card. The live summary and redundant Completed heading are removed. The default finished view lists changed files once. Empty streamed assistant bodies do not reserve a blank prose surface.

HoverReveal makes the full message/action envelope transparent but hittable, including the gap above the action row. Leaving starts a 260 ms bridge followed by a 150 ms fade; controls remain interactive until the fade ends. Re-entry or keyboard focus cancels hiding. Show motion lasts 160 ms and stopped/unloaded views leave no active timer. This shared behavior covers user and assistant actions, fenced-code copying and draft attachment removal.

## Composer and optional agent modules

The plus popup uses one compact, rounded, scrollable card with Add, Plugins and Browser tabs sections. File/folder selection is unified; at most one working folder is accepted. Goal and Planning render as removable composer chips and are read by the real tool runtime. A task keeps its goal in conversation memory; planning gates mutations until the native planning_decision tool saves a valid .md plan or explains why the narrow task needs none. Referenced external products require an actual source read. No independent scheduler or background goal service is implied.

Clipboard handling occurs only on explicit Ctrl+V/Shift+Insert and only intercepts file-drop, PNG or bitmap formats. Plain text follows normal editor behavior. Attachments preserve original immutable bytes separately from extracted provider text. Native Open/Copy/Download actions share those snapshots in composer tiles, message bubbles, code/image reviews and changed-file rows. Opening executable/code/HTML attachments uses a read-only viewer rather than execution. The local owned cache supports shell open and OS file-copy lifetime.

The Draw modal has an InkCanvas, pen/select/text/rectangle/ellipse/eraser, custom/preset colors, brush size and bounded undo/redo snapshots. Attach exports the actual canvas as PNG. The toolbar and modal use the existing vector and motion system with reduced-motion support.

Document plugins use Open XML SDK (DOCX/PPTX), ClosedXML (XLSX), PDFsharp/MigraDoc 6.2.4 (PDF creation), PdfPig (PDF extraction) and the Windows PDF renderer (actual PDF previews). Native schemas define supported typography, geometry, tables, images, headers and footers. Original attachment bytes remain available to tools; provider text extraction never replaces a downloadable Office file. Office XML is validated, XLSX formulas are checked for external/network behavior, and PDF output can be rendered back into a vision tool result. No macros, embedded scripts or general shell access are enabled. Unknown formats and unsupported operations report actual failures.

Browser use has two concrete implementations: a visible app-owned WebView2 window/profile for public pages, and a separately paired MV3 Chrome/Edge companion for existing tabs. The companion exposes tab metadata only after user pairing; content is requested only for an explicitly attached ID/URL. Native loopback pairing requires the exact extension origin and protected local secret. Navigation revokes attached-tab access. Reads return DOM text in bounded segments and visible control IDs. Browser clicks/fills resolve a fresh control and ask for a native per-action decision. No arbitrary JavaScript is accepted from the model. Cross-origin frames, browser-internal pages, canvas and protected content are not promised as readable. The SoundCloud bridge remains a separate, site-specific extension.

Computer use returns ordinary window metadata, and for a requested inspection sends UI Automation labels plus an actual selected-window PrintWindow image. Input follows the saved confirmation preference and requires a still-matching snapshot, PID, title, bounds and foreground target. Password controls, common credential/security apps and terminal processes are excluded. No broad unattended desktop session or terminal tool exists. Denied requests return tool errors and are not silently retried.

Generated documents have an 8 MB limit, atomic hash-guarded writes and the existing snapshot budget/Undo rules. The original 1 MB limit remains for normal text file editing. When no user folder is selected, enabled modes/plugins use an app-owned per-chat output folder, never an arbitrary working directory. Cache, output, browser profile and exported companion roots carry an explicit ownership marker for cleanup. External user workspaces are never managed as caches.

## Composer mention and glass-menu revision

ComposerMenu now reuses LiveBackdrop's actual GPU backdrop blur through SyncPopup, with a rounded 20 DIP clip, readability blur and a neutral translucent scrim. It never samples a wallpaper or screen image. Native composition failure/high contrast uses the established opaque Panel fallback. Popup move/resize hooks keep the companion under the popup HWND and close/unload disposes the material. Opening fades over 160 ms; explicit close fades over 140 ms. The plus mark has a separate 220 ms, reversible 0→45 degree state rotation; its ordinary hover motion is disabled so it cannot overwrite the menu-state transform. All motion observes the system animation setting.

The current GlassPopupMotion owner keeps popup lifetime explicit so outside clicks can finish the reverse animation. An input event hook tests the real transformed card and anchor bounds; anchor clicks remain the plus button's toggle, while other clicks dismiss without consuming the target's action. The old captured auto-dismiss guard has been removed. Hooks and render subscriptions detach on close/unload; plus rotation returns once.

ComposerMentions owns the token parser and PluginCatalog owns the module names, vector identity, blue instruction chip and instruction text. An active @token opens this same popup while keeping typing in the editor; additional contiguous text filters tools and live tabs. Email addresses are not mentions. Arrow keys navigate, Enter/Tab select, Escape dismisses without erasing the user's draft, and no-results never sends the prompt on Enter. Choosing a tool removes only the active @query and keeps surrounding draft text. The selected module appears as a removable blue icon/text chip inside the composer; sent bubbles retain that same module metadata. The chips and typed text deliberately remain separate native controls, preserving the established four-line TextBox/IME, paste and accessibility contract.

AiService snapshots selected instruction IDs per submitted user message, adds their known native-tool instructions to provider context, and preserves them for Retry. The ordinary visible prompt remains unchanged. Successful submission clears draft chips; missing credentials or preflight failures preserve them. New chat clears pending selections. Removing a chip removes that request's explicit instruction, rather than retroactively disabling tools used in earlier conversation history. Previously enabled native module capabilities remain available for follow-up work. No raw module name is mistaken for an executable plugin, and unknown IDs are ignored.

Instruction blue is a named native resource: AssistantInstructionInk, owned by WidgetWindow.SetAssistantInk (per-widget contrast) with WidgetVisuals.Tone supplying the shared preview fallback. It is #78ADFF over dark material and #124CA8 over bright material; both the selected module icon and label consume that same dynamic resource. This keeps the requested blue identity readable as the glass contrast estimate changes.


## Visible browser and computer sessions — 2026-10-21

AgentControlSession is the shared owner for per-request target grants, the model/action banner, global Escape and per-monitor input-transparent spectral edges. It is lazy until the first allowed target and remains visible between tool calls. The banner reuses the shared primary button and body typography; long model identifiers truncate with a tooltip. Ending or cancelling the run removes hooks/windows and revokes task grants. Native cursor movement is eased and bounded, with checks during movement and between input batches. Computer scroll/drag join existing click/type/key tools.

NativeBrowserTabs discovers browser chrome metadata on a single bounded UIA worker, skipping document subtrees during title discovery. Full active document URL is read as metadata via ValuePattern; it never guesses elided schemes. Selection/attachment and live inspection are distinct steps. External tab leases pin exact URLs; navigation requires reattachment. A paired companion is still the richer DOM route. The owned WebView browser is persistent for the AiService lifetime and uses themed, individually closable tabs. Only attached or request-created IDs are tool-accessible. A fixed serialized-argument DOM driver produces snapshot IDs and rejects stale/protected fields; actual operations require follow-up observation. Browser text and desktop labels remain untrusted data. No cookies, Chrome debugging flags, extension permissions or arbitrary model scripts are imported/enabled.


## App launching, activity details and monitor waves — 2026-10-21

InstalledApplications owns the bounded local launch catalog: standard Telegram/Discord locations, registered App Paths and Start-menu EXE shortcuts with no arbitrary shortcut arguments. Models receive opaque IDs, never a shell/path execution tool. Discord uses its normal updater with a fixed processStart argument. Opening follows the saved confirmation preference, waits for a matching real window and returns a window ID to inspect; it never implies a chat was read. Minimized windows remain discoverable. Approved inspection restores/activates a target before capture, with a bounded foreground-input attachment fallback and explicit failure if Windows still refuses.

Computer use includes the declared public-browser tools so a URL in that prompt does not require guessing Windows key combinations or an extra chip. Public cross-host redirects are checked by the asynchronous resource/DNS gate instead of being rejected merely because the hostname was not cached. A new final origin requires its own task grant before results reach the model. Existing private-network restrictions remain.

AgentStep retains actual result targets, counts and plain error reasons. AiService.ReportTool supplies factual live action labels. External-only runs show Activity rather than zero file totals/Undo; failed external actions remain expanded and readable. File review/diff behavior is preserved. ScreenControlGlow draws full-display inward waves and a spectral pointer aligned to the real native pointer while agent input is active. The normal OS cursor is not replaced globally; the agent indicator fades after its action. Reduced motion freezes waves and explicitly redraws input state without an idle animation timer.


Generation settings reuse the canonical panel, expander, combo/input and accent button resources. Chat has a compact connected summary; generation is split into four progressive disclosures. A named rounded PasswordBox template keeps masked credentials visually consistent with other fields. Pollinations quick-connect is a single introductory card, followed by independent service/model/key controls. Connection checks, catalog metadata and credit-consuming generation tests have separate labels and outcomes. Async key/model changes revoke pending UI results. Media cards share actual format/category labels, with audio playback and 3D file affordances rather than invented raster previews.


## Assistant generation motion and tool timeline — 2026-10-21

The processing row has one 32 DIP visual orb and a 12 DIP real status label. During an unfinished media job the orb changes from the adaptive dotted states into an original native shaded sphere with rotating chromatic lobes, a quiet spectral rim and a slow breathing highlight. AssistantSpectrum.Colors is the canonical processing palette: amber #F7B548, rose #F86394, violet #9A6FF5, blue #459EF8, mint #3FD8B5. VoiceGlow, the generation orb and the text wash consume that same array. This intentional active-processing exception does not add gradients to general management surfaces.

The status label retains its dynamic WidgetInk TextBlock as the accessible, readable base. An input-transparent native glyph overlay passes one spectral wash per second; a 200 ms 2 DIP eased rise occurs only when the actual status changes. It never cycles invented task phrases. ActiveVisual subscribes at most 30 fps while active, loaded, visible and motion-enabled. Reduced motion leaves a still orb and solid text; high contrast removes chromatic generation/sweep. No web renderer or third-party animation runtime is introduced.

ToolActivity extends the existing AgentActionExpander journal. Small rounded vector badges reuse PluginCatalog's real module identity; the disclosure header stacks distinct actually used modules with action count and measured response duration. Rows use 12 DIP action labels and subdued 10.5 DIP module/start-offset/duration metadata. Expanding exposes local start/end times and result/error details; file diffs and full absolute-path tooltips remain. Live expansion is retained across updates. Completed history folds once into the existing final card; subsequent manual expansion is preserved. Entry animation is 180 ms and respects the system animation policy. Native time measurements and cancellation replace guesses about model reasoning.

Visual references supplied by the user: https://21st.dev/@adrielzimbril/components/generating-orb ; https://21st.dev/@heygaia/components/tool-calls-section ; https://21st.dev/@animbits/components/text-dia ; https://21st.dev/@soralabs/components/dia-text-reveal . The WPF implementation is original and adapts the visual concepts, not copied React source.


## Composer connections, stable editing and compact glass help

The 28 DIP provider button stays immediately beside the plus and aligns to the bottom action row for every editor height and conversation state. It opens ChatConnectionMenu, an original native service list with current/saved/missing states and the provider's remembered model. ChatConnections stores one encrypted account/model per supported chat provider, independently of the four generation connections. Existing active credentials migrate; adding another provider preserves previous providers. A switch starts a new provider conversation while retaining unsent text, files and instruction chips. A missing key opens the exact provider's Chat connection settings, expanded and focused. Persistence failure restores every prior slot.

Composer measurement uses a separate FormattedText layout and reserves a stable scrollbar gutter. The actual TextBox is never switched to Auto height or remeasured at infinite height during editing. Height changes only when the measured one-to-four-line target changes; the capped editor keeps its native caret and scroll position. New chat/reset settles back at one line.

GlassPopupMotion owns shared native blur, geometric anchor-origin scale/translation and opacity, outside-dismiss hit testing, keyboard dismissal and teardown. Tools/provider menus open over 260 ms and reverse over 190 ms, shrinking toward the real button dimensions. Compositor bounds follow the transformed card during these brief transitions, without an idle render loop. Tooltips use a quieter 140 ms variant. Reduced motion and high contrast skip motion and retain an opaque fallback where live blur is unavailable. Escape is consumed within the assistant/menu and no longer navigates to the management shell.

GlassTips is the shared short-help surface for string tooltips and existing path tips. Default help appears after 500 ms, wraps within 240 DIP and is capped at 100 characters; full path tips retain a wider 390 DIP bound and the existing absolute path. Surfaces use 11 DIP text, 11×8 padding, 12 DIP corners, shared Panel/Line/Text fallback tokens and a live blurred backdrop when supported. Pointer leave, input, deactivation, timeout and unload dismiss with cleanup. Plugin chips expose their short product descriptions as help; full tool instructions remain exclusively in the submitted model context.

## Live voice implementation

VoiceCallOptions stores per-provider live model/voice preferences independently from chat/media models and keys. LiveVoiceSession owns a single microphone lease, bounded serialized WebSocket output, acknowledged setup, PCM16 mono capture (Gemini 16 kHz, OpenAI 24 kHz) and 24 kHz output. Gemini JSON can arrive as binary WebSocket frames. Mute closes capture and invalidates queued input chunks; interruption resets output and OpenAI receives the measured device playback position for item truncation. End/cancellation/error/hide/unload release all audio and transport resources. Voice call context/transcripts remain within the active chat connection. No audio is written to disk.

VoiceCallView replaces the composer while connected, with a native audio-reactive spectrum orb, real connection/speech state, duration and glass voice selector. Changing voice deliberately ends the session; settings apply at the next Start call. Reduced motion stops the render subscription. The WinMM path uses headphones for reliable full-duplex conversations; it has no acoustic echo cancellation. Optional native live function calls delegate actual browser/computer/file/plugin work to the chat agent and allowlisted player/calendar/focus/status work to the common widget dispatcher.

VoiceActionBridge delegates execute_task to the existing selected chat connection and its AgentWorkspace/runtime, preserving real browser DOM/screen evidence, the saved control confirmation preference, control overlay and chat history/Undo. Browser/computer capabilities are transient per request; cleanup cannot overwrite a newer request or draft. LiveVoiceSession owns a separate bounded action queue, duplicate/cancellation tracking, elapsed action log and provider-native tool responses. Audio receive/send continues while tools await execution. Voice transcripts queued during chat work are drained once on completion/cancellation.

WidgetCommandHub is the allowlisted in-process boundary for media transport, local calendar tasks, focus and status overlays. WidgetIpcServer exposes this boundary through a current-user-only named pipe, with a bounded one-line JSON request and timeout, marshalled to the UI dispatcher. It does not expose the agent, shell, arbitrary methods or credentials. Widgets share a process and use direct dispatch; separate clients use the pipe. See docs/widget-ipc.md for protocol and ownership. Ukrainian/English/Auto preferences shape session instructions; OpenAI also receives transcription language and spelling context. Gemini native audio omits unsupported languageCode.


## Direct control and game input

AiAskBeforeControl is the single saved browser/desktop confirmation policy, default off per user request. AiService snapshots it when each text or delegated voice task starts; AssistantToolRuntime applies it to both launch and target-control requests. Opt-in confirmations remain target-scoped, topmost and taskbar-visible. Waiting approval overrides stale tool progress. Scope checks, fresh snapshots, protected/password targets, foreground checks, the visible control banner and Esc/Stop remain active in either mode.

DesktopAgentTools owns left/right/middle clicks and scan-code keyboard input (letters, digits, F1–F12, navigation, Ctrl/Shift). Physical holds are bounded to 2000 ms, monitor focus in 25 ms slices and always send reverse-order key-up on cancellation/failure. System shortcuts are excluded. Accessibility failures in custom-rendered apps retain screenshot inspection; failed/black PrintWindow capture can use the visible foreground region after occlusion checks. Windowed/borderless capture is preferable. These are discrete observed actions, not a low-latency game-playing engine: no Dota 2 match, reflex performance or game/anti-cheat compatibility is claimed.

## Current management UI — 0.6.1

The React management shell supersedes the old native management navigation/palette above. Canonical base tokens are design/tokens.json, generated into ui/src/tokens.css and native ThemeTokens.cs: 72 DIP black rail, #080808 frame, #101010 background, #181818 surfaces, #F5F5F5 text and restrained liquid selection. Segoe UI Variable remains the local system font. Accent presets are owned by design/accents.json, consumed by both UI and native InterfaceAccent; they affect selection/indicators and subtle outlines, not widget materials. Neutral is default. User-requested Auto samples a small local wallpaper thumbnail solely for its color, with Windows-color fallback; the live desktop widget backdrop never uses this image. No path or pixels reach React or the network.

Settings switches respond optimistically, keep their geometry and keyboard focus, serialize each preference and roll back errors. Nearby controls remain available. Previews animate only while hovered/focused and visible, never at idle, and honor Reduced Motion.

## Management appearance 0.6.2

The chooser owns three adjacent previews: Dark, Light and an empty Custom tile. ThemePicker uses the shared Radix Modal, a saturation/brightness plane, hue slider and HEX field. Add second color creates a diagonal two-stop gradient with Remove and explicit Apply/Cancel. No OS color dialog or external color service is used. Black remains the default.

Palette ownership is design/tokens.json for Dark and design/light-theme.json for Light. appearance.ts adapts these semantic roles to runtime CSS and derives restrained custom tints for readable text. Widget previews and desktop widget materials keep their own colors. Chrome, cards, forms, menus and dialogs share management roles; accents remain independent.

The center paint layer sits outside the existing content scroller. Its switch fades/scales only this layer; chrome and card content remain visible and interactive. The native layered host and transparent WebView reveal actual desktop/window content without wallpaper capture. A minimum-alpha native input surface keeps interactions inside management. Theme changes use a 520 ms circular View Transition from the selected tile. Unsupported/hidden/reduced-motion contexts commit directly; equivalent native acknowledgements cannot interrupt a reveal. The native theme sound follows existing sound, volume, voice and fullscreen policy.

## Management window and responsive motion 0.6.4

ShellWindowLayout owns physical monitor bounds and the normal/maximized placement in local ui-state.json. Maximize fits the current monitor work area; normal bounds clamp after DPI/display changes and restart. The native minimum is 640 × 420 DIP capped by the available work area. ShellWindowLayout retains the OS restore rectangle and last non-minimized state. Maximize/restore commits the live browser geometry once, then uses a cancellable 250 ms cached-bitmap render transform and 100 ms crossfade; latest intent wins. Closing/minimizing settles that intent. Manual resizing remains directly controlled by the pointer.

The caption consumes a fixed 28 DIP; the remaining application frame has a bounded height. The rail uses dedicated brand, scrollable navigation and pinned bottom-action rows. Below 600 CSS px the rail uses 38 px buttons and compact spacing; Settings never depends on the content scroller. Existing width breakpoints at 760/1050/1450 px remain canonical.

The entire caption, including the application name, is a native WebView2 drag region. Caption controls explicitly opt out. WebView2 non-client support is enabled before navigation, so Windows owns dragging and snapping without relying on WPF mouse state across the browser HWND. Native caption double-click routes to the existing animated maximize/restore controller.

workspaceMotion.ts has a stable lifetime across navigation and owns the 280 ms opacity/translation/scale whole-window content reveal on a populated cold start or tray resume and 180 ms positional grid rearrangement at width breakpoints. Text is not stretched during manual resizing. Resize interrupts theme snapshots/opening transforms. Animation owners release frame callbacks/listeners on cancellation or disposal, and reduced-motion/hidden contexts do not animate.

The native maximize transition is the only geometry owner while it runs and for two stable viewport frames afterward; responsive card FLIP does not compete with it. DWM transitions are disabled locally for that custom geometry transition and restored afterward. The whole interface is prepared while hidden, waits for visible render frames, then reveals once. The initial ResizeObserver notification is not treated as a resize. The WPF host disables the system border/rounding decoration. A transparent 4 DIP input strip preserves native edge/corner resizing without painting a glass outline.

ShellSnapshotTransition captures only the WebView into temporary RAM (JPEG for opaque themes, PNG when transparency is needed), with a 2048 px texture cap. Its passive temporary overlay stays at one fixed physical union rectangle; WPF render clocks move/scale the bitmap, avoiding per-frame HWND resizing and HTML layout. The browser prepares its final layout once behind a temporary DWM cloak. A tokened two-frame acknowledgement begins during movement; after uncloaking, the handoff waits at most 80 ms before crossfading because Chromium may throttle frames while cloaked. The cloak is always removed on exit; unsupported DWM contexts use a bounded hidden-view fallback. Capture deadlines fall back to a direct geometry commit. Rendering failures cannot poison later transitions; core widgets/downloads never wait for UI rendering. Capture streams, bitmap references, animation clocks and temporary windows are released on success/cancellation. No transition image is written to disk.

## Desktop widgets and Focus 0.6.6

Native widget creation is transactional: a failed content tree cannot add an enabled instance or increase the runtime desktop count. WidgetManager.IsOnDesktop and the visible snapshot field own desktop presence; enabled is saved intent. Library separates visible windows from Hidden, counts actual instances, and offers Locate for overlapping windows. Global pause has a Resume action.

Focus and Controls inherit their pack surface and adaptive ink; Notifications retains its dark reading inset. Focus has one animated Start/End action and a popover with presets, 1–1440 minute input and call-app preferences. Controls uses honest settings shortcuts, mute, volume and hardware-dependent brightness. Notifications displays real entries when package identity/permission are available, or an explicit capability state in portable builds.

Focus combines Windows QuietHours DND with selected communication-app audio sessions. A positive allowlist excludes music players, browsers, system audio and unknown processes. CoreAudio session-created notifications run on an MTA worker with active-only one-second reconciliation. System output mute and volume never change. All audio within selected call apps is quieted; browser calls sharing music audio require site/app preferences. The QuietHours shell compatibility adapter is checked on the current Windows build; failures are surfaced. Recovery is journaled before mutation; exact audio session instances retain their control through normal End even if an app exits, and only unfinished restoration is retried after restart. End, expiry, Disable all and normal exit restore prior state.

## Transparent instruments 0.6.7

WidgetWindow/GlassSurface/LiveBackdrop remain the single pack material owner. Focus and Controls add no dark inner panel and do not override WidgetInk. WidgetVisuals.GlassFill/GlassEdge own restrained translucent control highlights; Controls uses 50 DIP circular actions and a 32 DIP tall slider gesture target. WidgetSlider retains native Slider keyboard and automation semantics while capturing a drag from any point on the rail. Volume changes coalesce at 40 ms; brightness at 150 ms, without feeding stale readings back into an active gesture.

Photos occupy the full rounded surface with zero content inset or image shadow. Choose photo fades over 160 ms on hover or keyboard focus; empty/error states keep the action available. Four 26 DIP corner targets resize independently with an anchored opposite edge. Custom DIP dimensions persist per instance, with minimum/maximum bounds; S/M/L presets clear that override. Reduced motion applies immediately. Image data remains local.
