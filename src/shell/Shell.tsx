import { useCallback, useEffect, useState } from "react";
import closeIcon from "../assets/CloseIcon.svg";
import downloadIcon from "../assets/DownloadIcon.svg";
import extraIcon from "../assets/ExtraIcon.svg";
import launchIcon from "../assets/LaunchIcon.svg";
import maximizeIcon from "../assets/MaximizeIcon.svg";
import minimizeIcon from "../assets/MinimizeIcon.svg";
import settingsIcon from "../assets/SettingsIcon.svg";
import { appWindow } from "../api/window";
import { APP_VERSION } from "../config";
import DownloadPage from "../pages/DownloadPage";
import ExtrasPage from "../pages/ExtrasPage";
import HomePage from "../pages/HomePage";
import SettingsPage from "../pages/SettingsPage";
import { ToastContext } from "./toast";

type PageKey = "home" | "download" | "settings" | "extras";

const NAV: { key: PageKey; label: string; icon: string }[] = [
  { key: "home", label: "启动", icon: launchIcon },
  { key: "download", label: "下载", icon: downloadIcon },
  { key: "settings", label: "设置", icon: settingsIcon },
  { key: "extras", label: "更多", icon: extraIcon },
];

function renderPage(page: PageKey) {
  switch (page) {
    case "home":
      return <HomePage />;
    case "download":
      return <DownloadPage />;
    case "settings":
      return <SettingsPage />;
    case "extras":
      return <ExtrasPage />;
  }
}

/** 应用外壳：标题栏、顶部导航、页面宿主与全局提示。 */
export default function Shell() {
  const [page, setPage] = useState<PageKey>("home");
  const [toast, setToast] = useState("");

  const notify = useCallback((message: string) => setToast(message), []);

  useEffect(() => {
    if (toast.length === 0) return;
    const timer = window.setTimeout(() => setToast(""), 3000);
    return () => window.clearTimeout(timer);
  }, [toast]);

  return (
    <ToastContext.Provider value={notify}>
      <div className="shell">
        <header
          className="titlebar"
          data-tauri-drag-region
          onDoubleClick={(event) => {
            if ((event.target as HTMLElement).closest("button")) return;
            void appWindow.toggleMaximize();
          }}
        >
          <div className="brand" data-tauri-drag-region>
            <span className="brand-title">MCUG Launcher</span>
            <span className="brand-version">{APP_VERSION}</span>
          </div>

          <nav className="topnav" aria-label="主导航">
            {NAV.map((item) => (
              <button
                key={item.key}
                type="button"
                className={`btn topnav ${item.key === page ? "active" : ""}`}
                onClick={() => setPage(item.key)}
              >
                <img src={item.icon} alt="" width={16} height={16} />
                {item.label}
              </button>
            ))}
          </nav>

          <div className="window-controls">
            <button type="button" className="btn icon" aria-label="最小化" onClick={() => void appWindow.minimize()}>
              <img src={minimizeIcon} alt="" />
            </button>
            <button type="button" className="btn icon" aria-label="最大化" onClick={() => void appWindow.toggleMaximize()}>
              <img src={maximizeIcon} alt="" />
            </button>
            <button type="button" className="btn icon close" aria-label="关闭" onClick={() => void appWindow.close()}>
              <img src={closeIcon} alt="" />
            </button>
          </div>
        </header>

        <main className="page-host">{renderPage(page)}</main>

        {toast.length > 0 ? <div className="toast">{toast}</div> : null}
      </div>
    </ToastContext.Provider>
  );
}
