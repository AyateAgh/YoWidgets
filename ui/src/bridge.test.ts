// @vitest-environment jsdom
import { it, expect, vi } from "vitest";
it("lets the native file picker stay open beyond normal RPC timeouts", async () => {
  vi.useFakeTimers();
  vi.resetModules();
  let message: (event: { data: any }) => void = () => {};
  let request: any;
  window.chrome = {
    webview: {
      postMessage: (value) => {
        request = value;
      },
      addEventListener: (_name, listener) => {
        message = listener;
      },
    },
  };
  try {
    const { rpc } = await import("./bridge");
    let settled = false;
    const pending = rpc("ui.import.pick").then(
      (value) => {
        settled = true;
        return value;
      },
      () => {
        settled = true;
        return null;
      },
    );
    await vi.advanceTimersByTimeAsync(60000);
    expect(settled).toBe(false);
    message({
      data: { id: request.id, ok: true, result: { cancelled: true } },
    });
    expect(await pending).toEqual({ cancelled: true });
  } finally {
    delete window.chrome;
    vi.useRealTimers();
  }
});
