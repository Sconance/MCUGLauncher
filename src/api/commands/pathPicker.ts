import { call } from "../invoke";
import { revealInFileManager } from "../shellBridge";
import { openFolder } from "./settings";

export interface PathPickerOptions {
  title?: string;
  defaultDirectory?: string;
}

/** 打开原生文件选择器，取消时返回 null。 */
export const pickFile = (options: PathPickerOptions = {}) =>
  call<string | null>("pick_file", { ...options });

/** 打开原生目录选择器，取消时返回 null。 */
export const pickFolder = (options: PathPickerOptions = {}) =>
  call<string | null>("pick_folder", { ...options });

/** 在系统文件管理器中打开或定位指定路径；演示模式或失败时回退到 open_folder。 */
export async function openPathInExplorer(path: string, fallbackKind?: string): Promise<string> {
  const trimmed = path.trim();
  if (trimmed.length > 0 && (await revealInFileManager(trimmed))) {
    return "已在文件管理器中打开。";
  }
  if (fallbackKind) {
    const result = await openFolder(fallbackKind);
    return result.message;
  }
  return trimmed.length === 0 ? "路径为空，无法打开。" : "当前环境无法打开文件管理器。";
}
