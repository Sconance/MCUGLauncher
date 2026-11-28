import { useEffect, useState } from "react";

/**
 * 浮层/下拉菜单的离场动画保活 Hook：
 * - open 变为 true 时立即挂载（mounted=true, closing=false）；
 * - open 变为 false 时保持挂载并置 closing=true，等待 durationMs 后再卸载。
 */
export function usePresence(open: boolean, durationMs = 140): {
  mounted: boolean;
  closing: boolean;
} {
  const [mounted, setMounted] = useState(open);

  useEffect(() => {
    if (open) {
      setMounted(true);
      return;
    }
    if (!mounted) return;
    const reduced =
      typeof window !== "undefined" &&
      window.matchMedia("(prefers-reduced-motion: reduce)").matches;
    if (reduced || durationMs <= 0) {
      setMounted(false);
      return;
    }
    const timer = window.setTimeout(() => setMounted(false), durationMs);
    return () => window.clearTimeout(timer);
  }, [open, mounted, durationMs]);

  return { mounted, closing: mounted && !open };
}
