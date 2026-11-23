import { ChakraProvider } from "@chakra-ui/react";
import DemoBadge from "./demo/DemoBadge";
import { isDemoMode } from "./api/invoke";
import Shell from "./shell/Shell";
import { ToastProvider } from "./shell/toast";
import { LauncherProvider } from "./state/launcher";
import { theme } from "./theme";
import { I18nProvider } from "./i18n";

export default function App() {
  return (
    <ChakraProvider value={theme}>
      <I18nProvider>
        <ToastProvider>
          <LauncherProvider>
            <Shell />
            {isDemoMode() ? <DemoBadge /> : null}
          </LauncherProvider>
        </ToastProvider>
      </I18nProvider>
    </ChakraProvider>
  );
}
