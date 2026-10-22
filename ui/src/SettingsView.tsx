import { useCallback, useEffect, useRef, useState } from "react";
import {
  ArrowUpRight,
  Check,
  CheckCircle2,
  Eye,
  EyeOff,
  Globe,
  KeyRound,
  Monitor,
  Moon,
  Play,
  ShieldCheck,
  Volume2,
} from "lucide-react";
import type {
  GenerationConnection,
  Run,
  Settings,
  Snapshot,
  Voice,
} from "./types";
import { Button, Dropdown, Modal, Section, ToggleRow } from "./primitives";
import { rpc } from "./bridge";

const settingsTabs = [
  ["general", "General"],
  ["appearance", "Appearance"],
  ["widgets", "Widgets"],
  ["connections", "Connections"],
  ["permissions", "Permissions"],
  ["about", "About"],
];
export function SettingsView({
  state,
  section,
  setSection,
  run,
  onDirty,
  requestedProvider,
}: {
  state: Snapshot;
  section: string;
  setSection: (section: string) => void;
  run: Run;
  onDirty: (dirty: boolean) => void;
  requestedProvider?: string;
}) {
  const dirtyForms = useRef(new Set<string>());
  const markDirty = useCallback(
    (name: string, value: boolean) => {
      if (value) dirtyForms.current.add(name);
      else dirtyForms.current.delete(name);
      onDirty(dirtyForms.current.size > 0);
    },
    [onDirty],
  );
  const chatDirty = useCallback(
    (v: boolean) => markDirty("chat", v),
    [markDirty],
  );
  const weatherDirty = useCallback(
    (v: boolean) => markDirty("weather", v),
    [markDirty],
  );
  const voiceDirty = useCallback(
    (v: boolean) => markDirty("voice", v),
    [markDirty],
  );
  useEffect(
    () => () => {
      dirtyForms.current.clear();
      onDirty(false);
    },
    [section, onDirty],
  );
  const s = state.settings;
  const [pending, setPending] = useState("");
  const [confirm, setConfirm] = useState("");
  const [restore, setRestore] = useState(false);
  const save = async (key: string, value: unknown) => {
    setPending(key);
    try {
      await run("settings.general", { [key]: value });
    } catch {
    } finally {
      setPending("");
    }
  };
  return (
    <>
      <header className="page-header">
        <div className="heading-group">
          <h1>Settings</h1>
        </div>
      </header>
      <div className="settings-layout">
        <nav className="settings-nav" aria-label="Settings sections">
          {settingsTabs.map(([id, label]) => (
            <button
              key={id}
              className={section === id ? "selected" : ""}
              aria-current={section === id ? "page" : undefined}
              onClick={() => setSection(id)}
            >
              {label}
            </button>
          ))}
        </nav>
        <div className="settings-content" key={section}>
          {section === "general" && (
            <>
              <Section title="Your everyday setup">
                <ToggleRow
                  label="Launch at Windows sign-in"
                  checked={s.startup}
                  onChange={(v) => void save("startup", v)}
                  disabled={Boolean(pending)}
                />
                <div className="setting-row">
                  <span>
                    <span className="setting-label">Close to tray</span>
                    <span className="setting-description">
                      Your widgets keep running when you close this window.
                    </span>
                  </span>
                  <Check size={17} />
                </div>
              </Section>
              <Section
                title="Interface sounds"
                description="Small, quiet sounds for navigation and completed actions."
              >
                <ToggleRow
                  label="Play interface sounds"
                  checked={s.interfaceSounds}
                  onChange={(v) => void save("interfaceSounds", v)}
                  disabled={Boolean(pending)}
                />
                <label className="setting-row volume-row">
                  <span className="setting-label">Volume</span>
                  <input
                    aria-label="Interface sound volume"
                    type="range"
                    min="0"
                    max="100"
                    defaultValue={Math.round(s.interfaceVolume * 100)}
                    onPointerUp={(e) =>
                      void save(
                        "interfaceVolume",
                        Number(e.currentTarget.value) / 100,
                      )
                    }
                    onKeyUp={(e) => {
                      if (
                        [
                          "ArrowLeft",
                          "ArrowRight",
                          "ArrowUp",
                          "ArrowDown",
                          "Home",
                          "End",
                          "PageUp",
                          "PageDown",
                        ].includes(e.key)
                      )
                        void save(
                          "interfaceVolume",
                          Number(e.currentTarget.value) / 100,
                        );
                    }}
                    disabled={Boolean(pending)}
                  />
                  <Button
                    onClick={() =>
                      void run("app.sound", {
                        sound: "navigate",
                        preview: true,
                      }).catch(() => {})
                    }
                  >
                    <Volume2 size={14} /> Preview
                  </Button>
                </label>
              </Section>
            </>
          )}
          {section === "appearance" && (
            <>
              <Section title="Theme">
                <div className="theme-options">
                  <div className="theme-tile selected">
                    <div className="theme-mini">
                      <i />
                      <span>
                        <b />
                        <b />
                        <b />
                      </span>
                    </div>
                    <span>
                      Dark <CheckCircle2 size={16} />
                    </span>
                  </div>
                </div>
              </Section>
              <Section title="Motion">
                <div className="setting-row">
                  <span>
                    <span className="setting-label">
                      Follow Windows preferences
                    </span>
                    <span className="setting-description">
                      Reduced motion and high contrast apply automatically.
                    </span>
                  </span>
                  <Monitor size={18} />
                </div>
              </Section>
            </>
          )}
          {section === "widgets" && (
            <>
              <Section title="Time & placement">
                <ToggleRow
                  label="Analog clock"
                  checked={s.analog}
                  onChange={(v) => void save("analog", v)}
                  disabled={Boolean(pending)}
                />
                <ToggleRow
                  label="24-hour time"
                  checked={s.hour24}
                  onChange={(v) => void save("hour24", v)}
                  disabled={Boolean(pending)}
                />
                <ToggleRow
                  label="Show seconds"
                  description="Updates the clock every second while it is visible."
                  checked={s.seconds}
                  onChange={(v) => void save("seconds", v)}
                  disabled={Boolean(pending)}
                />
                <ToggleRow
                  label="Always on top"
                  checked={s.topmost}
                  onChange={(v) => void save("topmost", v)}
                  disabled={Boolean(pending)}
                />
                <div className="setting-row">
                  <span>
                    <span className="setting-label">Widget positions</span>
                  </span>
                  <Button
                    onClick={() =>
                      void run(
                        "settings.placement.reset",
                        {},
                        "Widgets centered",
                      ).catch(() => {})
                    }
                  >
                    Center widgets
                  </Button>
                </div>
              </Section>
              <WeatherSettings s={s} run={run} onDirty={weatherDirty} />
              <Section title="Music">
                <div className="setting-row">
                  <span className="setting-label">Source</span>
                  <Dropdown
                    label="Music source"
                    value={s.mediaSource ?? "Automatic"}
                    onChange={(source) =>
                      void run(
                        "settings.media.source",
                        { source },
                        "Music source selected",
                      ).catch(() => {})
                    }
                    options={["Automatic", ...s.mediaSources].map((value) => ({
                      value,
                      label: value,
                    }))}
                  />
                </div>
                <ToggleRow
                  label="Song lyrics"
                  description="Track details are sent to LRCLIB when enabled."
                  checked={s.lyricsEnabled}
                  onChange={(v) => void save("lyricsEnabled", v)}
                  disabled={Boolean(pending)}
                />
                <div className="setting-row">
                  <span>
                    <span className="setting-label">SoundCloud bridge</span>
                    <span className="setting-description">
                      {s.bridgeStatus ||
                        "Optional browser extension for Shuffle and Repeat."}
                    </span>
                  </span>
                </div>
              </Section>
            </>
          )}
          {section === "connections" && (
            <>
              <ChatSettings
                s={s}
                run={run}
                onDirty={chatDirty}
                requestedProvider={requestedProvider}
              />
              <VoiceSettings s={s} run={run} onDirty={voiceDirty} />
              <Section
                title="Generation"
                description="Images, video, 3D and audio can use their own connections."
              >
                {s.generation.map((g) => (
                  <GenerationSettings
                    key={g.id}
                    connection={g}
                    run={run}
                    onDirty={(value) => markDirty(g.id, value)}
                  />
                ))}
              </Section>
              <p className="privacy-copy">
                Messages and chosen attachments go to your selected service when
                you send. Audio streams while a voice call is connected. API
                usage follows your provider's plan. Saved keys are encrypted for
                your Windows account.
              </p>
            </>
          )}
          {section === "permissions" && (
            <>
              <Section title="Windows notifications">
                <ToggleRow
                  label="Enable notification access"
                  checked={s.notificationsEnabled}
                  onChange={(v) => void save("notificationsEnabled", v)}
                  disabled={Boolean(pending)}
                />
                <div className="setting-row">
                  <span>
                    <span className="setting-description">
                      {s.notificationStatus ||
                        "Windows permission and package identity are required. Original notifications are kept."}
                    </span>
                  </span>
                  <Button
                    disabled={!s.notificationsEnabled || state.snoozed}
                    onClick={() =>
                      void run("settings.notifications.request").catch(() => {})
                    }
                  >
                    Request access
                  </Button>
                </div>
              </Section>
              <Section title="Assistant control">
                <ToggleRow
                  label="Ask before browser and app control"
                  description="Applies to new chat tasks and delegated voice actions."
                  checked={s.aiAskBeforeControl}
                  onChange={(v) => void save("aiAskBeforeControl", v)}
                  disabled={Boolean(pending)}
                />
                <div className="setting-row">
                  <span className="setting-description">
                    File and browser access stays scoped to the folders and
                    targets you choose. Esc and Stop remain available during
                    computer control.
                  </span>
                  <ShieldCheck size={18} />
                </div>
              </Section>
            </>
          )}
          {section === "about" && (
            <>
              <div className="about-brand">
                <span className="brand-ring" />
                <div>
                  <h2>YoWidgets</h2>
                  <p>Version {s.version}</p>
                </div>
              </div>
              <Section title="Application">
                <div className="setting-row">
                  <span className="setting-label">Windows</span>
                  <span className="setting-description">{s.platform}</span>
                </div>
                <div className="setting-row">
                  <span className="setting-label">Source code</span>
                  <Button
                    onClick={() =>
                      void rpc("ui.external", { target: "github" }).catch(
                        () => {},
                      )
                    }
                  >
                    GitHub <ArrowUpRight size={14} />
                  </Button>
                </div>
                <div className="setting-row">
                  <span className="setting-label">Privacy</span>
                  <Button
                    onClick={() =>
                      void rpc("ui.external", { target: "privacy" }).catch(
                        () => {},
                      )
                    }
                  >
                    Read policy <ArrowUpRight size={14} />
                  </Button>
                </div>
                <div className="setting-row">
                  <span className="setting-label">Interface cache</span>
                  <Button
                    onClick={() =>
                      void run(
                        "ui.clearCache",
                        {},
                        "Interface cache cleared",
                      ).catch(() => {})
                    }
                  >
                    Clear cache
                  </Button>
                </div>
              </Section>
              <Section title="Desktop & application">
                <div className="setting-row">
                  <span>
                    <span className="setting-label">Disable all widgets</span>
                    <span className="setting-description">
                      Also turns off integrations and launch at sign-in.
                    </span>
                  </span>
                  <Button onClick={() => setConfirm("disable")}>
                    Disable all
                  </Button>
                </div>
                <div className="setting-row">
                  <span>
                    <span className="setting-label">Quit YoWidgets</span>
                    <span className="setting-description">
                      Closes the app and all desktop widgets.
                    </span>
                  </span>
                  <Button onClick={() => setConfirm("quit")}>Quit</Button>
                </div>
                <div className="setting-row">
                  <span>
                    <span className="setting-label danger-text">
                      Uninstall YoWidgets
                    </span>
                    <span className="setting-description">
                      {s.canUninstall
                        ? "Removes this installation, saved keys, downloaded content and settings."
                        : "Available from an installed copy. Development sources are preserved."}
                    </span>
                  </span>
                  <Button
                    variant="danger"
                    disabled={!s.canUninstall}
                    onClick={() => setConfirm("uninstall")}
                  >
                    Uninstall
                  </Button>
                </div>
              </Section>
            </>
          )}
        </div>
      </div>
      <Modal
        open={Boolean(confirm)}
        onClose={() => setConfirm("")}
        title={
          confirm === "uninstall"
            ? "Uninstall YoWidgets?"
            : confirm === "quit"
              ? "Quit YoWidgets?"
              : "Disable all widgets?"
        }
        description={
          confirm === "uninstall"
            ? "This removes app-owned content, settings and saved connections. Your original photos and external files are kept."
            : confirm === "quit"
              ? "Your widgets will close until you start the app again."
              : "Widgets and integrations will stop. Saved settings are kept."
        }
      >
        {confirm === "uninstall" && (
          <ToggleRow
            label="Restore volume and brightness changed by YoWidgets"
            checked={restore}
            onChange={setRestore}
          />
        )}
        <div className="dialog-actions">
          <Button onClick={() => setConfirm("")}>Cancel</Button>
          <Button
            variant={confirm === "uninstall" ? "danger" : "primary"}
            onClick={() => {
              void run(
                confirm === "uninstall"
                  ? "app.uninstall"
                  : confirm === "quit"
                    ? "app.quit"
                    : "app.disableAll",
                { restoreControls: restore },
              ).catch(() => {});
              setConfirm("");
            }}
          >
            {confirm === "uninstall"
              ? "Uninstall YoWidgets"
              : confirm === "quit"
                ? "Quit"
                : "Disable all"}
          </Button>
        </div>
      </Modal>
    </>
  );
}

