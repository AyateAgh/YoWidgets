// @vitest-environment jsdom
import { it, expect, vi, afterEach } from "vitest";
import { render, waitFor, cleanup, screen, act } from "@testing-library/react";
const bridge = vi.hoisted(() => ({
  rpc: vi.fn(),
  listen: vi.fn((_: unknown) => () => {}),
}));
vi.mock("./bridge", () => ({
  native: true,
  preview: false,
  rpc: bridge.rpc,
  onHost: bridge.listen,
}));
import { App } from "./App";
import { previewState } from "./preview";
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

it("a requested page received during reopening cannot strand the prepared interface", async () => {
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
  const queued = new Map<number, FrameRequestCallback>();
  let id = 0;
  vi.stubGlobal("requestAnimationFrame", (callback: FrameRequestCallback) => {
    queued.set(++id, callback);
    return id;
  });
  vi.stubGlobal("cancelAnimationFrame", (value: number) =>
    queued.delete(value),
  );
  bridge.rpc.mockImplementation(async (method: string) =>
    method === "ui.ready"
      ? { viewState: {} }
      : method === "state"
        ? previewState
        : {},
  );
  render(<App />);
  await waitFor(() => expect(bridge.rpc).toHaveBeenCalledWith("ui.present"));
  const listener = bridge.listen.mock.calls.at(-1)![0] as (
    value: Record<string, unknown>,
  ) => void;
  await act(async () => {
    listener({ type: "present" });
    listener({
      type: "snapshot",
      state: {
        ...previewState,
        revision: previewState.revision + 1,
        requestedPage: "settings",
        requestedProvider: "OpenAI",
      },
    });
  });
  await act(async () => {
    for (let count = 0; queued.size && count < 10; count++) {
      const entries = [...queued.values()];
      queued.clear();
      entries.forEach((callback) => callback(0));
    }
  });
  expect(
    document.querySelector<HTMLElement>(".application")?.dataset.presentation,
  ).toBe("visible");
});
