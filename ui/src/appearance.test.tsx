// @vitest-environment jsdom
import { it, expect, vi, afterEach } from "vitest";
import {
  render,
  screen,
  fireEvent,
  cleanup,
  waitFor,
  act,
} from "@testing-library/react";
import { SettingsView } from "./SettingsView";
import { previewState } from "./preview";
import type { Run } from "./types";
import {
  resolveAppearance,
  luminance,
  defaultAppearance,
  appearanceOrigin,
  applyAppearance,
  fromHsv,
  toHsv,
} from "./appearance";
afterEach(cleanup);
it("offers Light next to Dark and persists it without changing the accent", async () => {
  const run = vi.fn(async () => ({})) as unknown as Run;
  render(
    <SettingsView
      state={previewState}
      section="appearance"
      setSection={() => {}}
      run={run}
      onDirty={() => {}}
    />,
  );
  fireEvent.click(screen.getByRole("radio", { name: "Light theme" }));
  await waitFor(() =>
    expect(run).toHaveBeenCalledWith("settings.general", {
      appearance: { mode: "light", colors: ["#8B9DC3"], transparent: false },
    }),
  );
});
it("reviews a two-color custom gradient before applying and can remove the second color", async () => {
  const run = vi.fn(async () => ({})) as unknown as Run;
  render(
    <SettingsView
      state={previewState}
      section="appearance"
      setSection={() => {}}
      run={run}
      onDirty={() => {}}
    />,
  );
  fireEvent.click(screen.getByRole("radio", { name: "Create custom theme" }));
  fireEvent.click(screen.getByRole("button", { name: "Add second color" }));
  fireEvent.change(screen.getByRole("textbox", { name: "First color hex" }), {
    target: { value: "#FFFFFF" },
  });
  fireEvent.change(screen.getByRole("textbox", { name: "Second color hex" }), {
    target: { value: "#000000" },
  });
  expect(run).not.toHaveBeenCalled();
  fireEvent.click(screen.getByRole("button", { name: "Apply theme" }));
  await waitFor(() =>
    expect(run).toHaveBeenCalledWith("settings.general", {
      appearance: {
        mode: "custom",
        colors: ["#FFFFFF", "#000000"],
        transparent: false,
      },
    }),
  );
});
it("preserves the selected theme when toggling the central background", () => {
  const run = vi.fn(async () => ({})) as unknown as Run;
  render(
    <SettingsView
      state={previewState}
      section="appearance"
      setSection={() => {}}
      run={run}
      onDirty={() => {}}
    />,
  );
  fireEvent.click(
    screen.getByRole("switch", { name: "Transparent content background" }),
  );
  expect(run).toHaveBeenCalledWith("settings.general", {
    appearance: { mode: "dark", colors: ["#8B9DC3"], transparent: true },
  });
});
it("keeps body and secondary text readable at both ends of arbitrary custom gradients", () => {
  const contrast = (a: string, b: string) =>
    (Math.max(luminance(a), luminance(b)) + 0.05) /
    (Math.min(luminance(a), luminance(b)) + 0.05);
  for (const colors of [
    ["#000000", "#FFFFFF"],
    ["#FF0000", "#0000FF"],
    ["#00FF00", "#FFFF00"],
    ["#FFFFFF"],
    ["#000000"],
  ]) {
    const theme = resolveAppearance({
      ...defaultAppearance,
      mode: "custom",
      colors,
    });
    for (const bg of [theme.first, theme.second])
      for (const ink of ["--text", "--muted"])
        expect(contrast(theme.variables[ink], bg)).toBeGreaterThanOrEqual(4.5);
  }
});
it("does not interrupt a circular reveal when the same native appearance is acknowledged", async () => {
  await applyAppearance(defaultAppearance);
  const element = document.createElement("button");
  appearanceOrigin(element);
  let ready!: () => void;
  const skip = vi.fn();
  const transition = {
    ready: new Promise<void>((resolve) => (ready = resolve)),
    skipTransition: skip,
  };
  const start = vi.fn(() => transition);
  Object.defineProperty(document, "startViewTransition", {
    value: start,
    configurable: true,
  });
  Object.defineProperty(document.documentElement, "animate", {
    value: vi.fn(() => ({ finished: Promise.resolve(), cancel: () => {} })),
    configurable: true,
  });
  const value = { ...defaultAppearance, mode: "light" as const };
  const pending = applyAppearance(value);
  await applyAppearance({ ...value, colors: [...value.colors] });
  expect(start).toHaveBeenCalledTimes(1);
  expect(skip).not.toHaveBeenCalled();
  ready();
  await pending;
  delete (document as any).startViewTransition;
});
it("releases an acknowledged appearance draft so later native preferences can be shown", async () => {
  const run = vi.fn(async () => ({})) as unknown as Run;
  const props = {
    section: "appearance",
    setSection: () => {},
    run,
    onDirty: () => {},
  };
  const view = render(<SettingsView state={previewState} {...props} />);
  await act(async () => {
    fireEvent.click(screen.getByRole("radio", { name: "Light theme" }));
    await Promise.resolve();
    await Promise.resolve();
  });
  view.rerender(
    <SettingsView
      state={{
        ...previewState,
        settings: {
          ...previewState.settings,
          appearance: { ...defaultAppearance, mode: "light" },
        },
      }}
      {...props}
    />,
  );
  view.rerender(
    <SettingsView
      state={{
        ...previewState,
        settings: {
          ...previewState.settings,
          appearance: { ...defaultAppearance },
        },
      }}
      {...props}
    />,
  );
  expect(
    screen
      .getByRole("radio", { name: "Dark theme" })
      .getAttribute("aria-checked"),
  ).toBe("true");
});
it("round-trips full color picker primaries", () => {
  for (const color of [
    "#FF0000",
    "#00FF00",
    "#0000FF",
    "#FFFFFF",
    "#000000",
    "#8B9DC3",
  ]) {
    const hsv = toHsv(color);
    expect(fromHsv(hsv.h, hsv.s, hsv.v)).toBe(color);
  }
});
