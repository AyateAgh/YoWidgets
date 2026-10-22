import {
  ArrowDownToLine,
  ArrowRight,
  Check,
  Clock3,
  Download,
  RotateCcw,
  X,
} from "lucide-react";
import type { Run, Snapshot } from "./types";
import { formatBytes, packName } from "./catalog";
import { Button, Empty } from "./primitives";
import { WidgetPreview } from "./WidgetPreview";
export function DownloadsView({
  state,
  run,
  onBrowse,
  onLibrary,
}: {
  state: Snapshot;
  run: Run;
  onBrowse: () => void;
  onLibrary: () => void;
}) {
  const active = state.jobs.filter(
    (j) => !["completed", "failed", "cancelled"].includes(j.stage),
  );
  return (
    <>
      <header className="page-header">
        <div className="heading-group">
          <h1>Downloads</h1>
          {active.length > 0 && (
            <span className="item-count">{active.length} in progress</span>
          )}
        </div>
        <div className="header-tools">
          {state.jobs.some((j) =>
            ["completed", "failed", "cancelled"].includes(j.stage),
          ) && (
            <Button
              onClick={() => void run("content.clearFinished").catch(() => {})}
            >
              Clear finished
            </Button>
          )}
          <Button onClick={onBrowse}>Browse catalog</Button>
        </div>
      </header>
      {!state.jobs.length ? (
        <Empty
          icon={<ArrowDownToLine size={30} />}
          title="All caught up"
          description="Your downloads will appear here. Installed widgets live in your Library."
          action={<Button onClick={onBrowse}>Explore widgets</Button>}
        />
      ) : (
        <div className="downloads-list">
          {state.jobs.map((job) => {
            const item = state.catalog.items.find((i) => i.id === job.itemId);
            const busy = !["completed", "failed", "cancelled"].includes(
              job.stage,
            );
            const percent = job.total
              ? Math.min(100, Math.floor((job.downloaded / job.total) * 100))
              : 0;
            return (
              <article
                className={`download-row ${job.stage === "failed" ? "failed" : ""}`}
                key={job.id}
              >
                <div className="download-cover">
                  {item ? (
                    <WidgetPreview item={item} />
                  ) : (
                    <Download size={22} />
                  )}
                </div>
                <div className="download-body">
                  <div className="download-heading">
                    <h2>{job.title}</h2>
                    <span>{item ? packName(item.pack) : ""}</span>
                  </div>
                  <div className="download-status">
                    {job.stage === "completed" ? (
                      <>
                        <Check size={14} /> Ready in Library
                      </>
                    ) : job.stage === "failed" ? (
                      job.error
                    ) : job.stage === "cancelled" ? (
                      "Cancelled"
                    ) : job.stage === "queued" ? (
                      <>
                        <Clock3 size={14} /> Queued
                      </>
                    ) : job.stage === "verifying" ? (
                      "Verifying package"
                    ) : job.stage === "installing" ? (
                      "Installing"
                    ) : (
                      `${formatBytes(job.downloaded)} of ${formatBytes(job.total)}`
                    )}
                  </div>
                  {busy && job.total > 0 && (
                    <div
                      className="progress-track"
                      role="progressbar"
                      aria-label={`${job.title} download`}
                      aria-valuemin={0}
                      aria-valuemax={100}
                      aria-valuenow={percent}
                    >
                      <i style={{ width: `${percent}%` }} />
                    </div>
                  )}
                </div>
                <div className="download-actions">
                  {busy ? (
                    <Button
                      onClick={() =>
                        void run("content.cancel", { id: job.id }).catch(
                          () => {},
                        )
                      }
                    >
                      <X size={14} /> Cancel
                    </Button>
                  ) : (
                    <>
                      {job.stage !== "completed" && (
                        <Button
                          onClick={() =>
                            void run("content.retry", {
                              id: job.id,
                            }).catch(() => {})
                          }
                        >
                          <RotateCcw size={14} /> Retry
                        </Button>
                      )}
                      {job.stage === "completed" && (
                        <button
                          className="icon-button"
                          aria-label={`Show ${job.title} in Library`}
                          onClick={onLibrary}
                        >
                          <ArrowRight size={18} />
                        </button>
                      )}
                    </>
                  )}
                </div>
              </article>
            );
          })}
        </div>
      )}
    </>
  );
}
