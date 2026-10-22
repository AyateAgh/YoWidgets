import { useState } from "react";
import {
  ArrowDownToLine,
  Check,
  ChevronRight,
  Grid2X2,
  Package,
  Plus,
  Search,
  SlidersHorizontal,
  WifiOff,
  X,
} from "lucide-react";
import type { CatalogItem, Filters, Snapshot, Run } from "./types";
import { defaults } from "./types";
import {
  collections,
  activeCount,
  downloadBytes,
  filterCatalog,
  formatBytes,
  installedCount,
  isInstalled,
  kindNames,
  packName,
} from "./catalog";
import { Button, Dropdown, Empty, Modal } from "./primitives";
import { WidgetPreview } from "./WidgetPreview";

export function InstallButton({
  item,
  state,
  run,
  small = false,
}: {
  item: CatalogItem;
  state: Snapshot;
  run: Run;
  small?: boolean;
}) {
  const [busy, setBusy] = useState(false);
  const job = state.jobs.find(
    (j) =>
      j.itemId === item.id &&
      ["queued", "downloading", "verifying", "installing"].includes(j.stage),
  );
  const installed = isInstalled(item, state.installed);
  const active = activeCount(item, state.widgets);
  const needsUpdate =
    installed &&
    (item.type === "pack"
      ? state.installed.some(
          (i) =>
            item.includes.includes(i.item.id) &&
            i.item.version !==
              state.catalog.items.find((x) => x.id === i.item.id)?.version,
        )
      : state.installed.find((i) => i.item.id === item.id)?.item.version !==
        item.version);
  const action = async () => {
    setBusy(true);
    try {
      if (!installed || needsUpdate)
        await run("content.install", { id: item.id });
      else if (item.type === "widget")
        await run("widget.add", { id: item.id }, "Added to desktop");
    } catch {
    } finally {
      setBusy(false);
    }
  };
  if (job)
    return (
      <Button className={small ? "small" : ""} busy disabled>
        {job.stage === "downloading" && job.total
          ? `${Math.floor((job.downloaded / job.total) * 100)}%`
          : "Installing"}
      </Button>
    );
  if (installed && item.type === "pack" && !needsUpdate)
    return (
      <span className="installed-label">
        <Check size={14} /> Installed
      </span>
    );
  return (
    <Button
      variant={!installed || needsUpdate ? "primary" : "quiet"}
      className={small ? "small" : ""}
      busy={busy}
      onClick={() => void action()}
      aria-label={`${!installed ? "Install" : needsUpdate ? "Update" : "Add"} ${item.title} ${packName(item.pack)}`}
    >
      {!installed || needsUpdate ? (
        <ArrowDownToLine size={14} />
      ) : (
        <Plus size={14} />
      )}
      <span>
        {!installed
          ? "Install"
          : needsUpdate
            ? "Update"
            : active
              ? "Add another"
              : "Add to desktop"}
      </span>
    </Button>
  );
}
export function CatalogCard({
  item,
  state,
  run,
  onOpen,
}: {
  item: CatalogItem;
  state: Snapshot;
  run: Run;
  onOpen: () => void;
}) {
  const installed = isInstalled(item, state.installed);
  const active = activeCount(item, state.widgets);
  return (
    <article
      className={`catalog-card ${item.type === "pack" ? "pack-card" : ""}`}
    >
      <div className="card-heading">
        <div>
          <h3>{item.title}</h3>
          <span>
            {item.type === "pack"
              ? `${item.includes.length} widgets`
              : packName(item.pack)}
          </span>
        </div>
        <InstallButton item={item} state={state} run={run} small />
      </div>
      <button
        className="preview-button"
        onClick={onOpen}
        aria-label={`View ${item.title} in ${packName(item.pack)}`}
      >
        <WidgetPreview item={item} />
        <span className="preview-open">
          <ChevronRight size={17} />
        </span>
      </button>
      <div className="card-footer">
        <span>
          {item.type === "pack"
            ? item.description
            : item.tags.slice(0, 2).join(" · ")}
        </span>
        <span
          className={
            active ? "status-dot active" : installed ? "status-dot" : ""
          }
        >
          {active
            ? `${active} active`
            : installed
              ? "Installed"
              : formatBytes(
                  downloadBytes(item, state.catalog, state.installed),
                )}
        </span>
      </div>
    </article>
  );
}

