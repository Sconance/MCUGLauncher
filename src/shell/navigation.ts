import { createContext, useContext } from "react";

interface Navigation {
  /** 跳到「更多 → 账号与皮肤」。 */
  openAccount: () => void;
}

export const NavigationContext = createContext<Navigation>({ openAccount: () => {} });

export const useNavigation = () => useContext(NavigationContext);
