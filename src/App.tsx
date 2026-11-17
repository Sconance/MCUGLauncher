import { useEffect } from "react";
import { syncLiquidGlass } from "./shell/liquid";
import Shell from "./shell/Shell";
import { ToastProvider } from "./shell/toast";
import { LauncherProvider } from "./state/launcher";

export default function App() {
  // 启动时把液态玻璃开关同步到 body，纯 CSS 部分据此生效
  useEffect(() => {
    syncLiquidGlass();
  }, []);

  return (
    <ToastProvider>
      <LauncherProvider>
        <Shell />
      </LauncherProvider>
    </ToastProvider>
  );
}
