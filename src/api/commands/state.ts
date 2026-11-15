import { call } from "../invoke";
import type {
  AccountInfo,
  FavoriteInfo,
  InstanceCatalogInfo,
  LauncherOverview,
  LauncherStateBundle,
  ModInfo,
  ResourceSnapshot,
  SavedInfo,
  SavedServerInfo,
  SettingsInfo,
} from "../../types/launcher";

/** 获取启动器完整状态快照。 */
/** 从独立资源查询组装兼容页面所需的状态视图。 */
export async function getState(): Promise<LauncherStateBundle> {
  const [overview, settings, accounts, instances, servers, bookmarks] = await Promise.all([
    call<ResourceSnapshot<LauncherOverview>>("get_overview"),
    call<ResourceSnapshot<SettingsInfo>>("get_settings"),
    call<ResourceSnapshot<AccountInfo[]>>("list_accounts"),
    call<ResourceSnapshot<InstanceCatalogInfo>>("list_instances"),
    call<ResourceSnapshot<SavedServerInfo[]>>("list_servers"),
    call<ResourceSnapshot<FavoriteInfo[]>>("list_bookmarks"),
  ]);
  const snapshots = [settings, accounts, instances, servers, bookmarks];
  if (snapshots.some((snapshot) => snapshot.epoch !== overview.epoch)) {
    throw new Error("后端运行代次已变化，请重新读取状态。");
  }
  const mods = overview.data.selectedInstanceId
    ? await call<ResourceSnapshot<ModInfo[]>>("list_instance_mods", {
        instanceId: overview.data.selectedInstanceId,
      })
    : { epoch: overview.epoch, revision: 0, data: [] };
  if (mods.epoch !== overview.epoch) throw new Error("后端运行代次已变化，请重新读取状态。");

  return {
    epoch: overview.epoch,
    settingsRevision: settings.revision,
    data: {
      ...overview.data,
      ...settings.data,
      accounts: accounts.data,
      instances: instances.data.items,
      versionGroups: instances.data.versionGroups,
      mods: mods.data,
      servers: servers.data,
      favorites: bookmarks.data,
    },
  };
}

/** 切换当前实例并持久化选择。 */
export const selectInstance = (instanceId: string) => call<SavedInfo>("select_instance", { instanceId });
