import { useState } from "react";
import { Upload, PackageCheck } from "lucide-react";
import type { Run } from "./types";
import { Button, Modal } from "./primitives";
import { formatBytes, kindNames } from "./catalog";
type Choice = {
  cancelled?: boolean;
  ticket: string;
  bytes: number;
  items: { title: string; kind: string; pack: string; author: string }[];
};
export function ImportPackage({
  run,
  onStarted,
}: {
  run: Run;
  onStarted: () => void;
}) {
  const [choice, setChoice] = useState<Choice | null>(null);
  const [busy, setBusy] = useState(false);
  const pick = async () => {
    setBusy(true);
    try {
      const result = await run<Choice>("ui.import.pick");
      if (!result.cancelled) setChoice(result);
    } catch {
    } finally {
      setBusy(false);
    }
  };
  const install = async () => {
    if (!choice) return;
    setBusy(true);
    try {
      await run("ui.import.install", { ticket: choice.ticket });
      setChoice(null);
      onStarted();
    } catch {
    } finally {
      setBusy(false);
    }
  };
  return (
    <>
      <Button busy={busy && !choice} onClick={() => void pick()}>
        <Upload size={15} /> Import package
      </Button>
      <Modal
        open={Boolean(choice)}
        onClose={() => {
          if (!busy) setChoice(null);
        }}
        title="Import collection"
        description="Community package · Not verified by Yo"
      >
        <div className="import-summary">
          <PackageCheck size={24} />
          <span>
            {choice?.items.length}{" "}
            {choice?.items.length === 1 ? "widget" : "widgets"} ·{" "}
            {formatBytes(choice?.bytes ?? 0)}
          </span>
        </div>
        <div className="import-items">
          {choice?.items.map((item, index) => (
            <div key={index}>
              <b>{item.title}</b>
              <span>
                {item.pack} · {kindNames[item.kind]} · {item.author}
              </span>
            </div>
          ))}
        </div>
        <p className="privacy-copy">
          Adds compatible widget resources to your local Library. Nothing is
          added to the desktop until you turn it on.
        </p>
        <div className="dialog-actions">
          <Button disabled={busy} onClick={() => setChoice(null)}>
            Cancel
          </Button>
          <Button variant="primary" busy={busy} onClick={() => void install()}>
            Import to Library
          </Button>
        </div>
      </Modal>
    </>
  );
}
