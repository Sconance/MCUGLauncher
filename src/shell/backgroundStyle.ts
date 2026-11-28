import { useEffect, useState } from "react";

/** 界面背景样式：frosted = 磨砂（半透明 + 模糊，默认），opaque = 不透明（新拟态）。 */
export type BackgroundStyle = "frosted" | "opaque";

const KEY = "mcugl.backgroundStyle";
const listeners = new Set<(style: BackgroundStyle) => void>();

/** 读取持久化的背景样式；未设置或值非法时回到磨砂。 */
export function getBackgroundStyle(): BackgroundStyle {
  return window.localStorage.getItem(KEY) === "opaque" ? "opaque" : "frosted";
}

/** 将背景样式写入根节点的 data-bg-style 属性，驱动 CSS 令牌与控件样式切换。 */
export function applyBackgroundStyle(style: BackgroundStyle): void {
  document.documentElement.dataset.bgStyle = style;
}

/** 启动时同步一次持久化值，避免首帧闪烁。 */
export function initBackgroundStyle(): BackgroundStyle {
  const style = getBackgroundStyle();
  applyBackgroundStyle(style);
  return style;
}

/** 切换背景样式：写入 localStorage、更新根节点属性并通知所有订阅者。 */
export function setBackgroundStyle(style: BackgroundStyle): void {
  window.localStorage.setItem(KEY, style);
  applyBackgroundStyle(style);
  listeners.forEach((listener) => listener(style));
}

/** 订阅背景样式变化，设置页与其他组件可即时同步。 */
export function useBackgroundStyle(): BackgroundStyle {
  const [style, setStyle] = useState<BackgroundStyle>(getBackgroundStyle);

  useEffect(() => {
    listeners.add(setStyle);
    return () => {
      listeners.delete(setStyle);
    };
  }, []);

  return style;
}
