import { createContext, useCallback, useContext, useEffect, useState, type ReactNode } from "react";

interface ToastApi {
  notify: (message: string) => void;
}

const ToastContext = createContext<ToastApi>({ notify: () => {} });

/** 全局提示：后端事件与页面操作共用同一个出口。 */
export function ToastProvider({ children }: { children: ReactNode }) {
  const [message, setMessage] = useState("");

  const notify = useCallback((next: string) => setMessage(next), []);

  useEffect(() => {
    if (message.length === 0) return;
    const timer = window.setTimeout(() => setMessage(""), 3000);
    return () => window.clearTimeout(timer);
  }, [message]);

  return (
    <ToastContext.Provider value={{ notify }}>
      {children}
      {message.length > 0 ? <div className="toast">{message}</div> : null}
    </ToastContext.Provider>
  );
}

export const useToast = () => useContext(ToastContext);
