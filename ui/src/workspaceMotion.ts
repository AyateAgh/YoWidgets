// Native window geometry and content reveal each have a single motion owner.
export function workspaceMotion(main: HTMLElement, surface: HTMLElement) {
  const application = surface.closest<HTMLElement>(".application") ?? surface;
  const reduced = window.matchMedia("(prefers-reduced-motion: reduce)");
  const animations = new Set<Animation>();
  let opening: Animation | undefined,
    pending = false,
    nativeResize = false;
  let frame = 0,
    disposed = false;
  let settleFrame = 0,
    endingResize = false;
  const cancelFrame = () => {
    if (frame) cancelAnimationFrame(frame);
    frame = 0;
  };
  const stopAnimations = () => {
    for (const animation of animations) animation.cancel();
    animations.clear();
    opening = undefined;
  };
  const stop = () => {
    pending = false;
    cancelFrame();
    stopAnimations();
  };
  const play = (element: HTMLElement, frames: Keyframe[], duration: number) => {
    if (reduced.matches || document.hidden || !element.animate) return;
    const animation = element.animate(frames, {
      duration,
      easing: "cubic-bezier(.16,1,.3,1)",
    });
    animations.add(animation);
    void animation.finished
      .catch(() => {})
      .finally(() => animations.delete(animation));
    return animation;
  };
  const prepare = () => {
    stop();
    application.dataset.presentation = reduced.matches ? "visible" : "prepared";
  };
  const paint = () => {
    if (!pending || disposed || document.hidden) return;
    // Two paint opportunities after visibility: WebView's native Show precedes visibilitychange.
    frame = requestAnimationFrame(() => {
      frame = requestAnimationFrame(() => {
        frame = 0;
        if (!pending || disposed || document.hidden) return;
        pending = false;
        application.dataset.presentation = "visible";
        opening = play(
          application,
          [
            { opacity: 0, transform: "translateY(12px) scale(.965)" },
            { opacity: 1, transform: "translateY(0) scale(1)" },
          ],
          280,
        );
      });
    });
  };
  const reveal = () => {
    prepare();
    pending = true;
    if (reduced.matches) {
      pending = false;
      application.dataset.presentation = "visible";
      return;
    }
    paint();
  };
  const bucket = () =>
    innerWidth <= 760 ? 0 : innerWidth <= 1050 ? 1 : innerWidth < 1450 ? 2 : 3;
  let previousBucket = bucket();
  let boxes = new Map<HTMLElement, { left: number; top: number }>();
  const measure = () => {
    const next = new Map<HTMLElement, { left: number; top: number }>();
    for (const element of Array.from(
      main.querySelectorAll<HTMLElement>(
        ".catalog-card,.settings-section,.settings-nav,.page-header",
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
  let width = main.clientWidth,
    height = main.clientHeight;
  const finishNativeResize = () => {
    if (settleFrame) cancelAnimationFrame(settleFrame);
    settleFrame = requestAnimationFrame(() => {
      settleFrame = requestAnimationFrame(() => {
        settleFrame = 0;
        if (disposed || !endingResize) return;
        boxes = measure();
        previousBucket = bucket();
        nativeResize = false;
        endingResize = false;
      });
    });
  };
  const resize = () => {
    const changed = width !== main.clientWidth || height !== main.clientHeight;
    width = main.clientWidth;
    height = main.clientHeight;
    // ResizeObserver's initial callback is not a resize and must not eat the opening effect.
    if (changed && opening) {
      opening.cancel();
      opening = undefined;
    }
    const next = measure(),
      currentBucket = bucket();
    if (currentBucket !== previousBucket && !nativeResize && !pending) {
      stopAnimations();
      for (const [element, box] of next) {
        const before = boxes.get(element);
        if (!before) continue;
        const x = before.left - box.left,
          y = before.top - box.top;
        const rect = element.getBoundingClientRect();
        if (rect.bottom < 0 || rect.top > innerHeight || Math.hypot(x, y) < 8)
          continue;
        play(
          element,
          [
            { transform: `translate(${x}px,${y}px)`, opacity: 0.7 },
            { transform: "translate(0,0)", opacity: 1 },
          ],
          180,
        );
      }
    }
    previousBucket = currentBucket;
    boxes = next;
    if (changed && nativeResize && endingResize) finishNativeResize();
  };
  const transition = (event: Event) => {
    if ((event as CustomEvent<{ active: boolean }>).detail?.active === true) {
      if (settleFrame) cancelAnimationFrame(settleFrame);
      settleFrame = 0;
      endingResize = false;
      nativeResize = true;
      stop();
      application.dataset.presentation = "visible";
      boxes = measure();
      previousBucket = bucket();
    } else if (nativeResize) {
      endingResize = true;
      finishNativeResize();
    }
  };
  const visibility = () => {
    if (document.hidden) {
      cancelFrame();
      stopAnimations();
    } else if (pending) paint();
  };
  const motionChanged = () => {
    stop();
    application.dataset.presentation = "visible";
  };
  const resetLayout = () => {
    boxes = measure();
    previousBucket = bucket();
  };
  if (!application.dataset.presentation)
    application.dataset.presentation = "prepared";
  boxes = measure();
  const observer = new ResizeObserver(resize);
  observer.observe(main);
  window.addEventListener("yo:present", reveal);
  window.addEventListener("yo:prepare-present", prepare);
  window.addEventListener("yo:suspend", prepare);
  window.addEventListener("yo:window-transition", transition);
  window.addEventListener("yo:layout-reset", resetLayout);
  document.addEventListener("visibilitychange", visibility);
  reduced.addEventListener("change", motionChanged);
  return () => {
    disposed = true;
    if (settleFrame) cancelAnimationFrame(settleFrame);
    stop();
    observer.disconnect();
    window.removeEventListener("yo:present", reveal);
    window.removeEventListener("yo:prepare-present", prepare);
    window.removeEventListener("yo:suspend", prepare);
    window.removeEventListener("yo:window-transition", transition);
    window.removeEventListener("yo:layout-reset", resetLayout);
    document.removeEventListener("visibilitychange", visibility);
    reduced.removeEventListener("change", motionChanged);
  };
}