function SecretField({
  value,
  onChange,
  saved = false,
  label = "API key",
}: {
  value: string;
  onChange: (value: string) => void;
  saved?: boolean;
  label?: string;
}) {
  const [visible, setVisible] = useState(false);
  return (
    <label className="form-field">
      <span>{label}</span>
      <span className="secret-field">
        <input
          type={visible ? "text" : "password"}
          value={value}
          onChange={(e) => onChange(e.target.value)}
          placeholder={
            saved ? "Saved · leave empty to keep" : "Enter your API key"
          }
          autoComplete="new-password"
          spellCheck={false}
        />
        <button
          type="button"
          className="icon-button"
          aria-label={visible ? "Hide API key" : "Show API key"}
          onClick={() => setVisible(!visible)}
        >
          {visible ? <EyeOff size={16} /> : <Eye size={16} />}
        </button>
      </span>
    </label>
  );
}
function WeatherSettings({
  s,
  run,
  onDirty,
}: {
  s: Settings;
  run: Run;
  onDirty: (value: boolean) => void;
}) {
  const [city, setCity] = useState(s.city);
  const [busy, setBusy] = useState(false);
  useEffect(() => () => onDirty(false), [onDirty]);
  return (
    <Section title="Weather">
      <form
        className={busy ? "form-pending" : undefined}
        noValidate
        onSubmit={async (e) => {
          e.preventDefault();
          setBusy(true);
          try {
            await run("settings.general", { city }, "City saved");
            onDirty(false);
            await run("settings.weather.refresh");
          } catch {
          } finally {
            setBusy(false);
          }
        }}
      >
        <fieldset disabled={Boolean(busy)}>
          <div className="form-inline">
            <label className="form-field">
              <span>City</span>
              <input
                disabled={busy}
                value={city}
                onChange={(e) => {
                  setCity(e.target.value);
                  onDirty(true);
                }}
                maxLength={120}
                required
              />
            </label>
            <Button type="submit" variant="primary" busy={busy}>
              Save & refresh
            </Button>
          </div>
        </fieldset>
      </form>
      <ToggleRow
        label="Temperature in Fahrenheit"
        checked={s.fahrenheit}
        onChange={(v) =>
          void run("settings.general", { fahrenheit: v }).catch(() => {})
        }
      />
    </Section>
  );
}

