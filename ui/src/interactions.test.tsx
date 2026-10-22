// @vitest-environment jsdom
import { afterEach, describe, it, expect, vi } from "vitest";
import {
  cleanup,
  render,
  screen,
  fireEvent,
  waitFor,
  act,
} from "@testing-library/react";
import { SettingsView } from "./SettingsView";
import { DownloadsView } from "./DownloadsView";
import { previewState } from "./preview";
import type { Run } from "./types";
afterEach(cleanup);
describe("stable management interactions", () => {
  it("moves a settings switch immediately without disabling unrelated rows", async () => {
    let complete!: () => void;
    const run = vi.fn(
      () => new Promise<void>((r) => (complete = r)),
    ) as unknown as Run;
    render(
      <SettingsView
        state={previewState}
        section="general"
        setSection={() => {}}
        run={run}
        onDirty={() => {}}
      />,
    );
    const control = screen.getByRole("switch", {
      name: "Play interface sounds",
    });
    fireEvent.click(control);
    expect(control.getAttribute("aria-checked")).toBe("true");
    expect(
      (
        screen.getByRole("switch", {
          name: "Launch at Windows sign-in",
        }) as HTMLButtonElement
      ).disabled,
    ).toBe(false);
    complete();
    await waitFor(() =>
      expect(control.getAttribute("aria-busy")).not.toBe("true"),
    );
  });
  it("rolls back an unsuccessful optimistic toggle", async () => {
    const run = vi.fn(async () => {
      throw new Error("Disk unavailable");
    }) as unknown as Run;
    render(
      <SettingsView
        state={previewState}
        section="general"
        setSection={() => {}}
        run={run}
        onDirty={() => {}}
      />,
    );
    const control = screen.getByRole("switch", {
      name: "Play interface sounds",
    });
    fireEvent.click(control);
    await waitFor(() =>
      expect(control.getAttribute("aria-checked")).toBe("false"),
    );
  });
  it("uses a Library destination for completed downloads and clears history in one action", () => {
    const state = {
      ...previewState,
      jobs: [
        {
          id: "done",
          itemId: "glass-clock",
          title: "Clock",
          stage: "completed",
          downloaded: 1,
          total: 1,
          error: "",
          startedAt: "",
        },
      ],
    };
    const run = vi.fn(async () => ({})) as unknown as Run;
    const library = vi.fn();
    render(
      <DownloadsView
        state={state}
        run={run}
        onBrowse={() => {}}
        onLibrary={library}
      />,
    );
    fireEvent.click(
      screen.getByRole("button", { name: "Show Clock in Library" }),
    );
    expect(library).toHaveBeenCalledOnce();
    fireEvent.click(screen.getByRole("button", { name: "Clear finished" }));
    expect(run).toHaveBeenCalledWith("content.clearFinished");
    expect(
      screen.queryByRole("button", { name: "Dismiss Clock download" }),
    ).toBeNull();
  });
});

it("serializes preference intents across settings unmount and remount", async () => {
  const calls: number[] = [];
  const releases: (() => void)[] = [];
  const run = vi.fn((_method: string, args: Record<string, unknown>) => {
    calls.push(args.interfaceVolume as number);
    return new Promise<void>((resolve) => releases.push(resolve));
  }) as unknown as Run;
  const show = () =>
    render(
      <SettingsView
        state={previewState}
        section="general"
        setSection={() => {}}
        run={run}
        onDirty={() => {}}
      />,
    );
  const first = show();
  const slide = screen.getByRole("slider", { name: "Interface sound volume" });
  fireEvent.change(slide, { target: { value: "20" } });
  fireEvent.pointerUp(slide);
  fireEvent.change(slide, { target: { value: "40" } });
  fireEvent.pointerUp(slide);
  first.unmount();
  show();
  const again = screen.getByRole("slider", { name: "Interface sound volume" });
  fireEvent.change(again, { target: { value: "80" } });
  fireEvent.pointerUp(again);
  await act(async () => {
    releases[0]();
    await Promise.resolve();
    await Promise.resolve();
  });
  expect(calls).toEqual([0.2, 0.8]);
  await act(async () => {
    for (const release of releases.slice(1)) release();
  });
});
it("fills an approximate city draft only after the explicit location action", async () => {
  const run = vi.fn(async (method: string) =>
    method === "settings.weather.locate" ? { city: "Lviv" } : {},
  ) as unknown as Run;
  render(
    <SettingsView
      state={previewState}
      section="widgets"
      setSection={() => {}}
      run={run}
      onDirty={() => {}}
    />,
  );
  expect(run).not.toHaveBeenCalled();
  fireEvent.click(
    screen.getByRole("button", {
      name: "Find my city using approximate IP location",
    }),
  );
  await waitFor(() =>
    expect(
      (screen.getByRole("textbox", { name: "City" }) as HTMLInputElement).value,
    ).toBe("Lviv"),
  );
  expect(run).toHaveBeenCalledTimes(1);
  expect(run).toHaveBeenCalledWith("settings.weather.locate");
  fireEvent.click(screen.getByRole("button", { name: "Save & refresh" }));
  await waitFor(() =>
    expect(run).toHaveBeenCalledWith(
      "settings.general",
      { city: "Lviv" },
      "City saved",
    ),
  );
});
it("retries the native download job so local imports retain their source", () => {
  const run = vi.fn(async () => ({})) as unknown as Run;
  const state = {
    ...previewState,
    jobs: [
      {
        id: "local-job",
        itemId: "import-local",
        title: "Custom collection",
        stage: "failed",
        downloaded: 0,
        total: 0,
        error: "Cannot read archive",
        startedAt: "",
      },
    ],
  };
  render(
    <DownloadsView
      state={state}
      run={run}
      onBrowse={() => {}}
      onLibrary={() => {}}
    />,
  );
  fireEvent.click(screen.getByRole("button", { name: "Retry" }));
  expect(run).toHaveBeenCalledWith("content.retry", { id: "local-job" });
});
