import { createContext, useCallback, useContext, useEffect, useRef, useState, type ReactNode } from "react";

interface ToastApi {
  notify: (message: string) => void;
}

const ToastContext = createContext<ToastApi>({ notify: () => {} });

/** 停留时长与滑出动画时长：滑出结束后才把节点摘掉 */
const HOLD_MS = 3000;
const LEAVE_MS = 180;

/** 全局提示：后端事件与页面操作共用同一个出口，固定在左下角。 */
export function ToastProvider({ children }: { children: ReactNode }) {
  const [message, setMessage] = useState("");
  /** 滑出中：节点还挂着，等动画播完再移除 */
  const [leaving, setLeaving] = useState(false);
  const timers = useRef<number[]>([]);

  const clearTimers = useCallback(() => {
    timers.current.forEach((timer) => window.clearTimeout(timer));
    timers.current = [];
  }, []);

  const notify = useCallback(
    (next: string) => {
      if (next.length === 0) return;
      clearTimers();
      setLeaving(false);
      setMessage(next);
      timers.current.push(
        window.setTimeout(() => {
          setLeaving(true);
          timers.current.push(
            window.setTimeout(() => {
              setMessage("");
              setLeaving(false);
            }, LEAVE_MS),
          );
        }, HOLD_MS),
      );
    },
    [clearTimers],
  );

  useEffect(() => clearTimers, [clearTimers]);

  return (
    <ToastContext.Provider value={{ notify }}>
      {children}
      {message.length > 0 ? (
        <div className={`toast${leaving ? " is-leaving" : ""}`} role="status" aria-live="polite">
          {message}
        </div>
      ) : null}
    </ToastContext.Provider>
  );
}

export const useToast = () => useContext(ToastContext);
