import { useEffect, useRef, useState } from "react";
import "./LaunchToast.css";

/** 进度爬升时长、补满时长、滑出时长（滑出与普通 toast 一致） */
const RISE_MS = 1700;
const FULL_MS = 260;
const EXIT_MS = 180;

interface LaunchToastProps {
  /** 实例名，显示在标题里 */
  name: string;
  /** 启动请求：settle 之后才补满进度并滑出 */
  task: Promise<unknown>;
  /** 外部取消：停在当前进度、标题改成「已取消启动」并滑出 */
  cancelled?: boolean;
  /** 滑出动画播完 */
  onExit: () => void;
}

/**
 * 启动进度的特殊 toast：白色卡片，浮在底部按钮栏上方，
 * 带一条进度条；靠右时从右侧横向进出，居中时位于启动图标正上方、向上滑入并向下滑出。
 */
export default function LaunchToast({ name, task, cancelled = false, onExit }: LaunchToastProps) {
  const [progress, setProgress] = useState(0);
  const [leaving, setLeaving] = useState(false);
  const exitRef = useRef(onExit);
  exitRef.current = onExit;
  const cancelledRef = useRef(cancelled);
  cancelledRef.current = cancelled;

  // 进度：爬到 90% 后等启动请求 settle，再补满
  useEffect(() => {
    let frame = 0;
    let settled = false;
    let phase: "rise" | "full" = "rise";
    let phaseStart = performance.now();

    void task.then(
      () => {
        settled = true;
      },
      () => {
        settled = true;
      },
    );

    const tick = (now: number) => {
      if (cancelledRef.current) return;
      if (phase === "rise") {
        const ratio = Math.min(1, (now - phaseStart) / RISE_MS);
        setProgress(90 * (1 - Math.pow(1 - ratio, 2)));
        if (ratio >= 1 && settled) {
          phase = "full";
          phaseStart = now;
        }
        frame = window.requestAnimationFrame(tick);
        return;
      }
      const ratio = Math.min(1, (now - phaseStart) / FULL_MS);
      setProgress(90 + 10 * ratio);
      if (ratio >= 1) {
        setLeaving(true);
        return;
      }
      frame = window.requestAnimationFrame(tick);
    };

    frame = window.requestAnimationFrame(tick);
    return () => window.cancelAnimationFrame(frame);
  }, [task]);

  // 取消：停在当前进度直接滑出
  useEffect(() => {
    if (cancelled) setLeaving(true);
  }, [cancelled]);

  // 滑出动画播完再通知父组件卸载
  useEffect(() => {
    if (!leaving) return;
    const timer = window.setTimeout(() => exitRef.current(), EXIT_MS);
    return () => window.clearTimeout(timer);
  }, [leaving]);

  return (
    <div className={`launch-toast${leaving ? " is-leaving" : ""}`} role="status" aria-live="polite">
      <div className="launch-toast-row">
        <span className="launch-toast-title">{cancelled ? "已取消启动" : `正在启动 ${name}`}</span>
        <span className="launch-toast-pct">{Math.round(progress)}%</span>
      </div>
      <div className="launch-toast-track">
        <span className="launch-toast-fill" style={{ width: `${progress}%` }} />
      </div>
    </div>
  );
}
