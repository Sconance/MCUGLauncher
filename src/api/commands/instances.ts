import { call } from "../invoke";
import type {
  ContentUpdatePreviewInfo,
  ContentUpdatesInfo,
  LaunchOptionsInfo,
  LaunchSettings,
  SavedInfo,
  UninstallPreviewInfo,
} from "../../types/launcher";

/** 生成卸载预览与确认令牌；不会删除文件。 */
export const previewUninstall = (version: string) =>
  call<UninstallPreviewInfo>("preview_uninstall", { version });

/** 按预览令牌执行卸载。 */
export const applyUninstall = (token: string) => call<SavedInfo>("apply_uninstall", { token });

/** 克隆实例，可选择包含存档与服务器列表。 */
export const cloneInstance = (version: string, name: string, saves = false, servers = false) =>
  call<SavedInfo>("clone_instance", { version, name, saves, servers });

/** 将实例导出为整合包 ZIP。 */
export const exportInstance = (version: string, path: string, servers = false, saves = false) =>
  call<SavedInfo>("export_instance", { version, path, servers, saves });

/** 检查实例中由启动器管理的内容更新。 */
export const checkUpdates = (version: string) => call<ContentUpdatesInfo>("check_updates", { version });

/** 为所选项目生成更新预览。 */
export const previewUpdates = (token: string, projects: string[] = []) =>
  call<ContentUpdatePreviewInfo>("preview_updates", { token, projects });

/** 按预览结果把更新加入队列。 */
export const applyUpdates = (token: string) => call<SavedInfo>("apply_updates", { token });

/** 启用或禁用模组。 */
export const toggleMod = (instanceId: string, modId: string, enabled: boolean) =>
  call<SavedInfo>("toggle_mod", { instanceId, modId, enabled });

/** 删除模组文件。 */
export const deleteMod = (instanceId: string, modId: string) =>
  call<SavedInfo>("delete_mod", { instanceId, modId });

/** 读取实例启动设置与继承状态。 */
export const readLaunchOptions = (version: string, requestId = "") =>
  call<LaunchOptionsInfo>("read_launch_options", { version, requestId });

/** 保存实例启动设置；`inherit` 表示恢复继承全局设置。 */
export const saveLaunchOptions = (
  token: string,
  version: string,
  name: string,
  settings: LaunchSettings,
  inherit: boolean,
) => call<SavedInfo>("save_launch_options", { token, version, name, settings, inherit });
