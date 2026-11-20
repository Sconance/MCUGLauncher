import { createContext, useContext } from "react";

interface Navigation {
  /** 回到启动页（主页）。 */
  goHome: () => void;
  /** 跳到「更多 → 账号与皮肤」。 */
  openAccount: () => void;
  /** 跳到「发现 → 我的收藏」。 */
  openDiscoverBookmarks: () => void;
  /** 打开实例管理器；传入实例 id 时直接展开该实例的设置。 */
  openInstanceManager: (instanceId?: string) => void;
}

export const NavigationContext = createContext<Navigation>({
  goHome: () => {},
  openAccount: () => {},
  openDiscoverBookmarks: () => {},
  openInstanceManager: () => {},
});

export const useNavigation = () => useContext(NavigationContext);
