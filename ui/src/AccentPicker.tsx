import { Check } from "lucide-react";
import palette from "../../design/accents.json";
import { ToggleRow } from "./primitives";
const names: Record<string, string> = {
  neutral: "Neutral",
  cyan: "Cyan",
  emerald: "Emerald",
  violet: "Violet",
  amber: "Amber",
  rose: "Rose",
  blue: "Blue",
  coral: "Coral",
};
export function AccentPicker({
  mode,
  onChange,
}: {
  mode: string;
  onChange: (mode: string) => void;
}) {
  const entries = Object.entries(palette);
  return (
    <>
      <ToggleRow
        label="Match Windows wallpaper"
        description="Extracts a subtle accent locally. Falls back to your Windows color."
        checked={mode === "auto"}
        onChange={(enabled) => onChange(enabled ? "auto" : "neutral")}
      />
      <div className="accent-grid" role="radiogroup" aria-label="Accent color">
        {entries.map(([id, color], index) => (
          <button
            key={id}
            type="button"
            role="radio"
            aria-checked={mode === id}
            aria-label={names[id]}
            tabIndex={mode === id || (mode === "auto" && index === 0) ? 0 : -1}
            onClick={() => onChange(id)}
            onKeyDown={(event) => {
              const step = ["ArrowRight", "ArrowDown"].includes(event.key)
                ? 1
                : ["ArrowLeft", "ArrowUp"].includes(event.key)
                  ? -1
                  : 0;
              if (step) {
                event.preventDefault();
                const next =
                  entries[(index + step + entries.length) % entries.length][0];
                onChange(next);
                (
                  event.currentTarget.parentElement?.children[
                    (index + step + entries.length) % entries.length
                  ] as HTMLButtonElement
                )?.focus();
              }
            }}
          >
            <span className="accent-swatch" style={{ background: color }}>
              {mode === id && <Check size={16} />}
            </span>
            <span>{names[id]}</span>
          </button>
        ))}
      </div>
    </>
  );
}
