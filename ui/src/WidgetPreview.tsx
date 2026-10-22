import { useEffect, useRef, useState } from "react";
import {
  ArrowUpRight,
  Bell,
  Bluetooth,
  CalendarDays,
  CloudSun,
  Mic,
  Moon,
  Pause,
  Play,
  Plus,
  SkipBack,
  SkipForward,
  Sun,
  Volume2,
  Wifi,
} from "lucide-react";
import type { CatalogItem } from "./types";
export function WidgetPreview({
  item,
  large = false,
}: {
  item: CatalogItem;
  large?: boolean;
}) {
  const kind = item.kind;
  const root = useRef<HTMLDivElement>(null);
  const [wanted, setWanted] = useState(false);
  const [visible, setVisible] = useState(true);
  const [reduced, setReduced] = useState(false);
  const [second, setSecond] = useState(0);
  useEffect(() => {
    const element = root.current;
    if (!element) return;
    const host = element.closest(".catalog-card") ?? element;
    const enter = () => setWanted(true);
    const leave = () => setWanted(false);
    host.addEventListener("pointerenter", enter);
    host.addEventListener("pointerleave", leave);
    host.addEventListener("focusin", enter);
    host.addEventListener("focusout", leave);
    const media = window.matchMedia?.("(prefers-reduced-motion: reduce)");
    setReduced(media?.matches ?? false);
    const motion = () => setReduced(media?.matches ?? false);
    media?.addEventListener("change", motion);
    return () => {
      host.removeEventListener("pointerenter", enter);
      host.removeEventListener("pointerleave", leave);
      host.removeEventListener("focusin", enter);
      host.removeEventListener("focusout", leave);
      media?.removeEventListener("change", motion);
    };
  }, []);
  const live = wanted && visible && !reduced;
  useEffect(() => {
    if (!wanted) return;
    const pause = () => setWanted(false);
    document.addEventListener("visibilitychange", pause);
    window.addEventListener("yo:suspend", pause);
    const observer =
      typeof IntersectionObserver === "undefined"
        ? null
        : new IntersectionObserver((entries) =>
            setVisible(entries[0]?.isIntersecting ?? false),
          );
    if (root.current) observer?.observe(root.current);
    return () => {
      observer?.disconnect();
      document.removeEventListener("visibilitychange", pause);
      window.removeEventListener("yo:suspend", pause);
    };
  }, [wanted]);
  useEffect(() => {
    if (!live || kind !== "clock") return;
    const tick = () => setSecond(new Date().getSeconds());
    tick();
    const timer = setInterval(tick, 1000);
    return () => clearInterval(timer);
  }, [live, kind]);
  return (
    <div
      ref={root}
      aria-label={`${item.title} preview`}
      className={`widget-preview ${item.pack} ${live ? "is-live" : ""} ${large ? "large" : ""} kind-${kind} ${item.type === "pack" ? "collection-preview" : ""}`}
    >
      <div className="preview-grain" />
      <div className="landscape">
        <i />
        <i />
        <i />
      </div>
      {item.type === "pack" ? (
        <div className="preview-collection">
          <div className="sample-glass collection-clock">
            <small>WEDNESDAY, 21 OCTOBER</small>
            <b>09:41</b>
          </div>
          <div className="sample-glass collection-weather">
            <CloudSun size={27} />
            <b>21°</b>
            <small>Partly cloudy</small>
          </div>
          <div className="sample-glass collection-controls">
            <Wifi size={16} />
            <Volume2 size={16} />
            <Sun size={16} />
          </div>
        </div>
      ) : (
        <div className={`sample-glass sample-${kind}`}>
          {kind === "clock" && (
            <>
              <small>Wednesday, 21 October</small>
              <b className="clock-time">09:41</b>
              <span className="preview-seconds" aria-hidden="true">
                :{String(second).padStart(2, "0")}
              </span>
            </>
          )}
          {kind === "weather" && (
            <>
              <div className="sample-top">
                <span>Kyiv</span>
                <ArrowUpRight size={13} />
              </div>
              <div className="weather-middle">
                <b>21°</b>
                <CloudSun size={43} />
              </div>
              <small>Partly cloudy · H:23° L:16°</small>
            </>
          )}
          {kind === "calendar" && (
            <>
              <div className="sample-top">
                <span>October</span>
                <CalendarDays size={14} />
              </div>
              <div className="calendar-days">
                {"MTWTFSS".split("").map((d, i) => (
                  <small key={i}>{d}</small>
                ))}
                {Array.from({ length: 28 }, (_, i) => (
                  <span key={i} className={i === 20 ? "today" : ""}>
                    {i + 1}
                  </span>
                ))}
              </div>
            </>
          )}
          {kind === "music" && (
            <>
              <div className="music-track">
                <div className="album-art">
                  <span />
                </div>
                <div>
                  <b>In the moment</b>
                  <small>Your music, in view</small>
                </div>
              </div>
              <div className="mini-wave" aria-hidden="true">
                {[0, 1, 2, 3, 4].map((i) => (
                  <i key={i} style={{ animationDelay: `${i * -130}ms` }} />
                ))}
              </div>
              <div className="music-progress">
                <i />
              </div>
              <div className="sample-transport">
                <SkipBack size={16} />
                <Pause size={21} fill="currentColor" />
                <SkipForward size={16} />
              </div>
            </>
          )}
          {kind === "photo" && (
            <div className="photo-scene">
              <i />
              <i />
              <i />
              <span>Little moments</span>
            </div>
          )}
          {kind === "focus" && (
            <>
              <div className="focus-preview-heading"><span className="focus-moon"><Moon size={20}/></span><span><strong>Focus</strong><small>Notifications & calls</small></span><b className="focus-time">25:00</b></div>
              <div className="focus-preview-footer"><span>25 min</span><i><Play size={14}/></i></div>
            </>
          )}
          {kind === "quick" && (
            <>
              <div className="control-orbs">
                <span>
                  <Wifi size={19} />
                </span>
                <span>
                  <Bluetooth size={19} />
                </span>
                <span>
                  <Volume2 size={19} />
                </span>
              </div>
              <div className="sample-slider">
                <Volume2 size={13} />
                <i />
              </div>
              <div className="sample-slider">
                <Sun size={13} />
                <i />
              </div>
            </>
          )}
          {kind === "notifications" && (
            <>
              <div className="sample-top">
                <span>Notifications</span>
                <Bell size={15} />
              </div>
              <div className="notification-sample">
                <div className="notification-icon">
                  <CalendarDays size={15} />
                </div>
                <div>
                  <b>Your space, up to date</b>
                  <small>Updates appear here</small>
                </div>
              </div>
              <div className="notification-sample faint">
                <div className="notification-icon">
                  <Bell size={15} />
                </div>
                <div>
                  <i />
                  <i />
                </div>
              </div>
            </>
          )}
          {kind === "assistant" && (
            <>
              <Plus size={17} />
              <span>Ask anything</span>
              <Mic size={17} />
              <div className="send-orb">
                <ArrowUpRight size={16} />
              </div>
            </>
          )}
        </div>
      )}
    </div>
  );
}