function ChatSettings({
  s,
  run,
  onDirty,
  requestedProvider,
}: {
  s: Settings;
  run: Run;
  onDirty: (value: boolean) => void;
  requestedProvider?: string;
}) {
  const [provider, setProvider] = useState(
    requestedProvider && s.chat.some((c) => c.provider === requestedProvider)
      ? requestedProvider
      : s.chatProvider,
  );
  const [model, setModel] = useState(
    s.chat.find((c) => c.provider === provider)?.model ?? s.chatModel,
  );
  const [key, setKey] = useState("");
  const [processText, setProcessText] = useState(s.aiProcessText);
  const [models, setModels] = useState<string[]>([]);
  const [busy, setBusy] = useState("");
  const [feedback, setFeedback] = useState("");
  const [removing, setRemoving] = useState(false);
  const epoch = useRef(0);
  const saved = s.chat.find((c) => c.provider === provider)?.connected ?? false;
  useEffect(
    () => () => {
      epoch.current++;
      onDirty(false);
    },
    [onDirty],
  );
  useEffect(() => {
    if (
      requestedProvider &&
      s.chat.some((c) => c.provider === requestedProvider)
    ) {
      epoch.current++;
      setProvider(requestedProvider);
      setModel(
        s.chat.find((c) => c.provider === requestedProvider)?.model ?? "",
      );
      setKey("");
      setFeedback("");
    }
  }, [requestedProvider]);
  const change = () => {
    epoch.current++;
    onDirty(true);
    setFeedback("");
  };
  const check = async () => {
    const id = ++epoch.current;
    setBusy("check");
    try {
      const result = await run<{ message: string; models: string[] }>(
        "settings.chat.check",
        { provider, key },
      );
      if (id === epoch.current) {
        setModels(result.models);
        setFeedback(result.message);
      }
    } catch (e) {
      if (id === epoch.current) setFeedback((e as Error).message);
    } finally {
      if (id === epoch.current) setBusy("");
    }
  };
  const save = async () => {
    const id = ++epoch.current;
    setBusy("save");
    try {
      await run(
        "settings.chat.save",
        { provider, model, key, processText },
        "Connection saved",
      );
      if (id !== epoch.current) return;
      setKey("");
      onDirty(false);
      setFeedback("Connection saved");
    } catch (e) {
      if (id === epoch.current) setFeedback((e as Error).message);
    } finally {
      if (id === epoch.current) setBusy("");
    }
  };
  return (
    <Section
      title="Chat"
      description={
        s.chat.find((c) => c.provider === s.chatProvider)?.connected
          ? `${s.chatProvider} · ${s.chatModel}`
          : "Connect a service to use the assistant."
      }
    >
      <form
        className={busy ? "form-pending" : undefined}
        noValidate
        onSubmit={(e) => {
          e.preventDefault();
          void save();
        }}
      >
        <fieldset disabled={Boolean(busy)}>
          <div className="form-grid">
            <label className="form-field">
              <span>Service</span>
              <Dropdown
                label="Chat service"
                value={provider}
                onChange={(value) => {
                  change();
                  setProvider(value);
                  setModel(
                    s.chat.find((c) => c.provider === value)?.model ?? "",
                  );
                  setModels([]);
                  setKey("");
                  setBusy("");
                }}
                options={s.chat.map((c) => ({
                  value: c.provider,
                  label: c.provider + (c.connected ? " · connected" : ""),
                }))}
              />
            </label>
            <label className="form-field">
              <span>Model</span>
              <input
                value={model}
                list="chat-models"
                onChange={(e) => {
                  change();
                  setModel(e.target.value);
                }}
                maxLength={160}
              />
              <datalist id="chat-models">
                {models.map((m) => (
                  <option key={m} value={m} />
                ))}
              </datalist>
            </label>
          </div>
          <SecretField
            value={key}
            saved={saved}
            onChange={(value) => {
              change();
              setKey(value);
              setBusy("");
            }}
          />
          <details className="advanced-settings">
            <summary>Advanced</summary>
            <label className="form-field">
              <span>Processing label</span>
              <input
                value={processText}
                maxLength={80}
                onChange={(e) => {
                  change();
                  setProcessText(e.target.value);
                }}
              />
            </label>
          </details>
          <div className="form-actions">
            <Button
              type="submit"
              variant="primary"
              busy={busy === "save"}
              disabled={Boolean(busy)}
            >
              Save connection
            </Button>
            <Button
              type="button"
              onClick={() => void check()}
              busy={busy === "check"}
              disabled={Boolean(busy)}
            >
              Check connection
            </Button>
            {saved && (
              <button
                type="button"
                className="text-button danger-text"
                onClick={() => setRemoving(true)}
              >
                Remove key
              </button>
            )}
          </div>
          <p className="form-feedback" role="status">
            {feedback}
          </p>
        </fieldset>
      </form>
      <Modal
        open={removing}
        onClose={() => setRemoving(false)}
        title={`Remove the ${provider} key?`}
        description="You will need to enter it again to use this connection."
      >
        <div className="dialog-actions">
          <Button onClick={() => setRemoving(false)}>Cancel</Button>
          <Button
            variant="danger"
            onClick={() => {
              void run(
                "settings.chat.remove",
                { provider },
                "Saved key removed",
              )
                .then(() => {
                  setKey("");
                  setRemoving(false);
                  onDirty(false);
                })
                .catch(() => {});
            }}
          >
            Remove key
          </Button>
        </div>
      </Modal>
    </Section>
  );
}

