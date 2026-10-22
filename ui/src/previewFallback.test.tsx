// @vitest-environment jsdom
import { it, expect, vi, afterEach } from "vitest";
import { render, cleanup } from "@testing-library/react";
vi.mock("./bridge", () => ({
  native: false,
  preview: false,
  rpc: vi.fn(),
  onHost: () => () => {},
}));
import { App } from "./App";
afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
});
it("the unconnected browser route reveals its recovery message", () => {
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
  const queued: FrameRequestCallback[] = [];
  vi.stubGlobal("requestAnimationFrame", (callback: FrameRequestCallback) => {
    queued.push(callback);
    return queued.length;
  });
  vi.stubGlobal("cancelAnimationFrame", vi.fn());
  render(<App />);
  while (queued.length) queued.shift()!(0);
  expect(
    document.querySelector<HTMLElement>(".application")?.dataset.presentation,
  ).toBe("visible");
  expect(document.querySelector('[role="alert"]')?.textContent).toContain(
    "Open YoWidgets on your desktop",
  );
});
