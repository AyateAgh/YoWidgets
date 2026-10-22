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
  const queued: FrameRequestCallback[] = [];
  vi.stubGlobal("requestAnimationFrame", (callback: FrameRequestCallback) => {
    queued.push(callback);
    return queued.length;
  });
  vi.stubGlobal("cancelAnimationFrame", vi.fn());
  const flush = () => {
    while (queued.length) queued.shift()!(0);
  };
  const main = document.createElement("main"),
    surface = document.createElement("div");
  surface.append(main);
  document.body.append(surface);
  const cancel = vi.fn(),
    animate = vi.fn(() => ({ cancel, finished: new Promise(() => {}) }));
  surface.animate = animate as unknown as typeof surface.animate;
  const dispose = workspaceMotion(main, surface);
  return {
    main,
    surface,
    flush,
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
  f.flush();
  window.dispatchEvent(new Event("yo:present"));
  f.flush();
  expect(f.animate).toHaveBeenCalledTimes(2);
  expect(f.cancel).toHaveBeenCalledTimes(1);
  window.dispatchEvent(new Event("yo:suspend"));
  expect(f.cancel).toHaveBeenCalledTimes(2);
  f.dispose();
  expect(f.disconnect).toHaveBeenCalled();
  window.dispatchEvent(new Event("yo:present"));
  f.flush();
  expect(f.animate).toHaveBeenCalledTimes(2);
});
it("reduced motion preserves an immediate readable workspace", () => {
  const f = fixture(true);
  window.dispatchEvent(new Event("yo:present"));
  f.flush();
  expect(f.animate).not.toHaveBeenCalled();
  f.dispose();
  expect(f.remove).toHaveBeenCalled();
});
it("resizing interrupts an opening transform before measuring the new layout", () => {
  const f = fixture();
  window.dispatchEvent(new Event("yo:present"));
  f.flush();
  Object.defineProperty(f.main, "clientWidth", {
    value: 200,
    configurable: true,
  });
  f.resize();
  expect(f.cancel).toHaveBeenCalled();
  f.dispose();
});
it("defers a hidden tray reveal until the browser is visible and painted", () => {
  const f = fixture();
  let hidden = true;
  vi.spyOn(document, "hidden", "get").mockImplementation(() => hidden);
  const frames: FrameRequestCallback[] = [];
  vi.stubGlobal("requestAnimationFrame", (callback: FrameRequestCallback) => {
    frames.push(callback);
    return frames.length;
  });
  vi.stubGlobal("cancelAnimationFrame", vi.fn());
  window.dispatchEvent(new Event("yo:present"));
  expect(f.animate).not.toHaveBeenCalled();
  hidden = false;
  document.dispatchEvent(new Event("visibilitychange"));
  while (frames.length) frames.shift()!(0);
  expect(f.animate).toHaveBeenCalledTimes(1);
  f.dispose();
  vi.restoreAllMocks();
});
it("native window resize owns motion without a second sideways card animation", () => {
  const f = fixture();
  const card = document.createElement("article");
  card.className = "catalog-card";
  Object.defineProperty(card, "offsetLeft", { configurable: true, value: 200 });
  f.main.append(card);
  f.resize();
  const cardAnimate = vi.fn(() => ({
    cancel: vi.fn(),
    finished: new Promise(() => {}),
  }));
  card.animate = cardAnimate as unknown as typeof card.animate;
  window.dispatchEvent(
    new CustomEvent("yo:window-transition", { detail: { active: true } }),
  );
  vi.stubGlobal("innerWidth", 700);
  Object.defineProperty(card, "offsetLeft", { configurable: true, value: 0 });
  f.resize();
  expect(cardAnimate).not.toHaveBeenCalled();
  window.dispatchEvent(
    new CustomEvent("yo:window-transition", { detail: { active: false } }),
  );
  f.dispose();
});
it("keeps the reveal armed across a requested-page layout change", () => {
  const f = fixture();
  window.dispatchEvent(new Event("yo:present"));
  window.dispatchEvent(new Event("yo:layout-reset"));
  f.flush();
  expect(f.animate).toHaveBeenCalledTimes(1);
  expect(f.surface.dataset.presentation).toBe("visible");
  f.dispose();
});
it("the initial observer callback cannot swallow the opening effect", () => {
  const f = fixture();
  window.dispatchEvent(new Event("yo:present"));
  f.flush();
  f.resize();
  expect(f.cancel).not.toHaveBeenCalled();
  f.dispose();
});
