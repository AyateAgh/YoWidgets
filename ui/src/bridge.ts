type HostEvent = {
  id?: string;
  ok?: boolean;
  result?: unknown;
  error?: string;
  type?: string;
  state?: unknown;
};
type WebView = {
  postMessage: (data: unknown) => void;
  addEventListener: (
    name: "message",
    callback: (event: { data: HostEvent }) => void,
  ) => void;
};
declare global {
  interface Window {
    chrome?: { webview?: WebView };
  }
}
const waiting = new Map<
  string,
  {
    resolve: (value: unknown) => void;
    reject: (reason: Error) => void;
    timer?: ReturnType<typeof setTimeout>;
  }
>();
const listeners = new Set<(event: HostEvent) => void>();
let sequence = 0;
window.chrome?.webview?.addEventListener("message", ({ data }) => {
  if (data.id && waiting.has(data.id)) {
    const pending = waiting.get(data.id)!;
    waiting.delete(data.id);
    clearTimeout(pending.timer);
    if (data.ok) pending.resolve(data.result);
    else pending.reject(new Error(data.error ?? "This action failed."));
  } else listeners.forEach((listener) => listener(data));
});
export const native = Boolean(window.chrome?.webview);
export const preview =
  new URLSearchParams(location.search).get("preview") === "1";
export function rpc<T = Record<string, unknown>>(
  method: string,
  args: Record<string, unknown> = {},
): Promise<T> {
  if (!native)
    return Promise.reject(
      new Error("Open the desktop app to use this action."),
    );
  return new Promise((resolve, reject) => {
    const id = String(++sequence);
    const timer =
      method === "ui.import.pick"
        ? undefined
        : setTimeout(() => {
            waiting.delete(id);
            reject(
              new Error(
                "This action took too long. Check the desktop app and retry.",
              ),
            );
          }, 35000);
    waiting.set(id, { resolve: (value) => resolve(value as T), reject, timer });
    window.chrome!.webview!.postMessage({ id, method, args });
  });
}
export const onHost = (listener: (event: HostEvent) => void) => {
  listeners.add(listener);
  return () => {
    listeners.delete(listener);
  };
};
window.addEventListener("yo:suspend", () => {
  for (const entry of waiting.values()) {
    clearTimeout(entry.timer);
    entry.reject(new Error("The interface was paused. Reopen it to continue."));
  }
  waiting.clear();
  listeners.forEach((listener) => listener({ type: "suspend" }));
});
