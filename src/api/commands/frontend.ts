import type { KernelManifest } from "@mcugl/frontpack-sdk";
import { callHost } from "../invoke";
import type { SavedInfo } from "../../types/launcher";

/** 已安装前端包的一条记录（同一 identifier 可能装了多个版本）。 */
export interface FrontendSummary {
  identifier: string;
  name: string;
  version: string;
  kernelApi: string;
  entry: string;
  author?: string | null;
  description?: string | null;
  homepage?: string | null;
  /** 包内是否带图标（`icon.png` 或清单里的 `icon`）。 */
  hasIcon: boolean;
  /** 是否是配置里选中的前端包（多版本时只有最新版本会被加载）。 */
  active: boolean;
}

export interface FrontendList {
  active: string | null;
  items: FrontendSummary[];
}

/** 读取内核自描述清单（方法表、协议版本、能力边界与当前挂载前端包）。 */
export const getKernelManifest = () => callHost<KernelManifest>("kernel_manifest");

/** 列出数据目录里已安装的前端包。 */
export const listFrontends = () => callHost<FrontendList>("list_frontends");

/** 读一个已安装前端包的图标，返回 `data:` URL；包内没有图标时返回 null。 */
export const readFrontendIcon = (identifier: string, version: string | null) =>
  callHost<string | null>("read_frontend_icon", { identifier, version });

/** 从 `.mcuglf` / `.zip` 包导入一个前端包（校验通过后解压到数据目录）。 */
export const installFrontend = (path: string) => callHost<FrontendSummary>("install_frontend", { path });

/** 切换前端包；`identifier` 为 null 时切回官方前端包。点「重启前端包」后生效。 */
export const useFrontend = (identifier: string | null, expectedRevision: number) =>
  callHost<SavedInfo>("use_frontend", { identifier, expectedRevision });

/**
 * 按当前配置立即重建界面，让已保存的前端包选择生效；进程不退出。
 *
 * 调用返回后旧窗口会被销毁，所以这个 Promise 可能来不及完成 —— 属于正常现象。
 */
export const reloadFrontend = () => callHost<SavedInfo>("reload_frontend");

/** 卸载前端包；`version` 为 null 时卸载该 identifier 的全部版本。 */
export const removeFrontend = (identifier: string, version: string | null, expectedRevision: number) =>
  callHost<SavedInfo>("remove_frontend", { identifier, version, expectedRevision });
