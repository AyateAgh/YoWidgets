// @vitest-environment jsdom
import { it, expect, vi, afterEach } from "vitest";
import { render, waitFor, cleanup, screen } from "@testing-library/react";
const bridge = vi.hoisted(() => ({
  rpc: vi.fn(),
  listen: vi.fn(() => () => {}),
}));
vi.mock("./bridge", () => ({
  native: true,
  preview: false,
  rpc: bridge.rpc,
  onHost: bridge.listen,
}));
import { App } from "./App";
afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
});
it("presents a usable retry screen when the first native snapshot fails", async () => {
  vi.stubGlobal(
    "ResizeObserver",
    class {
      observe() {}
      disconnect() {}
    },
  );
  vi.stubGlobal("matchMedia", () => ({
    matches: false,
    addEventListener() {},
    removeEventListener() {},
  }));
  bridge.rpc.mockImplementation(async (method: string) => {
    if (method === "ui.ready") return { viewState: {} };
    if (method === "state") throw new Error("Fixture engine unavailable");
    return {};
  });
  render(<App />);
  await waitFor(() => expect(bridge.rpc).toHaveBeenCalledWith("ui.present"));
  expect(screen.getByRole("button", { name: "Retry connection" })).toBeTruthy();
  expect(screen.getByRole("alert").textContent).toContain(
    "Fixture engine unavailable",
  );
});