export function CatalogView({
  state,
  filters,
  setFilters,
  run,
  onOpen,
}: {
  state: Snapshot;
  filters: Filters;
  setFilters: (value: Filters) => void;
  run: Run;
  onOpen: (item: CatalogItem) => void;
}) {
  const [expanded, setExpanded] = useState(false);
  const [limit, setLimit] = useState(24);
  const [resultKey, setResultKey] = useState("");
  const key = JSON.stringify(filters);
  if (resultKey !== key) {
    setResultKey(key);
    setLimit(24);
  }
  const found = filterCatalog(
    state.catalog.items,
    filters,
    state.installed,
    state.widgets,
    state.catalog,
  );
  const packs = found.filter((i) => i.type === "pack");
  const widgets = found.filter((i) => i.type === "widget");
  const count =
    Number(filters.kind !== "all") +
    Number(filters.pack !== "all") +
    Number(filters.status !== "all");
  const categories = [
    ["all", "All"],
    ["widgets", "Widgets"],
    ["pets", "Pets"],
    ["taskbars", "Taskbars"],
    ["docks", "Docks"],
    ["system", "System"],
  ];
  const set = (key: keyof Filters, value: string) =>
    setFilters({ ...filters, [key]: value });
  return (
    <>
      <header className="page-header">
        <div className="heading-group">
          <h1>Home</h1>
          <span className="item-count">{state.catalog.items.length} items</span>
        </div>
        <div className="header-tools">
          <div className="search-field">
            <Search size={16} />
            <input
              id="catalog-search"
              aria-label="Search catalog"
              placeholder="Search your next favourite"
              value={filters.query}
              onChange={(e) => set("query", e.target.value)}
            />
            {filters.query ? (
              <button
                aria-label="Clear search"
                onClick={() => set("query", "")}
              >
                <X size={14} />
              </button>
            ) : (
              <kbd>Ctrl K</kbd>
            )}
          </div>
          <Button
            onClick={() => setExpanded(!expanded)}
            aria-expanded={expanded}
            aria-controls="catalog-filters"
          >
            <SlidersHorizontal size={16} /> Filters
            {count > 0 && <span className="count-badge">{count}</span>}
          </Button>
        </div>
      </header>
      <div className="catalog-navigation">
        <div
          className="category-tabs"
          role="group"
          aria-label="Content category"
        >
          {categories.map(([value, label]) => (
            <button
              key={value}
              aria-pressed={filters.category === value}
              className={filters.category === value ? "selected" : ""}
              onClick={() => set("category", value)}
            >
              {label}
            </button>
          ))}
        </div>
        <Dropdown
          label="Sort catalog"
          value={filters.sort}
          onChange={(v) => set("sort", v)}
          options={[
            { value: "recent", label: "Recently added" },
            { value: "name", label: "Name A–Z" },
            { value: "size", label: "Download size" },
          ]}
        />
      </div>
      {expanded && (
        <div className="filter-bar" id="catalog-filters">
          <div>
            <span>Type</span>
            <Dropdown
              label="Widget type"
              value={filters.kind}
              onChange={(v) => set("kind", v)}
              options={[
                { value: "all", label: "All types" },
                { value: "pack", label: "Packs" },
                ...Object.entries(kindNames).map(([value, label]) => ({
                  value,
                  label,
                })),
              ]}
            />
          </div>
          <div>
            <span>Collection</span>
            <Dropdown
              label="Collection"
              value={filters.pack}
              onChange={(v) => set("pack", v)}
              options={[
                { value: "all", label: "All collections" },
                ...collections(state.catalog.items),
              ]}
            />
          </div>
          <div>
            <span>Status</span>
            <Dropdown
              label="Installation status"
              value={filters.status}
              onChange={(v) => set("status", v)}
              options={[
                { value: "all", label: "Everything" },
                { value: "available", label: "Not installed" },
                { value: "installed", label: "Installed" },
                { value: "active", label: "Active" },
              ]}
            />
          </div>
          <Button
            variant="ghost"
            disabled={!count && !filters.query}
            onClick={() =>
              setFilters({ ...defaults, category: filters.category })
            }
          >
            Reset filters
          </Button>
        </div>
      )}
      {state.catalogStatus === "offline" && (
        <div className="notice-banner" role="status">
          <WifiOff size={16} />
          <span>{state.catalogError}</span>
          <button onClick={() => void run("catalog.refresh").catch(() => {})}>
            Retry
          </button>
        </div>
      )}
      {state.recovery && (
        <div className="notice-banner" role="status">
          <Package size={16} />
          <span>{state.recovery}</span>
        </div>
      )}
      {!found.length ? (
        <Empty
          icon={<Grid2X2 size={30} />}
          title={
            filters.category !== "all" && filters.category !== "widgets"
              ? "A little more to come"
              : "No matches"
          }
          description={
            filters.category !== "all" && filters.category !== "widgets"
              ? "This category will open when its first module is ready."
              : "Try a different search or clear your filters."
          }
          action={
            <Button onClick={() => setFilters(defaults)}>
              {filters.category !== "all" && filters.category !== "widgets"
                ? "Explore widgets"
                : "Clear filters"}
            </Button>
          }
        />
      ) : (
        <>
          {packs.length > 0 && (
            <section className="catalog-section">
              <div className="list-heading">
                <h2>Collections</h2>
                <span>{packs.length}</span>
              </div>
              <div className="pack-grid">
                {packs.slice(0, limit).map((item) => (
                  <CatalogCard
                    key={item.id}
                    item={item}
                    state={state}
                    run={run}
                    onOpen={() => onOpen(item)}
                  />
                ))}
              </div>
            </section>
          )}
          {widgets.length > 0 && (
            <section className="catalog-section">
              <div className="list-heading">
                <h2>{filters.query ? "Search results" : "Widgets"}</h2>
                <span>{widgets.length}</span>
              </div>
              <div className="catalog-grid">
                {widgets.slice(0, limit).map((item) => (
                  <CatalogCard
                    key={item.id}
                    item={item}
                    state={state}
                    run={run}
                    onOpen={() => onOpen(item)}
                  />
                ))}
              </div>
            </section>
          )}
          {(packs.length > limit || widgets.length > limit) && (
            <div className="dialog-actions">
              <Button onClick={() => setLimit((n) => n + 24)}>Show more</Button>
            </div>
          )}{" "}
        </>
      )}
    </>
  );
}

