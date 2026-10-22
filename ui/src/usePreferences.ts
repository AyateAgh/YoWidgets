import { useEffect, useSyncExternalStore } from "react";
import { native, rpc } from "./bridge";
import type { Run, Settings } from "./types";
const samePreference = (a: unknown, b: unknown) =>
  a === b ||
  (typeof a === "object" &&
    a !== null &&
    typeof b === "object" &&
    b !== null &&
    JSON.stringify(a) === JSON.stringify(b));

// The application owns pending writes, so leaving Settings cannot fork a queue.
class Preferences {
  server: Settings;
  snapshot: {
    drafts: Record<string, unknown>;
    pending: Record<string, boolean>;
  } = { drafts: {}, pending: {} };
  listeners = new Set<() => void>();
  desired = new Map<string, unknown>();
  running = new Set<string>();
  constructor(
    readonly run: Run,
    settings: Settings,
  ) {
    this.server = settings;
  }
  subscribe = (listener: () => void) => {
    this.listeners.add(listener);
    return () => {
      this.listeners.delete(listener);
    };
  };
  read = () => this.snapshot;
  emit() {
    this.snapshot = {
      drafts: { ...this.snapshot.drafts },
      pending: { ...this.snapshot.pending },
    };
    this.listeners.forEach((listener) => listener());
  }
  acknowledge(settings: Settings) {
    this.server = settings;
    let changed = false;
    for (const key of Object.keys(this.snapshot.drafts))
      if (
        !this.running.has(key) &&
        samePreference(
          this.snapshot.drafts[key],
          settings[key as keyof Settings],
        )
      ) {
        delete this.snapshot.drafts[key];
        changed = true;
      }
    if (changed) this.emit();
  }
  save = async (key: string, value: unknown) => {
    this.desired.set(key, value);
    this.snapshot.drafts[key] = value;
    if (this.running.has(key)) {
      this.emit();
      return;
    }
    if (!this.running.size && native)
      void rpc("ui.saving", { saving: true }).catch(() => {});
    this.running.add(key);
    this.snapshot.pending[key] = true;
    this.emit();
    try {
      while (this.desired.has(key)) {
        const next = this.desired.get(key);
        this.desired.delete(key);
        try {
          await this.run("settings.general", { [key]: next });
        } catch {
          if (!this.desired.has(key)) delete this.snapshot.drafts[key];
        }
      }
    } finally {
      this.running.delete(key);
      delete this.snapshot.pending[key];
      if (
        samePreference(
          this.snapshot.drafts[key],
          this.server[key as keyof Settings],
        )
      )
        delete this.snapshot.drafts[key];
      this.emit();
      if (!this.running.size && native)
        void rpc("ui.saving", { saving: false }).catch(() => {});
    }
  };
}
const stores = new WeakMap<Run, Preferences>();
export function usePreferences(settings: Settings, run: Run) {
  let store = stores.get(run);
  if (!store) {
    store = new Preferences(run, settings);
    stores.set(run, store);
  }
  const { drafts, pending } = useSyncExternalStore(store.subscribe, store.read);
  useEffect(() => store.acknowledge(settings), [store, settings]);
  return {
    value: { ...settings, ...drafts } as Settings,
    save: store.save,
    pending,
  };
}
