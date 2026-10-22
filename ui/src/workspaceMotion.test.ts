// @vitest-environment jsdom
import { it, expect, vi, afterEach } from "vitest";
import { workspaceMotion } from "./workspaceMotion";
afterEach(() => {
  vi.unstubAllGlobals();
  document.body.innerHTML = "";
});
function fixture(reduced = false) {
  let changed: (() => void) | undefined;
  const disconnect = vi.fn();
  vi.stubGlobal(
    "ResizeObserver",
    class {
      constructor(callback: () => void) {
        changed = callback;
      }
      observe() {}
      disconnect = disconnect;
    },
  );
  const remove = vi.fn(),
    add = vi.fn();
  vi.stubGlobal("matchMedia", () => ({
    matches: reduced,
    addEventListener: add,
    removeEventListener: remove,
  }));
  const main = document.createElement("main"),
    surface = document.createElement("div");
  surface.append(main);
  document.body.append(surface);
  const cancel = vi.fn(),
    animate = vi.fn(() => ({ cancel, finished: new Promise(() => {}) }));
  surface.animate = animate as unknown as typeof surface.animate;
  const dispose = workspaceMotion(main, surface);
  return {
    dispose,
    cancel,
    animate,
    disconnect,
    remove,
    resize: () => changed?.(),
  };
}
it("reopening retargets a reveal and suspension cancels it", () => {
  const f = fixture();
  window.dispatchEvent(new Event("yo:present"));
  window.dispatchEvent(new Event("yo:present"));
  expect(f.animate).toHaveBeenCalledTimes(2);
  expect(f.cancel).toHaveBeenCalledTimes(1);
  window.dispatchEvent(new Event("yo:suspend"));
  expect(f.cancel).toHaveBeenCalledTimes(2);
  f.dispose();
  expect(f.disconnect).toHaveBeenCalled();
  window.dispatchEvent(new Event("yo:present"));
  expect(f.animate).toHaveBeenCalledTimes(2);
});
it("reduced motion preserves an immediate readable workspace", () => {
  const f = fixture(true);
  window.dispatchEvent(new Event("yo:present"));
  expect(f.animate).not.toHaveBeenCalled();
  f.dispose();
  expect(f.remove).toHaveBeenCalled();
});
it("resizing interrupts an opening transform before measuring the new layout", () => {
  const f = fixture();
  window.dispatchEvent(new Event("yo:present"));
  f.resize();
  expect(f.cancel).toHaveBeenCalled();
  f.dispose();
});
