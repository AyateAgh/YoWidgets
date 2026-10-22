import { describe, it, expect } from "vitest";
import catalog from "../../catalog/v1/catalog.json";
import { filterCatalog, isInstalled, downloadBytes } from "./catalog";
import { defaults } from "./types";
import type { Catalog, Installed } from "./types";
const data = catalog as Catalog;
describe("catalog discovery", () => {
  it("combines search, style and kind instead of resetting other filters", () => {
    const found = filterCatalog(
      data.items,
      { ...defaults, query: "time", pack: "liquid", kind: "clock" },
      [],
      [],
      data,
    );
    expect(found.map((x) => x.id)).toEqual(["liquid-clock"]);
  });
  it("searches tags and handles extra spaces", () => {
    expect(
      filterCatalog(
        data.items,
        { ...defaults, query: "  AI   liquid  " },
        [],
        [],
        data,
      ).map((x) => x.id),
    ).toEqual(["liquid-assistant"]);
  });
  it("never invents content for future categories", () =>
    expect(
      filterCatalog(
        data.items,
        { ...defaults, category: "pets" },
        [],
        [],
        data,
      ),
    ).toEqual([]));
  it("distinguishes partially installed packs", () => {
    const item = data.items.find((i) => i.id === "glass-clock")!;
    const installed: Installed[] = [{ item, packages: [], installedAt: "" }];
    expect(
      isInstalled(
        data.items.find((i) => i.id === "pack-glass")!,
        installed,
      ),
    ).toBe(false);
    expect(isInstalled(item, installed)).toBe(true);
  });
  it("charges common dependencies only once and excludes downloaded versions", () => {
    const pack = data.items.find((i) => i.id === "pack-glass")!;
    const expected = data.packages
      .filter((p) => p.id.startsWith("glass.") || p.id === "style.glass")
      .reduce((s, p) => s + p.bytes, 0);
    expect(downloadBytes(pack, data, [])).toBe(expected);
  });
  it("sorts names consistently", () => {
    const found = filterCatalog(
      data.items,
      { ...defaults, kind: "clock", sort: "name" },
      [],
      [],
      data,
    );
    expect(found).toHaveLength(2);
    expect(found[0].title).toBe("Clock");
  });
});
