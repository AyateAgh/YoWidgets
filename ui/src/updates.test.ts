import { describe, it, expect } from "vitest";
import { needsUpdate } from "./catalog";
import catalog from "../../catalog/v1/catalog.json";
import type { Catalog, Installed } from "./types";
const data = catalog as Catalog;
const item = data.items.find((i) => i.id === "glass-clock")!;
const local = (version: string): Installed[] => [
  { item: { ...item, version }, packages: [], installedAt: "" },
];
describe("version-aware actions", () => {
  it("offers updates only for strictly newer numeric versions", () => {
    expect(needsUpdate(item, local("1.0.0"), data)).toBe(false);
    expect(needsUpdate(item, local("2.0.0"), data)).toBe(false);
    expect(
      needsUpdate({ ...item, version: "1.10.0" }, local("1.9.0"), data),
    ).toBe(true);
    expect(needsUpdate(item, [], data)).toBe(false);
  });
});
