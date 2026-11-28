import { useEffect, useRef, useState, type PointerEvent, type RefObject } from "react";
import "./FadeScrollbar.css";

interface FadeScrollbarProps {
  /** 被绑定的滚动容器 */
  targetRef: RefObject<HTMLElement | null>;
  /** 外部强制隐藏（例如 SplitPage 切页淡出期间） */
  hidden?: boolean;
}

const MIN_THUMB_HEIGHT = 28;

/**
 * 覆盖式细线滚动条：保持原生滚动体验，同时在内容溢出/恢复时平滑淡入淡出。
 * 滚动和尺寸变化直接更新 DOM 样式，避免滚动过程中触发 React 重渲染。
 */
export default function FadeScrollbar({ targetRef, hidden = false }: FadeScrollbarProps) {
  const trackRef = useRef<HTMLDivElement>(null);
  const thumbRef = useRef<HTMLDivElement>(null);
  const [overflows, setOverflows] = useState(false);
  const [dragging, setDragging] = useState(false);
  const dragState = useRef<{
    pointerId: number;
    startY: number;
    startScrollTop: number;
    maxScroll: number;
    maxThumbTop: number;
  } | null>(null);

  useEffect(() => {
    const el = targetRef.current;
    if (!el) return;

    let rafId = 0;

    const measure = () => {
      rafId = 0;
      const track = trackRef.current;
      const thumb = thumbRef.current;
      if (!track || !thumb) return;

      const clientHeight = el.clientHeight;
      const scrollHeight = el.scrollHeight;
      const maxScroll = scrollHeight - clientHeight;
      const canScroll = clientHeight > 0 && maxScroll > 1;

      setOverflows((prev) => (prev === canScroll ? prev : canScroll));

      // 内容不再溢出时保留上一次滑块几何尺寸，让滑块在原地平滑淡出而非先闪缩
      if (!canScroll) return;

      const trackHeight = track.clientHeight || Math.max(0, clientHeight - 4);
      const thumbHeight = Math.min(
        trackHeight,
        Math.max(MIN_THUMB_HEIGHT, Math.round((clientHeight / scrollHeight) * trackHeight)),
      );
      const maxThumbTop = Math.max(0, trackHeight - thumbHeight);
      const ratio = Math.min(1, Math.max(0, el.scrollTop / maxScroll));
      const thumbTop = ratio * maxThumbTop;

      thumb.style.height = `${thumbHeight}px`;
      thumb.style.transform = `translate3d(0, ${thumbTop}px, 0)`;
    };

    const schedule = () => {
      if (rafId !== 0) return;
      rafId = window.requestAnimationFrame(measure);
    };

    const observedChildren = new Set<Element>();
    const resizeObserver = new ResizeObserver(schedule);
    resizeObserver.observe(el);

    const syncChildren = () => {
      const current = new Set(Array.from(el.children));
      for (const child of observedChildren) {
        if (!current.has(child)) {
          resizeObserver.unobserve(child);
          observedChildren.delete(child);
        }
      }
      for (const child of current) {
        if (!observedChildren.has(child)) {
          resizeObserver.observe(child);
          observedChildren.add(child);
        }
      }
    };

    syncChildren();

    const mutationObserver = new MutationObserver(() => {
      syncChildren();
      schedule();
    });
    mutationObserver.observe(el, { childList: true, subtree: true });

    el.addEventListener("scroll", schedule, { passive: true });
    window.addEventListener("resize", schedule, { passive: true });
    schedule();

    return () => {
      if (rafId !== 0) window.cancelAnimationFrame(rafId);
      el.removeEventListener("scroll", schedule);
      window.removeEventListener("resize", schedule);
      mutationObserver.disconnect();
      resizeObserver.disconnect();
    };
  }, [targetRef]);

  const handleThumbPointerDown = (event: PointerEvent<HTMLDivElement>) => {
    const el = targetRef.current;
    const track = trackRef.current;
    const thumb = thumbRef.current;
    if (!el || !track || !thumb || event.button !== 0) return;

    const maxScroll = el.scrollHeight - el.clientHeight;
    const maxThumbTop = track.clientHeight - thumb.offsetHeight;
    if (maxScroll <= 0 || maxThumbTop <= 0) return;

    event.preventDefault();
    event.stopPropagation();
    thumb.setPointerCapture(event.pointerId);
    dragState.current = {
      pointerId: event.pointerId,
      startY: event.clientY,
      startScrollTop: el.scrollTop,
      maxScroll,
      maxThumbTop,
    };
    setDragging(true);
  };

  const handleThumbPointerMove = (event: PointerEvent<HTMLDivElement>) => {
    const state = dragState.current;
    const el = targetRef.current;
    if (!state || !el || state.pointerId !== event.pointerId) return;

    const deltaY = event.clientY - state.startY;
    const nextTop = state.startScrollTop + (deltaY / state.maxThumbTop) * state.maxScroll;
    el.scrollTop = Math.min(state.maxScroll, Math.max(0, nextTop));
  };

  const endDrag = (event: PointerEvent<HTMLDivElement>) => {
    const state = dragState.current;
    const thumb = thumbRef.current;
    if (!state || state.pointerId !== event.pointerId) return;

    if (thumb?.hasPointerCapture(event.pointerId)) {
      thumb.releasePointerCapture(event.pointerId);
    }
    dragState.current = null;
    setDragging(false);
  };

  const handleTrackPointerDown = (event: PointerEvent<HTMLDivElement>) => {
    const el = targetRef.current;
    const track = trackRef.current;
    const thumb = thumbRef.current;
    if (!el || !track || !thumb || event.button !== 0 || event.target === thumb) return;

    const maxScroll = el.scrollHeight - el.clientHeight;
    const thumbHeight = thumb.offsetHeight;
    const maxThumbTop = track.clientHeight - thumbHeight;
    if (maxScroll <= 0 || maxThumbTop <= 0) return;

    event.preventDefault();
    const rect = track.getBoundingClientRect();
    const clickY = event.clientY - rect.top - thumbHeight / 2;
    const ratio = Math.min(1, Math.max(0, clickY / maxThumbTop));
    el.scrollTop = ratio * maxScroll;
  };

  const visible = (overflows && !hidden) || dragging;

  return (
    <div
      ref={trackRef}
      className={`fade-scrollbar${visible ? " is-visible" : ""}${dragging ? " is-dragging" : ""}`}
      aria-hidden="true"
      onPointerDown={handleTrackPointerDown}
    >
      <div
        ref={thumbRef}
        className="fade-scrollbar-thumb"
        onPointerDown={handleThumbPointerDown}
        onPointerMove={handleThumbPointerMove}
        onPointerUp={endDrag}
        onPointerCancel={endDrag}
      />
    </div>
  );
}
