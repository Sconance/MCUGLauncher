import { useEffect, useState } from "react";

/** 主页底部按钮栏的对齐方式：right = 靠右下（默认），center = 贴底居中。 */
export type HomeAlignment = "right" | "center";

const KEY = "mcugl.homeAlignment";
const listeners = new Set<(alignment: HomeAlignment) => void>();

/** 读取持久化的对齐方式；未设置或值非法时回到靠右。 */
export function getHomeAlignment(): HomeAlignment {
  return window.localStorage.getItem(KEY) === "center" ? "center" : "right";
}

/** 切换对齐方式：写入 localStorage 并通知所有订阅者。 */
export function setHomeAlignment(alignment: HomeAlignment): void {
  window.localStorage.setItem(KEY, alignment);
  listeners.forEach((listener) => listener(alignment));
}

/** 订阅对齐方式，主页与设置页可即时同步。 */
export function useHomeAlignment(): HomeAlignment {
  const [alignment, setAlignment] = useState<HomeAlignment>(getHomeAlignment);

  useEffect(() => {
    listeners.add(setAlignment);
    return () => {
      listeners.delete(setAlignment);
    };
  }, []);

  return alignment;
}
