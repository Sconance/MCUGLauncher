import { ChakraProvider } from "@chakra-ui/react";
import { useEffect } from "react";
import { syncLiquidGlass } from "./shell/liquid";
import Shell from "./shell/Shell";
import { ToastProvider } from "./shell/toast";
import { LauncherProvider } from "./state/launcher";
import { theme } from "./theme";
import { I18nProvider } from "./i18n";

export default function App() {
  // 启动时把液态玻璃开关同步到 body，纯 CSS 部分据此生效
  useEffect(() => {
    syncLiquidGlass();
  }, []);

  return (
    <ChakraProvider value={theme}>
      <I18nProvider>
        <ToastProvider>
          <LauncherProvider>
            <Shell />
          </LauncherProvider>
        </ToastProvider>
      </I18nProvider>
    </ChakraProvider>
  );
}
