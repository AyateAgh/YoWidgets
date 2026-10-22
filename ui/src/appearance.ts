import dark from "../../design/tokens.json";
import light from "../../design/light-theme.json";
import type { Appearance } from "./types";
export const defaultAppearance: Appearance = {
  mode: "dark",
  colors: ["#8B9DC3"],
  transparent: false,
};
export const validColor = (color: string) => /^#[0-9a-f]{6}$/i.test(color);
const rgb = (color: string) =>
  [1, 3, 5].map((n) => parseInt(color.slice(n, n + 2), 16));
const hex = (values: number[]) =>
  "#" +
  values
    .map((v) => Math.round(v).toString(16).padStart(2, "0"))
    .join("")
    .toUpperCase();
const mix = (a: string, b: string, weight: number) =>
  hex(rgb(a).map((v, i) => v * (1 - weight) + rgb(b)[i] * weight));
export function luminance(color: string) {
  const values = rgb(color)
    .map((v) => v / 255)
    .map((v) => (v <= 0.04045 ? v / 12.92 : ((v + 0.055) / 1.055) ** 2.4));
  return values[0] * 0.2126 + values[1] * 0.7152 + values[2] * 0.0722;
}
export function resolveAppearance(value: Appearance) {
  const colors = value.colors?.filter(validColor).slice(0, 2);
  if (!colors?.length) colors?.push(defaultAppearance.colors[0]);
  const list = colors?.length ? colors : defaultAppearance.colors;
  const custom = value.mode === "custom";
  const isLight =
    value.mode === "light" ||
    (custom && list.reduce((n, c) => n + luminance(c), 0) / list.length > 0.35);
  const tokens: { [key: string]: string | number } = {
    ...(isLight ? light : dark),
  };
  const first = custom
    ? mix(String(tokens.background), list[0], 0.12)
    : String(tokens.background);
  const second = custom
    ? mix(String(tokens.background), list.at(-1)!, 0.12)
    : first;
  if (custom) {
    tokens.frame = mix(String(tokens.frame), list[0], 0.055);
    tokens.surface = mix(String(tokens.surface), list[0], 0.045);
    tokens.field = mix(String(tokens.field), list[0], 0.055);
  }
  const variables: Record<string, string> = {};
  for (const [key, entry] of Object.entries(tokens))
    if (typeof entry === "string")
      variables["--" + key.replace(/[A-Z]/g, (m) => "-" + m.toLowerCase())] =
        entry;
  variables["--page-paint"] = `linear-gradient(135deg, ${first}, ${second})`;
  variables["--ink-rgb"] = isLight ? "23 23 25" : "245 245 245";
  return { variables, isLight, first, second };
}
export function toHsv(color: string) {
  const [r, g, b] = rgb(validColor(color) ? color : "#8B9DC3").map(
    (v) => v / 255,
  );
  const max = Math.max(r, g, b),
    min = Math.min(r, g, b),
    d = max - min;
  let h = 0;
  if (d)
    h =
      max === r
        ? ((g - b) / d) % 6
        : max === g
          ? (b - r) / d + 2
          : (r - g) / d + 4;
  return { h: (h * 60 + 360) % 360, s: max ? d / max : 0, v: max };
}
export function fromHsv(h: number, s: number, v: number) {
  const c = v * s,
    x = c * (1 - Math.abs(((h / 60) % 2) - 1)),
    m = v - c;
  const values =
    h < 60
      ? [c, x, 0]
      : h < 120
        ? [x, c, 0]
        : h < 180
          ? [0, c, x]
          : h < 240
            ? [0, x, c]
            : h < 300
              ? [x, 0, c]
              : [c, 0, x];
  return hex(values.map((n) => (n + m) * 255));
}
let origin: { x: number; y: number } | undefined;
let current: Appearance | undefined;
let active: ViewTransition | undefined;
let generation = 0;
const stopReveal = () => active?.skipTransition();
window.addEventListener("yo:suspend", stopReveal);
window.addEventListener("resize", stopReveal);
window.addEventListener("yo:window-transition", stopReveal);
document.addEventListener("visibilitychange", () => {
  if (document.hidden) stopReveal();
});
window
  .matchMedia?.("(prefers-reduced-motion: reduce)")
  .addEventListener("change", (event) => {
    if (event.matches) stopReveal();
  });
export function appearanceOrigin(element: HTMLElement) {
  const box = element.getBoundingClientRect();
  origin = { x: box.x + box.width / 2, y: box.y + box.height / 2 };
}
export async function applyAppearance(value: Appearance) {
  if (current && JSON.stringify(current) === JSON.stringify(value)) {
    origin = undefined;
    return false;
  }
  const previous = current;
  const point = origin;
  origin = undefined;
  const stamp = ++generation;
  current = value;
  active?.skipTransition();
  const resolved = resolveAppearance(value);
  const changedTheme =
    previous &&
    JSON.stringify([previous.mode, previous.colors]) !==
      JSON.stringify([value.mode, value.colors]);
  const commit = () => {
    if (stamp !== generation) return;
    const root = document.documentElement;
    for (const [key, color] of Object.entries(resolved.variables))
      root.style.setProperty(key, color);
    root.dataset.theme = value.mode;
    root.dataset.tone = resolved.isLight ? "light" : "dark";
    root.dataset.panelClear = String(value.transparent);
    root.style.colorScheme = resolved.isLight ? "light" : "dark";
  };
  const reduced = window.matchMedia?.(
    "(prefers-reduced-motion: reduce)",
  ).matches;
  if (
    !point ||
    !changedTheme ||
    reduced ||
    document.hidden ||
    !document.startViewTransition
  ) {
    commit();
    return false;
  }
  const transition = document.startViewTransition(commit);
  active = transition;
  try {
    await transition.ready;
    if (stamp !== generation) return false;
    const radius = Math.hypot(
      Math.max(point.x, innerWidth - point.x),
      Math.max(point.y, innerHeight - point.y),
    );
    const animation = document.documentElement.animate(
      {
        clipPath: [
          `circle(0px at ${point.x}px ${point.y}px)`,
          `circle(${radius}px at ${point.x}px ${point.y}px)`,
        ],
      },
      {
        duration: 520,
        easing: "cubic-bezier(.2,.75,.2,1)",
        pseudoElement: "::view-transition-new(root)",
        fill: "both",
      },
    );
    await animation.finished;
    animation.cancel();
  } catch {
    commit();
  } finally {
    if (active === transition) active = undefined;
  }
  return true;
}
