import { defaults } from "./types";
import type { Filters, LibraryPreferences } from "./types";
const pages = [
  "home",
  "library",
  "downloads",
  ...[
    "general",
    "appearance",
    "widgets",
    "connections",
    "permissions",
    "about",
  ].map((s) => "settings/" + s),
];
const text = (v: unknown, fallback = "") =>
  typeof v === "string" && v.length <= 200 ? v : fallback;
const object = (raw: string | undefined): Record<string, unknown> => {
  try {
    const v = JSON.parse(raw ?? "{}");
    return v && typeof v === "object" && !Array.isArray(v) ? v : {};
  } catch {
    return {};
  }
};
export function restoreView(saved: Record<string, string>) {
  const filters: Filters = { ...defaults };
  for (const key of Object.keys(defaults) as (keyof Filters)[])
    filters[key] = text(saved[key], defaults[key]);
  if (
    !["all", "widgets", "pets", "taskbars", "docks", "system"].includes(
      filters.category,
    )
  )
    filters.category = "all";
  if (!["recent", "name", "size"].includes(filters.sort))
    filters.sort = "recent";
  if (!["all", "available", "installed", "active"].includes(filters.status))
    filters.status = "all";
  const raw = object(saved.library);
  const library: LibraryPreferences = {
    query: text(raw.query),
    tab: raw.tab === "active" || raw.tab === "hidden" ? raw.tab : "installed",
    pack: text(raw.pack, "all"),
    sort: "recent",
  };
  const scroll: Record<string, number> = {};
  for (const [page, value] of Object.entries(object(saved.scroll)))
    if (
      pages.includes(page) &&
      typeof value === "number" &&
      Number.isFinite(value) &&
      value >= 0
    )
      scroll[page] = Math.min(value, 1000000);
  return {
    page: pages.includes(saved.page) ? saved.page : "home",
    filters,
    library,
    scroll,
  };
}
