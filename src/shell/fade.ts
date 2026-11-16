import { useEffect, useState } from "react";

/**
 * 分区切换的淡出淡入：先让旧内容淡出，再换成新内容淡入。
 *
 * 返回值：`shown` 是当前应当渲染的分区键，`out` 为 true 时给容器加淡出类。
 * 容器需配 `transition: opacity`，否则会瞬间消失而不是淡出。
 */
export function useFadeSwap<T extends string>(active: T, duration = 130): { shown: T; out: boolean } {
  const [shown, setShown] = useState(active);
  const [out, setOut] = useState(false);

  useEffect(() => {
    if (active === shown) return;
    setOut(true);
    const timer = window.setTimeout(() => {
      setShown(active);
      setOut(false);
    }, duration);
    return () => window.clearTimeout(timer);
  }, [active, shown, duration]);

  return { shown, out };
}
