// One animation owner for reopening and responsive rearrangement; no idle render loop.
export function workspaceMotion(main: HTMLElement, surface: HTMLElement) {
  const reduced = window.matchMedia("(prefers-reduced-motion: reduce)");
  const animations = new Set<Animation>();
  let opening: Animation | undefined;
  const stop = () => {
    for (const animation of animations) animation.cancel();
    animations.clear();
  };
  const play = (element: HTMLElement, frames: Keyframe[], duration: number) => {
    if (reduced.matches || document.hidden || !element.animate) return;
    const animation = element.animate(frames, {
      duration,
      easing: "cubic-bezier(.18,.78,.24,1)",
    });
    animations.add(animation);
    void animation.finished
      .catch(() => {})
      .finally(() => animations.delete(animation));
    return animation;
  };
  const reveal = () => {
    stop();
    opening = play(
      surface,
      [
        { opacity: 0.35, transform: "translateY(9px) scale(.985)" },
        { opacity: 1, transform: "translateY(0) scale(1)" },
      ],
      300,
    );
  };
  const bucket = () =>
    innerWidth <= 760 ? 0 : innerWidth <= 1050 ? 1 : innerWidth < 1450 ? 2 : 3;
  let previousBucket = bucket();
  let boxes = new Map<HTMLElement, { left: number; top: number }>();
  const measure = () => {
    const next = new Map<HTMLElement, { left: number; top: number }>();
    for (const element of Array.from(
      main.querySelectorAll<HTMLElement>(
        ".catalog-card,.pack-card,.settings-section,.settings-nav,.page-header",
      ),
    ).slice(0, 40)) {
      let left = 0,
        top = 0;
      for (
        let parent: HTMLElement | null = element;
        parent;
        parent = parent.offsetParent as HTMLElement | null
      ) {
        left += parent.offsetLeft;
        top += parent.offsetTop;
      }
      next.set(element, { left, top });
    }
    return next;
  };
  const resize = () => {
    // Cancel the reveal before measuring: its scale must never affect layout coordinates.
    opening?.cancel();
    opening = undefined;
    const next = measure();
    const currentBucket = bucket();
    if (currentBucket !== previousBucket) {
      stop();
      for (const [element, box] of next) {
        const before = boxes.get(element);
        if (!before) continue;
        const x = before.left - box.left,
          y = before.top - box.top;
        const rect = element.getBoundingClientRect();
        if (rect.bottom < 0 || rect.top > innerHeight || Math.hypot(x, y) < 8)
          continue;
        // Position only: text and controls stay sharp while a grid changes column count.
        play(
          element,
          [
            { transform: `translate(${x}px,${y}px)`, opacity: 0.7 },
            { transform: "translate(0,0)", opacity: 1 },
          ],
          220,
        );
      }
    }
    previousBucket = currentBucket;
    boxes = next;
  };
  boxes = measure();
  const observer = new ResizeObserver(resize);
  observer.observe(main);
  window.addEventListener("yo:present", reveal);
  window.addEventListener("yo:suspend", stop);
  const visibility = () => {
    if (document.hidden) stop();
  };
  document.addEventListener("visibilitychange", visibility);
  reduced.addEventListener("change", stop);
  return () => {
    stop();
    observer.disconnect();
    window.removeEventListener("yo:present", reveal);
    window.removeEventListener("yo:suspend", stop);
    document.removeEventListener("visibilitychange", visibility);
    reduced.removeEventListener("change", stop);
  };
}
