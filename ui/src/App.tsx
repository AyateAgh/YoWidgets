import {
  useCallback,
  useEffect,
  useLayoutEffect,
  useRef,
  useState,
} from "react";
import {
  ArrowDownToLine,
  ArrowUpRight,
  CheckCircle2,
  Download,
  House,
  Library,
  LoaderCircle,
  Maximize2,
  Minus,
  Moon,
  Settings as SettingsIcon,
  Sun,
  X,
} from "lucide-react";
import type {
  CatalogItem,
  Filters,
  LibraryPreferences,
  Run,
  Snapshot,
} from "./types";
import { defaults } from "./types";
import { native, onHost, preview, rpc } from "./bridge";
import { previewState } from "./preview";
import { restoreView } from "./viewState";
import { Button, Hint, Modal, Tooltip } from "./primitives";
import { CatalogView, ItemDetails } from "./CatalogView";
import { LibraryView } from "./LibraryView";
import { DownloadsView } from "./DownloadsView";
import { SettingsView } from "./SettingsView";

export function App() {
  const [state, setState] = useState<Snapshot | null>(
    preview ? previewState : null,
  );
  const [page, setPage] = useState("home");
  const [filters, setFilters] = useState<Filters>(defaults);
  const [selected, setSelected] = useState<CatalogItem | null>(null);
  const [error, setError] = useState("");
  const [toast, setToast] = useState("");
  const [dirty, setDirty] = useState(false);
  const [pendingPage, setPendingPage] = useState("");
  const [provider, setProvider] = useState<string>();
  const [library, setLibrary] = useState<LibraryPreferences>({
    tab: "installed",
    query: "",
    pack: "all",
    sort: "recent",
  });
  const [settingsEpoch, setSettingsEpoch] = useState(0);
  const positions = useRef<Record<string, number>>({});
  const dirtyRef = useRef(false);
  dirtyRef.current = dirty;
  const main = useRef<HTMLElement>(null);
  const rail = useRef<HTMLElement>(null);
  const pill = useRef<HTMLSpanElement>(null);
  const initialized = useRef(false);
  const handledRequest = useRef("");
  const accept = useCallback((incoming: Snapshot) => {
    setState((previous) =>
      previous && previous.revision > incoming.revision ? previous : incoming,
    );
    if (!incoming.requestedPage) handledRequest.current = "";
    if (incoming.requestedPage) {
      const request = incoming.requestedPage + "|" + incoming.requestedProvider;
      if (handledRequest.current !== request) {
        handledRequest.current = request;
        const target =
          incoming.requestedPage === "appearance"
            ? "settings/appearance"
            : incoming.requestedPage === "settings"
              ? "settings/general"
              : incoming.requestedPage;
        if (dirtyRef.current) setPendingPage(target);
        else {
          setPage(target);
          setProvider(incoming.requestedProvider);
        }
        void rpc("app.clearRequest").catch(() => {});
      }
    }
  }, []);
  const refresh = useCallback(async () => {
    if (native) accept(await rpc<Snapshot>("state"));
  }, [accept]);
  const run: Run = useCallback(
    async <T,>(
      method: string,
      args: Record<string, unknown> = {},
      success?: string,
    ) => {
      try {
        const result = await rpc<T>(method, args);
        await refresh();
        if (success) setToast(success);
        return result;
      } catch (e) {
        setError((e as Error).message);
        throw e;
      }
    },
    [refresh],
  );
  useEffect(() => {
    if (initialized.current) return;
    initialized.current = true;
    const unsubscribe = onHost((event) => {
      if (event.type === "snapshot" && event.state)
        accept(event.state as Snapshot);
      if (event.type === "closeRequested") setPendingPage("__close");
      if (event.type === "resume") {
        void refresh().catch((e) => setError(e.message));
      }
      if (event.type === "connectionError")
        setError(event.error ?? "Connection interrupted.");
    });
    if (native)
      void rpc<{ viewState: Record<string, string> }>("ui.ready")
        .then(async (result) => {
          const saved = restoreView(result.viewState);
          setPage(saved.page);
          setFilters(saved.filters);
          positions.current = saved.scroll;
          setLibrary(saved.library);
          await refresh();
        })
        .catch((e) => setError(e.message));
    else if (!preview)
      setError(
        "Open YoWidgets on your desktop to connect to its native engine.",
      );
    return unsubscribe;
  }, [accept, refresh]);
  useEffect(() => {
    if (page === "home" && native) void rpc("catalog.refresh").catch(() => {});
  }, [page]);
  useEffect(() => {
    if (!native) return;
    const persist = () => {
      positions.current[page] = main.current?.scrollTop ?? 0;
      void rpc("ui.persist", {
        page,
        ...filters,
        library: JSON.stringify(library),
        scroll: JSON.stringify(positions.current),
      }).catch(() => {});
    };
    const timer = setTimeout(persist, 250);
    const element = main.current;
    let scrollTimer: ReturnType<typeof setTimeout>;
    const scroll = () => {
      clearTimeout(scrollTimer);
      scrollTimer = setTimeout(persist, 250);
    };
    element?.addEventListener("scroll", scroll, { passive: true });
    window.addEventListener("yo:suspend", persist);
    return () => {
      clearTimeout(timer);
      clearTimeout(scrollTimer);
      element?.removeEventListener("scroll", scroll);
      window.removeEventListener("yo:suspend", persist);
    };
  }, [page, filters, library]);
  useLayoutEffect(() => {
    if (main.current) main.current.scrollTop = positions.current[page] ?? 0;
  }, [page, Boolean(state)]);
  useEffect(() => {
    if (native) void rpc("ui.dirty", { dirty }).catch(() => {});
  }, [dirty]);
  useEffect(() => {
    if (toast) {
      const timer = setTimeout(() => setToast(""), 3200);
      return () => clearTimeout(timer);
    }
  }, [toast]);
  useEffect(() => {
    const key = (event: KeyboardEvent) => {
      if (event.ctrlKey && event.key.toLowerCase() === "k") {
        event.preventDefault();
        if (document.querySelector('[role="dialog"]')) return;
        if (dirtyRef.current) {
          setPendingPage("home");
          return;
        }
        if (page !== "home") {
          positions.current[page] = main.current?.scrollTop ?? 0;
          setPage("home");
          requestAnimationFrame(() =>
            document.getElementById("catalog-search")?.focus(),
          );
        } else document.getElementById("catalog-search")?.focus();
      }
    };
    window.addEventListener("keydown", key);
    return () => window.removeEventListener("keydown", key);
  }, [page]);
  useLayoutEffect(() => {
    const update = () => {
      const active = rail.current?.querySelector<HTMLButtonElement>(
        '[aria-current="page"]',
      );
      if (active && pill.current) {
        pill.current.style.transform = `translateY(${active.offsetTop}px)`;
        pill.current.style.opacity = "1";
      }
    };
    update();
    const observer = new ResizeObserver(update);
    if (rail.current) observer.observe(rail.current);
    return () => observer.disconnect();
  }, [page]);
  const navigate = (target: string, force = false) => {
    if (target === page) return;
    if (dirty && !force) {
      setPendingPage(target);
      return;
    }
    positions.current[page] = main.current?.scrollTop ?? 0;
    setDirty(false);
    setPage(target);
    setSelected(null);
    setError("");
    if (native) void rpc("app.sound", { sound: "navigate" }).catch(() => {});
  };
  const section = page.startsWith("settings/") ? page.split("/")[1] : "general";
  const navPage = page.startsWith("settings") ? "settings" : page;
  const pending =
    state?.jobs.filter((j) =>
      ["queued", "downloading", "verifying", "installing"].includes(j.stage),
    ).length ?? 0;
  const links = [
    { id: "home", label: "Home", icon: House },
    { id: "library", label: "Library", icon: Library },
    { id: "downloads", label: "Downloads", icon: ArrowDownToLine },
  ];
  return (
    <Tooltip.Provider delayDuration={350}>
      <div className="application">
        <header
          className="window-caption"
          onMouseDown={(e) => {
            if (e.button === 0 && e.target === e.currentTarget && native)
              void rpc("window.drag").catch(() => {});
          }}
          onDoubleClick={(e) => {
            if (e.target === e.currentTarget && native)
              void rpc("window.maximize").catch(() => {});
          }}
        >
          <span>YoWidgets</span>
          {preview && <span className="preview-badge">Design preview</span>}
          <div className="window-controls">
            <button
              aria-label="Minimize"
              onClick={() => void rpc("window.minimize").catch(() => {})}
            >
              <Minus size={14} />
            </button>
            <button
              aria-label="Maximize or restore"
              onClick={() => void rpc("window.maximize").catch(() => {})}
            >
              <Maximize2 size={12} />
            </button>
            <button
              aria-label="Close to tray"
              onClick={() => {
                if (dirty) setPendingPage("__close");
                else void rpc("window.close").catch(() => {});
              }}
            >
              <X size={15} />
            </button>
          </div>
        </header>
        <div className="app-frame">
          <aside className="sidebar" ref={rail} aria-label="Main navigation">
            <button
              className="brand-button"
              aria-label="YoWidgets Home"
              onClick={() => navigate("home")}
            >
              <span className="brand-ring" />
            </button>
            <span ref={pill} className="liquid-spot" aria-hidden="true" />
            <nav className="primary-navigation" aria-label="Your space">
              {links.map(({ id, label, icon: Icon }) => (
                <Hint label={label} key={id}>
                  <button
                    aria-label={label}
                    aria-current={navPage === id ? "page" : undefined}
                    className={`nav-button icon-${id}`}
                    onClick={() => navigate(id)}
                  >
                    <Icon size={21} strokeWidth={1.65} />
                    {id === "downloads" && pending > 0 && (
                      <span className="nav-notification">{pending}</span>
                    )}
                  </button>
                </Hint>
              ))}
            </nav>
            <div className="sidebar-bottom">
              <Hint
                label={state?.snoozed ? "Resume widgets" : "Snooze widgets"}
              >
                <button
                  className="nav-button icon-snooze"
                  aria-label={
                    state?.snoozed ? "Resume widgets" : "Snooze widgets"
                  }
                  aria-pressed={state?.snoozed ?? false}
                  onClick={() => void run("app.snooze").catch(() => {})}
                >
                  {state?.snoozed ? <Sun size={21} /> : <Moon size={21} />}
                </button>
              </Hint>
              <Hint label="Settings">
                <button
                  className="nav-button icon-settings"
                  aria-label="Settings"
                  aria-current={navPage === "settings" ? "page" : undefined}
                  onClick={() => navigate("settings/general")}
                >
                  <SettingsIcon size={21} strokeWidth={1.65} />
                </button>
              </Hint>
            </div>
          </aside>
          <main className="main-content" ref={main} id="main-content">
            {error && (
              <div className="error-banner" role="alert">
                <span>{error}</span>
                <button aria-label="Dismiss error" onClick={() => setError("")}>
                  <X size={15} />
                </button>
              </div>
            )}
            {state?.snoozed && (
              <div className="notice-banner snooze-banner">
                <Moon size={15} />
                <span>Widgets are paused</span>
                <button onClick={() => void run("app.snooze").catch(() => {})}>
                  Resume
                </button>
              </div>
            )}
            {!state ? (
              <div className="connecting-state">
                <span className="brand-ring" />
                <h1>Your space is opening</h1>
                <p>
                  {error
                    ? "The native engine is not connected."
                    : "Connecting to your desktop"}
                </p>
                {native && (
                  <Button
                    onClick={() =>
                      void refresh().catch((e) => setError(e.message))
                    }
                  >
                    Retry connection
                  </Button>
                )}
              </div>
            ) : page === "home" ? (
              <CatalogView
                state={state}
                filters={filters}
                setFilters={setFilters}
                run={run}
                onOpen={setSelected}
              />
            ) : page === "library" ? (
              <LibraryView
                preferences={library}
                setPreferences={setLibrary}
                state={state}
                run={run}
                onBrowse={() => navigate("home")}
                onOpen={setSelected}
              />
            ) : page === "downloads" ? (
              <DownloadsView
                state={state}
                run={run}
                onBrowse={() => navigate("home")}
              />
            ) : (
              <SettingsView
                key={settingsEpoch}
                state={state}
                section={section}
                setSection={(value) => navigate("settings/" + value)}
                run={run}
                onDirty={setDirty}
                requestedProvider={provider}
              />
            )}
          </main>
        </div>
        {toast && (
          <div className="toast" role="status">
            <CheckCircle2 size={16} />
            {toast}
          </div>
        )}
        {selected && state && (
          <ItemDetails
            item={selected}
            state={state}
            run={run}
            onClose={() => setSelected(null)}
            onSelect={setSelected}
          />
        )}
        <Modal
          open={Boolean(pendingPage)}
          onClose={() => setPendingPage("")}
          title="Leave without saving?"
          description="Your unsaved connection or preferences will be discarded."
        >
          <div className="dialog-actions">
            <Button onClick={() => setPendingPage("")}>Keep editing</Button>
            <Button
              variant="primary"
              onClick={() => {
                if (pendingPage === "__close") {
                  setDirty(false);
                  setSettingsEpoch((e) => e + 1);
                  void rpc("window.close", { discard: true }).catch(() => {});
                } else {
                  setSettingsEpoch((e) => e + 1);
                  navigate(pendingPage, true);
                }
                setPendingPage("");
              }}
            >
              Discard changes
            </Button>
          </div>
        </Modal>
      </div>
    </Tooltip.Provider>
  );
}
