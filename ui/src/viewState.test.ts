import { describe, it, expect } from "vitest";
import { restoreView } from "./viewState";
describe("saved management view", () => {
  it("ignores malformed preferences without preventing launch", () => {
    expect(
      restoreView({ library: "null", scroll: "[]", page: "unknown" }).page,
    ).toBe("home");
    expect(
      restoreView({ library: '{"query":null}', scroll: '{"home":-4}' }).library
        .query,
    ).toBe("");
  });
  it("restores only bounded known state", () => {
    const s = restoreView({
      page: "settings/connections",
      library: '{"query":"clock","tab":"active","pack":"custom"}',
      scroll: '{"home":320}',
      sort: "name",
    });
    expect(s.page).toBe("settings/connections");
    expect(s.library.tab).toBe("active");
    expect(s.scroll.home).toBe(320);
    expect(s.filters.sort).toBe("name");
  });
});
