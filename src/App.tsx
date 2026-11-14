import Shell from "./shell/Shell";
import { ToastProvider } from "./shell/toast";
import { LauncherProvider } from "./state/launcher";

export default function App() {
  return (
    <ToastProvider>
      <LauncherProvider>
        <Shell />
      </LauncherProvider>
    </ToastProvider>
  );
}