export function ItemDetails({
  item,
  state,
  run,
  onClose,
  onSelect,
}: {
  item: CatalogItem;
  state: Snapshot;
  run: Run;
  onClose: () => void;
  onSelect: (item: CatalogItem) => void;
}) {
  const count = installedCount(item, state.installed);
  const included = state.catalog.items.filter((i) =>
    item.includes.includes(i.id),
  );
  return (
    <Modal
      open
      onClose={onClose}
      title={item.title}
      description={`${packName(item.pack)} · By ${item.author}`}
      className="item-dialog"
    >
      <div className="item-detail-grid">
        <div className="item-detail-preview">
          <WidgetPreview item={item} large />
        </div>
        <div className="item-detail-info">
          <span className="detail-kind">
            {item.type === "pack"
              ? `${item.includes.length} widgets`
              : kindNames[item.kind]}
          </span>
          <p>{item.description}</p>
          <InstallButton item={item} state={state} run={run} />
          <dl>
            <div>
              <dt>Collection</dt>
              <dd>{packName(item.pack)}</dd>
            </div>
            <div>
              <dt>Download</dt>
              <dd>
                {formatBytes(
                  downloadBytes(item, state.catalog, state.installed),
                )}
              </dd>
            </div>
            <div>
              <dt>Version</dt>
              <dd>{item.version}</dd>
            </div>
            <div>
              <dt>Compatibility</dt>
              <dd>Windows 11</dd>
            </div>
            {item.type === "pack" && (
              <div>
                <dt>Installed</dt>
                <dd>
                  {count} of {item.includes.length}
                </dd>
              </div>
            )}
          </dl>
          {item.permissions.length > 0 && (
            <div className="permission-note">
              <h3>Connections & access</h3>
              {item.permissions.map((p) => (
                <p key={p}>{p}</p>
              ))}
            </div>
          )}
        </div>
      </div>
      {item.type === "pack" && (
        <div className="pack-includes">
          <h2>In this collection</h2>
          <div className="included-grid">
            {included.map((i) => (
              <button key={i.id} onClick={() => onSelect(i)}>
                <WidgetPreview item={i} />
                <span>{i.title}</span>
                {isInstalled(i, state.installed) && <Check size={14} />}
              </button>
            ))}
          </div>
        </div>
      )}
    </Modal>
  );
}
