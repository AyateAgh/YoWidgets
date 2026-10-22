import { useState } from "react";
import {
  ArrowDownToLine,
  Check,
  Library,
  MoreHorizontal,
  Pause,
  Play,
  Plus,
  Search,
  Trash2,
  X,
} from "lucide-react";
import type { CatalogItem, LibraryPreferences, Run, Snapshot } from "./types";
import {
  collections,
  activeCount,
  belongs,
  kindNames,
  packName,
} from "./catalog";
import { Button, Dropdown, Empty, Modal } from "./primitives";
import { WidgetPreview } from "./WidgetPreview";
import { InstallButton } from "./CatalogView";
export function LibraryView({
  state,
  run,
  onBrowse,
  onOpen,
  preferences,
  setPreferences,
}: {
  state: Snapshot;
  run: Run;
  onBrowse: () => void;
  onOpen: (item: CatalogItem) => void;
  preferences: LibraryPreferences;
  setPreferences: (value: LibraryPreferences) => void;
}) {
  const { tab, query, pack } = preferences;
  const setTab = (tab: string) => setPreferences({ ...preferences, tab });
  const setQuery = (query: string) => setPreferences({ ...preferences, query });
  const setPack = (pack: string) => setPreferences({ ...preferences, pack });
  const [removing, setRemoving] = useState<CatalogItem | null>(null);
  const [busy, setBusy] = useState(false);
  const items = state.installed
    .map((i) => state.catalog.items.find((c) => c.id === i.item.id) ?? i.item)
    .filter(
      (i) =>
        (pack === "all" || i.pack === pack) &&
        `${i.title} ${packName(i.pack)}`
          .toLowerCase()
          .includes(query.toLowerCase()) &&
        (tab !== "active" || state.widgets.some((w) => belongs(i, w))),
    );
  const remove = async () => {
    if (!removing) return;
    setBusy(true);
    try {
      await run("content.remove", { id: removing.id }, "Content uninstalled");
      setRemoving(null);
    } catch {
    } finally {
      setBusy(false);
    }
  };
  return (
    <>
      <header className="page-header">
        <div className="heading-group">
          <h1>Library</h1>
          <span className="item-count">{state.installed.length} installed</span>
        </div>
        <Button onClick={onBrowse}>
          <Plus size={15} /> Find something new
        </Button>
      </header>
      <div className="library-toolbar">
        <div className="segmented" aria-label="Library view">
          <button
            className={tab === "installed" ? "selected" : ""}
            onClick={() => setTab("installed")}
          >
            Installed
          </button>
          <button
            className={tab === "active" ? "selected" : ""}
            onClick={() => setTab("active")}
          >
            On desktop <span>{state.activeCount}</span>
          </button>
        </div>
        <div className="header-tools">
          <div className="search-field compact">
            <Search size={15} />
            <input
              aria-label="Search library"
              placeholder="Search library"
              value={query}
              onChange={(e) => setQuery(e.target.value)}
            />
            {query && (
              <button
                aria-label="Clear library search"
                onClick={() => setQuery("")}
              >
                <X size={14} />
              </button>
            )}
          </div>
          <Dropdown
            label="Library collection"
            value={pack}
            onChange={setPack}
            options={[
              { value: "all", label: "All collections" },
              ...collections(state.installed.map((i) => i.item)),
            ]}
          />
        </div>
      </div>
      {!items.length ? (
        <Empty
          icon={<Library size={30} />}
          title={
            state.installed.length
              ? "Nothing here yet"
              : "Your space starts here"
          }
          description={
            state.installed.length
              ? "Change your filters or add a widget to your desktop."
              : "Install a widget or collection from Home. It will be waiting here."
          }
          action={
            <Button variant="primary" onClick={onBrowse}>
              <Plus size={15} /> Explore widgets
            </Button>
          }
        />
      ) : tab === "active" ? (
        <div className="desktop-list">
          {items.map((item) => (
            <section className="desktop-group" key={item.id}>
              <div className="desktop-group-heading">
                <div>
                  <h2>{item.title}</h2>
                  <span>{packName(item.pack)}</span>
                </div>
                <InstallButton item={item} state={state} run={run} />
              </div>
              {state.widgets
                .filter((w) => belongs(item, w))
                .map((widget, index) => (
                  <div className="instance-row" key={widget.id}>
                    <div className="instance-preview">
                      <WidgetPreview item={item} />
                    </div>
                    <div className="instance-name">
                      <b>
                        {item.title}
                        {index > 0 ? ` ${index + 1}` : ""}
                      </b>
                      <span
                        className={
                          widget.enabled ? "status-dot active" : "status-dot"
                        }
                      >
                        {widget.enabled ? "On desktop" : "Hidden"}
                      </span>
                    </div>
                    <Dropdown
                      label={`Size of ${item.title} ${index + 1}`}
                      value={widget.size}
                      onChange={(size) =>
                        void run("widget.size", { id: widget.id, size }).catch(
                          () => {},
                        )
                      }
                      options={["S", "M", "L"].map((value) => ({
                        value,
                        label:
                          value === "S"
                            ? "Small"
                            : value === "M"
                              ? "Medium"
                              : "Large",
                      }))}
                    />
                    <Button
                      onClick={() =>
                        void run(
                          widget.enabled ? "widget.disable" : "widget.enable",
                          { id: widget.id },
                        ).catch(() => {})
                      }
                    >
                      {widget.enabled ? (
                        <Pause size={14} />
                      ) : (
                        <Play size={14} />
                      )}{" "}
                      {widget.enabled ? "Hide" : "Show"}
                    </Button>
                    <button
                      className="icon-button"
                      aria-label={`Remove ${item.title} instance`}
                      onClick={() =>
                        void run(
                          "widget.remove",
                          { id: widget.id },
                          "Instance removed",
                        ).catch(() => {})
                      }
                    >
                      <X size={16} />
                    </button>
                  </div>
                ))}
            </section>
          ))}
        </div>
      ) : (
        <div className="catalog-grid library-grid">
          {items.map((item) => (
            <article className="catalog-card" key={item.id}>
              <div className="card-heading">
                <div>
                  <h3>{item.title}</h3>
                  <span>{packName(item.pack)}</span>
                </div>
                <InstallButton item={item} state={state} run={run} small />
              </div>
              <button
                className="preview-button"
                onClick={() => onOpen(item)}
                aria-label={`View ${item.title} details`}
              >
                <WidgetPreview item={item} />
              </button>
              <div className="card-footer">
                <span
                  className={
                    activeCount(item, state.widgets)
                      ? "status-dot active"
                      : "status-dot"
                  }
                >
                  {activeCount(item, state.widgets)
                    ? `${activeCount(item, state.widgets)} active`
                    : "Installed"}
                </span>
                <button
                  className="icon-button subtle"
                  aria-label={`Uninstall ${item.title} ${packName(item.pack)}`}
                  onClick={() => setRemoving(item)}
                >
                  <Trash2 size={14} />
                </button>
              </div>
            </article>
          ))}
        </div>
      )}
      <Modal
        open={Boolean(removing)}
        onClose={() => !busy && setRemoving(null)}
        title={`Uninstall ${removing?.title ?? "widget"}?`}
        description="Its desktop instances will be removed. You can install this widget again from Home."
      >
        <div className="dialog-actions">
          <Button disabled={busy} onClick={() => setRemoving(null)}>
            Cancel
          </Button>
          <Button variant="danger" busy={busy} onClick={() => void remove()}>
            Uninstall widget
          </Button>
        </div>
      </Modal>
    </>
  );
}
