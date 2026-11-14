import { getCurrentWindow } from "@tauri-apps/api/window";

const inTauri = typeof window !== "undefined" && "__TAURI_INTERNALS__" in window;

async function run(action: (win: ReturnType<typeof getCurrentWindow>) => Promise<void>): Promise<void> {
  if (!inTauri) return;
  try {
    await action(getCurrentWindow());
  } catch {
    /* 浏览器预览或权限不足时忽略 */
  }
}

/** 自定义标题栏的窗口操作；非 Tauri 环境下静默降级。 */
export const appWindow = {
  minimize: () => run((win) => win.minimize()),
  toggleMaximize: () => run((win) => win.toggleMaximize()),
  close: () => run((win) => win.close()),
};
