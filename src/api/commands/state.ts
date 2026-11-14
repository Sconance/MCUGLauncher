import { call } from "../invoke";
import type { LauncherState, SavedInfo } from "../../types/launcher";

/** 获取启动器完整状态快照。 */
export const getState = () => call<LauncherState>("get_launcher_state");

/** 切换当前实例并持久化选择。 */
export const selectInstance = (name: string) => call<SavedInfo>("select_instance", { name });

/** 切换当前版本并持久化选择。 */
export const selectVersion = (version: string) => call<SavedInfo>("select_version", { version });
