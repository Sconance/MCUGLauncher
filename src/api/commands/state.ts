import { call } from "../invoke";
import type {
  AccountInfo,
  FavoriteInfo,
  InstanceCatalogInfo,
  LauncherOverview,
  ModInfo,
  ResourceSnapshot,
  SavedInfo,
  SavedServerInfo,
  SettingsInfo,
} from "../../types/launcher";

export const getOverview = () => call<ResourceSnapshot<LauncherOverview>>("get_overview");
export const getSettings = () => call<ResourceSnapshot<SettingsInfo>>("get_settings");
export const listAccounts = () => call<ResourceSnapshot<AccountInfo[]>>("list_accounts");
export const listInstances = () => call<ResourceSnapshot<InstanceCatalogInfo>>("list_instances");
export const listInstanceMods = (instanceId: string) =>
  call<ResourceSnapshot<ModInfo[]>>("list_instance_mods", { instanceId });
export const listServers = () => call<ResourceSnapshot<SavedServerInfo[]>>("list_servers");
export const listBookmarks = () => call<ResourceSnapshot<FavoriteInfo[]>>("list_bookmarks");

/** 按稳定 ID 切换当前实例。 */
export const selectInstance = (instanceId: string) => call<SavedInfo>("select_instance", { instanceId });
