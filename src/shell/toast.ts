import { createContext, useContext } from "react";

/** 全局提示出口：Shell 提供，页面通过 useToast() 调用。 */
export const ToastContext = createContext<(message: string) => void>(() => {});

export const useToast = () => useContext(ToastContext);