function VoiceSettings({
  s,
  run,
  onDirty,
}: {
  s: Settings;
  run: Run;
  onDirty: (value: boolean) => void;
}) {
  const [voice, setVoice] = useState<Voice>(s.voice);
  const [busy, setBusy] = useState(false);
  const [feedback, setFeedback] = useState("");
  const change = (name: keyof Voice, value: string | boolean) => {
    setVoice({ ...voice, [name]: value });
    onDirty(true);
  };
  return (
    <Section
      title="Live voice"
      description="Uses your saved Gemini or OpenAI chat connection."
    >
      <details className="connection-disclosure">
        <summary>Voice preferences</summary>
        <form
          className={busy ? "form-pending" : undefined}
          noValidate
          onSubmit={async (e) => {
            e.preventDefault();
            setBusy(true);
            try {
              await run("settings.voice", { voice }, "Voice preferences saved");
              onDirty(false);
              setFeedback("Changes apply to your next call.");
            } catch (e) {
              setFeedback((e as Error).message);
            } finally {
              setBusy(false);
            }
          }}
        >
          <fieldset disabled={Boolean(busy)}>
            <div className="form-grid">
              <label className="form-field">
                <span>Gemini live model</span>
                <input
                  value={voice.geminiModel}
                  onChange={(e) => change("geminiModel", e.target.value)}
                />
              </label>
              <label className="form-field">
                <span>Gemini voice</span>
                <Dropdown
                  label="Gemini voice"
                  value={voice.geminiVoice}
                  onChange={(v) => change("geminiVoice", v)}
                  options={s.voiceChoices.gemini.map((value) => ({
                    value,
                    label: value,
                  }))}
                />
              </label>
              <label className="form-field">
                <span>OpenAI live model</span>
                <input
                  value={voice.openAIModel}
                  onChange={(e) => change("openAIModel", e.target.value)}
                />
              </label>
              <label className="form-field">
                <span>OpenAI voice</span>
                <Dropdown
                  label="OpenAI voice"
                  value={voice.openAIVoice}
                  onChange={(v) => change("openAIVoice", v)}
                  options={s.voiceChoices.openai.map((value) => ({
                    value,
                    label: value,
                  }))}
                />
              </label>
            </div>
            <label className="form-field">
              <span>Conversation language</span>
              <Dropdown
                label="Conversation language"
                value={voice.language}
                onChange={(v) => change("language", v)}
                options={[
                  { value: "auto", label: "Auto" },
                  { value: "uk", label: "Ukrainian" },
                  { value: "en", label: "English" },
                ]}
              />
            </label>
            <ToggleRow
              label="Allow actions in calls"
              description="Lets voice requests use the configured chat agent and widget controls."
              checked={voice.allowActions}
              onChange={(v) => change("allowActions", v)}
            />
            <Button type="submit" variant="primary" busy={busy}>
              Save voice preferences
            </Button>
            <p className="form-feedback" role="status">
              {feedback}
            </p>
          </fieldset>
        </form>
      </details>
    </Section>
  );
}

