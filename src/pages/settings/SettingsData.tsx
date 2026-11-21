import { createContext, useContext, type ReactNode } from "react";
import type { ResourceVersion } from "../../state/launcher";
import type { AccountInfo, LauncherOverview, ResourceKeyInfo, SettingsInfo } from "../../types/launcher";

/**
 * 设置分区共享的已加载数据。
 *
 * SettingsPage 在启动器数据就绪后才挂载各分区，所以这里的字段都是非空的；
 * 分区组件不需要各自再判空，也不会重复订阅 useLauncher。
 */
export interface SettingsData {
  overview: LauncherOverview;
  settings: SettingsInfo;
  accounts: AccountInfo[];
  resourceVersions: Partial<Record<string, ResourceVersion>>;
  refreshSettingsResources: (changed: readonly ResourceKeyInfo[]) => Promise<void>;
}

const SettingsDataContext = createContext<SettingsData | null>(null);

export function SettingsDataProvider({
  value,
  children,
}: {
  value: SettingsData;
  children: ReactNode;
}) {
  return <SettingsDataContext.Provider value={value}>{children}</SettingsDataContext.Provider>;
}

export function useSettingsData(): SettingsData {
  const value = useContext(SettingsDataContext);
  if (!value) throw new Error("useSettingsData 必须在 SettingsDataProvider 内使用。");
  return value;
}
