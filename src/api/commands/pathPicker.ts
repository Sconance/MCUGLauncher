import { call } from "../invoke";

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