function GenerationSettings({
  connection: g,
  run,
  onDirty,
}: {
  connection: GenerationConnection;
  run: Run;
  onDirty: (value: boolean) => void;
}) {
  const [draft, setDraft] = useState({ ...g, key: "" });
  const [busy, setBusy] = useState("");
  const [feedback, setFeedback] = useState("");
  const [models, setModels] = useState<string[]>([]);
  const epoch = useRef(0);
  const change = (name: string, value: string) => {
    epoch.current++;
    setDraft({ ...draft, [name]: value });
    setFeedback("");
    setBusy("");
    onDirty(true);
  };
  const action = async (check: boolean) => {
    const id = ++epoch.current;
    setBusy(check ? "check" : "save");
    try {
      const result = await run<{ message: string; models?: string[] }>(
        check ? "settings.generation.check" : "settings.generation.save",
        {
          kind: g.id,
          mode: draft.mode,
          provider: draft.provider,
          model: draft.model,
          voice: draft.voice,
          key: draft.key,
        },
      );
      if (id !== epoch.current) return;
      setFeedback(result.message);
      if (result.models) setModels(result.models);
      if (!check) {
        setDraft({ ...draft, key: "" });
        onDirty(false);
      }
    } catch (e) {
      if (id === epoch.current) setFeedback((e as Error).message);
    } finally {
      if (id === epoch.current) setBusy("");
    }
  };
  useEffect(
    () => () => {
      epoch.current++;
    },
    [],
  );
  return (
    <details className="generation-disclosure">
      <summary>
        <span>{g.title}</span>
        <span>
          {g.mode === "off"
            ? "Off"
            : g.mode === "chat"
              ? "Chat connection"
              : g.provider}
        </span>
      </summary>
      <form
        className={busy ? "form-pending" : undefined}
        noValidate
        onSubmit={(e) => {
          e.preventDefault();
          void action(false);
        }}
      >
        <fieldset disabled={Boolean(busy)}>
          <div className="form-grid">
            <label className="form-field">
              <span>Connection</span>
              <Dropdown
                label={`${g.title} connection`}
                value={draft.mode}
                onChange={(v) => change("mode", v)}
                options={[
                  { value: "off", label: "Off" },
                  { value: "chat", label: "Use chat connection" },
                  { value: "separate", label: "Separate connection" },
                ]}
              />
            </label>
            <label className="form-field">
              <span>Service</span>
              <Dropdown
                label={`${g.title} service`}
                value={draft.provider}
                onChange={(v) => {
                  change("provider", v);
                  setDraft({ ...draft, provider: v, key: "", model: "" });
                  setModels([]);
                }}
                options={g.providers.map((value) => ({ value, label: value }))}
              />
            </label>
          </div>
          <label className="form-field">
            <span>Model ID</span>
            <input
              list={`${g.id}-models`}
              value={draft.model}
              placeholder="Service default"
              onChange={(e) => change("model", e.target.value)}
            />
            <datalist id={`${g.id}-models`}>
              {models.map((m) => (
                <option key={m} value={m} />
              ))}
            </datalist>
          </label>
          {draft.mode === "separate" && (
            <SecretField
              value={draft.key}
              saved={g.keySaved && draft.provider === g.provider}
              onChange={(v) => change("key", v)}
            />
          )}
          {g.id === "audio" && (
            <label className="form-field">
              <span>Voice</span>
              <input
                value={draft.voice}
                onChange={(e) => change("voice", e.target.value)}
                placeholder="alloy"
              />
            </label>
          )}
          <div className="form-actions">
            <Button
              variant="primary"
              type="submit"
              busy={busy === "save"}
              disabled={Boolean(busy)}
            >
              Save preferences
            </Button>
            <Button
              type="button"
              onClick={() => void action(true)}
              disabled={Boolean(busy) || draft.mode === "off"}
              busy={busy === "check"}
            >
              Check connection
            </Button>
          </div>
          <p className="form-feedback" role="status">
            {feedback}
          </p>
        </fieldset>
      </form>
    </details>
  );
}
