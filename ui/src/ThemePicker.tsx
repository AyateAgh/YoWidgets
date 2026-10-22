import { useEffect, useRef, useState } from "react";
import { CheckCircle2, Plus, X } from "lucide-react";
import type { Appearance } from "./types";
import { Button, Modal } from "./primitives";
import { appearanceOrigin, fromHsv, toHsv, validColor } from "./appearance";
import { native, rpc } from "./bridge";
const sound = () => {
  if (native) void rpc("app.sound", { sound: "theme" }).catch(() => {});
};
function ColorField({
  label,
  value,
  onChange,
}: {
  label: string;
  value: string;
  onChange: (v: string) => void;
}) {
  const [hsv, setHsv] = useState(() => toHsv(value));
  useEffect(() => {
    if (
      validColor(value) &&
      fromHsv(hsv.h, hsv.s, hsv.v) !== value.toUpperCase()
    )
      setHsv(toHsv(value));
  }, [value, hsv]);
  const change = (next: typeof hsv) => {
    setHsv(next);
    onChange(fromHsv(next.h, next.s, next.v));
  };
  const point = (event: React.PointerEvent<HTMLDivElement>) => {
    const rect = event.currentTarget.getBoundingClientRect();
    change({
      ...hsv,
      s: Math.max(0, Math.min(1, (event.clientX - rect.left) / rect.width)),
      v: 1 - Math.max(0, Math.min(1, (event.clientY - rect.top) / rect.height)),
    });
  };
  return (
    <div className="color-field">
      <span className="form-field-title">{label} color</span>
      <div
        className="color-plane"
        role="slider"
        tabIndex={0}
        aria-label={`${label} color saturation and brightness`}
        aria-valuemin={0}
        aria-valuemax={100}
        aria-valuenow={Math.round(hsv.s * 100)}
        aria-valuetext={value}
        style={{ backgroundColor: fromHsv(hsv.h, 1, 1) }}
        onPointerDown={(e) => {
          e.preventDefault();
          e.currentTarget.focus();
          e.currentTarget.setPointerCapture(e.pointerId);
          point(e);
        }}
        onPointerMove={(e) => {
          if (e.currentTarget.hasPointerCapture(e.pointerId)) point(e);
        }}
        onKeyDown={(e) => {
          if (
            !["ArrowLeft", "ArrowRight", "ArrowUp", "ArrowDown"].includes(e.key)
          )
            return;
          e.preventDefault();
          const step = e.shiftKey ? 0.1 : 0.01;
          change({
            ...hsv,
            s: Math.max(
              0,
              Math.min(
                1,
                hsv.s +
                  (e.key === "ArrowRight"
                    ? step
                    : e.key === "ArrowLeft"
                      ? -step
                      : 0),
              ),
            ),
            v: Math.max(
              0,
              Math.min(
                1,
                hsv.v +
                  (e.key === "ArrowUp"
                    ? step
                    : e.key === "ArrowDown"
                      ? -step
                      : 0),
              ),
            ),
          });
        }}
      >
        <i style={{ left: `${hsv.s * 100}%`, top: `${(1 - hsv.v) * 100}%` }} />
      </div>
      <input
        className="hue-slider"
        aria-label={`${label} color hue`}
        type="range"
        min="0"
        max="359"
        value={Math.round(hsv.h)}
        onChange={(e) => change({ ...hsv, h: Number(e.target.value) })}
      />
      <label className="color-hex">
        <span
          style={{ background: validColor(value) ? value : "transparent" }}
        />
        <input
          aria-label={`${label} color hex`}
          value={value}
          maxLength={7}
          spellCheck={false}
          onChange={(e) => onChange(e.target.value)}
          aria-invalid={!validColor(value) || undefined}
        />
      </label>
    </div>
  );
}
export function ThemePicker({
  value,
  onChange,
  onDirty,
}: {
  value: Appearance;
  onChange: (v: Appearance) => void;
  onDirty: (v: boolean) => void;
}) {
  const [open, setOpen] = useState(false);
  const [colors, setColors] = useState(value.colors);
  const tile = useRef<HTMLButtonElement>(null);
  const dirty = open && JSON.stringify(colors) !== JSON.stringify(value.colors);
  useEffect(() => {
    onDirty(dirty);
    return () => onDirty(false);
  }, [dirty, onDirty]);
  const choose = (mode: "dark" | "light", element: HTMLButtonElement) => {
    appearanceOrigin(element);
    if (value.mode !== mode) sound();
    onChange({ ...value, mode });
  };
  const finish = () => {
    if (!colors.every(validColor)) return;
    if (tile.current) appearanceOrigin(tile.current);
    if (
      value.mode !== "custom" ||
      JSON.stringify(value.colors) !== JSON.stringify(colors)
    )
      sound();
    onChange({
      ...value,
      mode: "custom",
      colors: colors.map((c) => c.toUpperCase()),
    });
    setOpen(false);
  };
  return (
    <>
      <div
        className="theme-options"
        role="radiogroup"
        aria-label="Theme"
        onKeyDown={(event) => {
          if (!["ArrowLeft", "ArrowRight", "Home", "End"].includes(event.key))
            return;
          event.preventDefault();
          const buttons = Array.from(
            event.currentTarget.querySelectorAll<HTMLButtonElement>(
              ":scope > button",
            ),
          );
          const index = buttons.indexOf(event.target as HTMLButtonElement);
          const next =
            event.key === "Home"
              ? 0
              : event.key === "End"
                ? 2
                : (index + (event.key === "ArrowRight" ? 1 : 2)) % 3;
          buttons[next]?.focus();
          buttons[next]?.click();
        }}
      >
        <button
          type="button"
          role="radio"
          aria-label="Dark theme"
          aria-checked={value.mode === "dark"}
          tabIndex={value.mode === "dark" ? 0 : -1}
          className={`theme-tile ${value.mode === "dark" ? "selected" : ""}`}
          onClick={(e) => choose("dark", e.currentTarget)}
        >
          <span className="theme-mini dark-mini">
            <i />
            <span>
              <b />
              <b />
              <b />
            </span>
          </span>
          <span>
            Dark {value.mode === "dark" && <CheckCircle2 size={16} />}
          </span>
        </button>
        <button
          type="button"
          role="radio"
          aria-label="Light theme"
          aria-checked={value.mode === "light"}
          tabIndex={value.mode === "light" ? 0 : -1}
          className={`theme-tile ${value.mode === "light" ? "selected" : ""}`}
          onClick={(e) => choose("light", e.currentTarget)}
        >
          <span className="theme-mini light-mini">
            <i />
            <span>
              <b />
              <b />
              <b />
            </span>
          </span>
          <span>
            Light {value.mode === "light" && <CheckCircle2 size={16} />}
          </span>
        </button>
        <button
          ref={tile}
          type="button"
          role="radio"
          aria-label="Create custom theme"
          aria-checked={value.mode === "custom"}
          tabIndex={value.mode === "custom" ? 0 : -1}
          aria-haspopup="dialog"
          className={`theme-tile ${value.mode === "custom" ? "selected" : ""}`}
          onClick={() => {
            setColors(value.colors);
            setOpen(true);
          }}
        >
          <span
            className={`theme-mini custom-mini ${value.mode === "custom" ? "has-color" : ""}`}
            style={
              value.mode === "custom"
                ? {
                    background: `linear-gradient(135deg, ${value.colors.join(", ")}, ${value.colors.at(-1)})`,
                  }
                : undefined
            }
          >
            <Plus size={23} />
          </span>
          <span>
            Custom {value.mode === "custom" && <CheckCircle2 size={16} />}
          </span>
        </button>
      </div>
      <Modal
        open={open}
        onClose={() => setOpen(false)}
        title="Your own theme"
        description="Choose a color. Add a second for a smooth gradient."
        className="theme-editor"
      >
        <div
          className="custom-theme-preview"
          style={{
            background: `linear-gradient(135deg, ${colors.map((c) => (validColor(c) ? c : "#8B9DC3")).join(", ")}, ${validColor(colors.at(-1)!) ? colors.at(-1) : "#8B9DC3"})`,
          }}
          aria-label="Custom theme color preview"
        >
          <span />
          <div>
            <i />
            <i />
            <i />
          </div>
        </div>
        <div className="color-fields">
          {colors.map((color, index) => (
            <ColorField
              key={index}
              label={index ? "Second" : "First"}
              value={color}
              onChange={(next) =>
                setColors((old) => old.map((c, i) => (i === index ? next : c)))
              }
            />
          ))}
        </div>
        <div className="gradient-action">
          {colors.length === 1 ? (
            <Button onClick={() => setColors([...colors, "#D8B9DB"])}>
              <Plus size={14} />
              Add second color
            </Button>
          ) : (
            <Button onClick={() => setColors([colors[0]])}>
              <X size={14} />
              Remove second color
            </Button>
          )}
          <span>Colors are balanced for readable text.</span>
        </div>
        <div className="dialog-actions">
          <Button onClick={() => setOpen(false)}>Cancel</Button>
          <Button
            variant="primary"
            disabled={!colors.every(validColor)}
            onClick={finish}
          >
            Apply theme
          </Button>
        </div>
      </Modal>
    </>
  );
}
