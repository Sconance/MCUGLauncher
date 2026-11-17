import { useEffect, useState } from "react";

const KEY = "mcugl.liquidGlass";
const listeners = new Set<(on: boolean) => void>();

/** 液态玻璃开关是否开启（持久化在 localStorage）。 */
export function isLiquidGlass(): boolean {
  return window.localStorage.getItem(KEY) === "1";
}

/** 切换开关：同步 localStorage、body 属性与所有订阅者。 */
export function setLiquidGlass(on: boolean): void {
  window.localStorage.setItem(KEY, on ? "1" : "0");
  document.body.dataset.liquid = on ? "on" : "off";
  listeners.forEach((listener) => listener(on));
}

/** 启动时把持久化的值同步到 body，供纯 CSS 选择器使用。 */
export function syncLiquidGlass(): void {
  document.body.dataset.liquid = isLiquidGlass() ? "on" : "off";
}

/** 订阅开关状态，组件内可即时响应设置页的切换。 */
export function useLiquidGlass(): boolean {
  const [on, setOn] = useState(isLiquidGlass);

  useEffect(() => {
    listeners.add(setOn);
    return () => {
      listeners.delete(setOn);
    };
  }, []);

  return on;
}
