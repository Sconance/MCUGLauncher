import { useCallback, useState } from "react";
import DiscoverIcon from "../assets/DiscoverIcon.svg";
import downloadIcon from "../assets/DownloadIcon.svg";
import extraIcon from "../assets/ExtraIcon.svg";
import launchIcon from "../assets/LaunchIcon.svg";
import settingsIcon from "../assets/SettingsIcon.svg";
import DiscoverPage from "../pages/DiscoverPage";
import DownloadPage from "../pages/DownloadPage";
import ExtrasPage from "../pages/ExtrasPage";
import HomePage from "../pages/HomePage";
import SettingsPage from "../pages/SettingsPage";
import { useLauncher } from "../state/launcher";
import { NavigationContext } from "./navigation";

type PageKey = "home" | "discover" | "download" | "settings" | "extras";

const NAV: { key: PageKey; label: string; icon: string }[] = [
  { key: "home", label: "启动", icon: launchIcon },
  { key: "discover", label: "发现", icon: DiscoverIcon },
  { key: "download", label: "下载", icon: downloadIcon },
  { key: "settings", label: "设置", icon: settingsIcon },
  { key: "extras", label: "更多", icon: extraIcon },
];

/** 应用外壳：悬浮胶囊导航 + 页面宿主 + 后端错误提示（Toast 由 App 统一提供）。 */
export default function Shell() {
  const { error } = useLauncher();
  const [page, setPage] = useState<PageKey>("home");
  const [extrasSection, setExtrasSection] = useState("instances");

  const openAccount = useCallback(() => {
    setExtrasSection("account");
    setPage("extras");
  }, []);
  const openInstances = useCallback(() => {
    setExtrasSection("instances");
    setPage("extras");
  }, []);

  let content;
  switch (page) {
    case "home":
      content = <HomePage />;
      break;
    case "discover":
      content = <DiscoverPage />;
      break;
    case "download":
      content = <DownloadPage />;
      break;
    case "settings":
      content = <SettingsPage />;
      break;
    case "extras":
      content = <ExtrasPage active={extrasSection} onChange={setExtrasSection} />;
      break;
  }

  return (
    <NavigationContext.Provider value={{ openAccount, openInstances }}>
      <div className="shell">
        <main className="page-host">
          {error.length > 0 ? <div className="backend-error">{error}</div> : null}
          {content}
        </main>

        {/* 悬浮胶囊导航：不占布局高度，页面内容从窗口顶边开始、从胶囊下方穿过 */}
        <div className="nav-dock">
          <nav className="nav-pill" aria-label="主导航">
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
        </div>
      </div>
    </NavigationContext.Provider>
  );
}
