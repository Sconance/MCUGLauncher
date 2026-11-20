import { createContext, useContext } from "react";

interface Navigation {
  /** 回到启动页（主页）。 */
  goHome: () => void;
  /** 跳到「更多 → 账号与皮肤」。 */
  openAccount: () => void;
  /** 打开实例管理器（主页实例卡与实例设置进入的左右栏页面）。 */
  openInstanceManager: () => void;
}

export const NavigationContext = createContext<Navigation>({
  goHome: () => {},
  openAccount: () => {},
  openInstanceManager: () => {},
});

export const useNavigation = () => useContext(NavigationContext);
