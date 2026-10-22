import type { Catalog, CatalogItem, Filters, Installed, Widget } from "./types";
export const packName = (pack: string) =>
  pack === "liquid"
    ? "Liquid Glass"
    : pack === "glass"
      ? "Glass"
      : pack
          .split(/[.-]/)
          .map((p) => p.charAt(0).toUpperCase() + p.slice(1))
          .join(" ");
export const kindNames: Record<string, string> = {
  clock: "Clock",
  calendar: "Calendar",
  weather: "Weather",
  music: "Music",
  photo: "Photos",
  focus: "Focus",
  quick: "Controls",
  notifications: "Notifications",
  assistant: "Assistant",
};
export function installedCount(item: CatalogItem, installed: Installed[]) {
  return item.type === "pack"
    ? item.includes.filter((id) =>
        installed.some((i) => i.item.id === id && !i.needsRepair),
      ).length
    : Number(installed.some((i) => i.item.id === item.id && !i.needsRepair));
}
export const isInstalled = (item: CatalogItem, installed: Installed[]) =>
  installedCount(item, installed) ===
  (item.type === "pack" ? item.includes.length : 1);
export const belongs = (item: CatalogItem, widget: Widget) =>
  item.type === "pack"
    ? widget.pack === item.pack
    : widget.contentId
      ? widget.contentId === item.id
      : widget.pack === item.pack && widget.kind === item.kind;
export const activeCount = (item: CatalogItem, widgets: Widget[]) =>
  widgets.filter((w) => w.enabled && belongs(item, w)).length;
export function downloadBytes(
  item: CatalogItem,
  catalog: Catalog,
  installed: Installed[],
) {
  const need = new Set<string>();
  const visiting = new Set<string>();
  function add(id: string) {
    if (need.has(id) || visiting.has(id)) return;
    visiting.add(id);
    const p = catalog.packages.find((p) => p.id === id);
    if (!p) return;
    p.dependencies.forEach(add);
    need.add(id);
  }
  const entries =
    item.type === "pack"
      ? catalog.items.filter((i) => item.includes.includes(i.id))
      : [item];
  entries.forEach((i) => i.packages.forEach(add));
  return [...need].reduce((sum, id) => {
    const p = catalog.packages.find((p) => p.id === id)!;
    return (
      sum +
      (installed.some(
        (i) =>
          !i.needsRepair &&
          i.packages.some(
            (r) =>
              r.id === id && r.version === p.version && r.sha256 === p.sha256,
          ),
      )
        ? 0
        : p.bytes)
    );
  }, 0);
}
export function formatBytes(value: number) {
  if (value < 1024) return value + " B";
  if (value < 1024 * 1024)
    return (value / 1024).toFixed(value < 10240 ? 1 : 0) + " KB";
  return (value / 1024 / 1024).toFixed(1) + " MB";
}
const fold = (s: string) =>
  s
    .normalize("NFKD")
    .replace(/\p{Diacritic}/gu, "")
    .toLowerCase();
export function filterCatalog(
  items: CatalogItem[],
  filters: Filters,
  installed: Installed[],
  widgets: Widget[],
  catalog: Catalog,
) {
  const terms = fold(filters.query).trim().split(/\s+/).filter(Boolean);
  return items
    .filter((item) => {
      if (filters.category !== "all" && item.category !== filters.category)
        return false;
      if (
        filters.kind !== "all" &&
        (filters.kind === "pack"
          ? item.type !== "pack"
          : item.type !== "widget" || item.kind !== filters.kind)
      )
        return false;
      if (filters.pack !== "all" && item.pack !== filters.pack) return false;
      const has = isInstalled(item, installed);
      if (
        (filters.status === "installed" && !has) ||
        (filters.status === "available" && has) ||
        (filters.status === "active" && !activeCount(item, widgets))
      )
        return false;
      const text = fold(
        [
          item.title,
          item.description,
          item.author,
          item.collectionName || packName(item.pack),
          ...item.tags,
        ].join(" "),
      );
      const words = text.split(/[^\p{L}\p{N}]+/u);
      return terms.every((term) =>
        term.length < 3
          ? words.some((word) => word.startsWith(term))
          : text.includes(term),
      );
    })
    .sort((a, b) =>
      filters.sort === "name"
        ? a.title.localeCompare(b.title, "en") || a.pack.localeCompare(b.pack)
        : filters.sort === "size"
          ? downloadBytes(a, catalog, installed) -
              downloadBytes(b, catalog, installed) ||
            a.title.localeCompare(b.title)
          : Date.parse(b.publishedAt) - Date.parse(a.publishedAt) ||
            a.type.localeCompare(b.type) ||
            a.title.localeCompare(b.title),
    );
}

export function collections(items: CatalogItem[]) {
  return [
    ...new Map(
      items.map((i) => [
        i.pack,
        { value: i.pack, label: i.collectionName || packName(i.pack) },
      ]),
    ).values(),
  ].sort((a, b) => a.label.localeCompare(b.label));
}

export function newerVersion(remote: string, local: string) {
  const parse = (v: string) =>
    /^\d+(\.\d+){1,3}$/.test(v) ? v.split(".").map(Number) : null;
  const a = parse(remote),
    b = parse(local);
  if (!a || !b) return false;
  for (let n = 0; n < 4; n++) {
    if ((a[n] ?? 0) !== (b[n] ?? 0)) return (a[n] ?? 0) > (b[n] ?? 0);
  }
  return false;
}
export function needsUpdate(
  item: CatalogItem,
  installed: Installed[],
  catalog: Catalog,
) {
  const items =
    item.type === "pack"
      ? catalog.items.filter((i) => item.includes.includes(i.id))
      : [item];
  return items.some((i) => {
    const local = installed.find((x) => x.item.id === i.id);
    return (
      local && !local.needsRepair && newerVersion(i.version, local.item.version)
    );
  });
}
