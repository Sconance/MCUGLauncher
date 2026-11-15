import { createContext, useContext } from "react";

interface Navigation {
  /** 跳到「更多 → 账号与皮肤」。 */
  openAccount: () => void;
  /** 跳到「更多 → 实例」。 */
  openInstances: () => void;
}

export const NavigationContext = createContext<Navigation>({
  openAccount: () => {},
  openInstances: () => {},
});

export const useNavigation = () => useContext(NavigationContext);
