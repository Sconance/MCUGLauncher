import { revealItemInDir } from "@tauri-apps/plugin-opener";
import { isDemoMode, isTauri } from "./invoke";

/**
 * 桌面壳本地能力桥接（不在 `mcugl.kernel/v1` 内核方法表内）。
 * 演示模式或非 Tauri 环境下返回 false，由调用方优雅回退。
 */
export async function revealInFileManager(path: string): Promise<boolean> {
  if (isDemoMode() || !isTauri()) return false;
  try {
    await revealItemInDir(path);
    return true;
  } catch {
    return false;
  }
}
