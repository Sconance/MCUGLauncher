import { useCallback, useEffect, useLayoutEffect, useRef, useState } from "react";
import DiscoverIcon from "../assets/DiscoverIcon.svg";
import DiscoverIconActive from "../assets/DiscoverIconActive.svg";
import downloadIcon from "../assets/DownloadIcon.svg";
import downloadIconActive from "../assets/DownloadIconActive.svg";
import extraIcon from "../assets/ExtraIcon.svg";
import extraIconActive from "../assets/ExtraIconActive.svg";
import launchIcon from "../assets/LaunchIcon.svg";
import launchIconActive from "../assets/LaunchIconActive.svg";
import settingsIcon from "../assets/SettingsIcon.svg";
import settingsIconActive from "../assets/SettingsIconActive.svg";
import DiscoverPage from "../pages/DiscoverPage";
import DownloadPage from "../pages/DownloadPage";
import ExtrasPage from "../pages/ExtrasPage";
import HomePage from "../pages/HomePage";
import SettingsPage from "../pages/SettingsPage";
import { useLauncher } from "../state/launcher";
import { NavigationContext } from "./navigation";

type PageKey = "home" | "discover" | "download" | "settings" | "extras";

const NAV: { key: PageKey; label: string; icon: string; iconActive: string }[] = [
  { key: "home", label: "启动", icon: launchIcon, iconActive: launchIconActive },
  { key: "discover", label: "发现", icon: DiscoverIcon, iconActive: DiscoverIconActive },
  { key: "download", label: "下载", icon: downloadIcon, iconActive: downloadIconActive },
  { key: "settings", label: "设置", icon: settingsIcon, iconActive: settingsIconActive },
  { key: "extras", label: "更多", icon: extraIcon, iconActive: extraIconActive },
];

/** 应用外壳：悬浮胶囊导航 + 页面宿主 + 后端错误提示（Toast 由 App 统一提供）。 */
export default function Shell() {
  const { error } = useLauncher();
  const [page, setPage] = useState<PageKey>("home");
  const [extrasSection, setExtrasSection] = useState("instances");

  // 页面切换的淡出淡入：page 是选中态（胶囊高亮立刻跟随），shown 是正在渲染的页
  const [shown, setShown] = useState<PageKey>("home");
  const [phase, setPhase] = useState<"idle" | "out" | "in">("idle");
  const [dismissPanel, setDismissPanel] = useState(false); // 回「启动」时让内容栏随内容一起淡出
  const pageRef = useRef<PageKey>("home");

  // 选中背景用一枚独立的滑块，位置与宽度跟随当前按钮
  const navRef = useRef<HTMLDivElement>(null);
  const [indicator, setIndicator] = useState({ left: 0, width: 0 });
  const [slide, setSlide] = useState(false);

  useLayoutEffect(() => {
    const active = navRef.current?.querySelector<HTMLButtonElement>(`button[data-page="${page}"]`);
    if (!active) return;
    setIndicator({ left: active.offsetLeft, width: active.offsetWidth });
  }, [page]);

  // 首帧定位不加过渡，避免从左侧滑入
  useEffect(() => {
    const frame = window.requestAnimationFrame(() => setSlide(true));
    return () => window.cancelAnimationFrame(frame);
  }, []);

  /** 切页：先淡出旧页，再换页淡入。 */
  const go = useCallback((next: PageKey) => {
    if (next === pageRef.current) return;
    setDismissPanel(pageRef.current !== "home" && next === "home");
    pageRef.current = next;
    setPage(next);
    setPhase("out");
  }, []);

  useEffect(() => {
    if (phase !== "out") return;
    const timer = window.setTimeout(() => {
      setShown(page);
      setDismissPanel(false);
      setPhase("in");
    }, 140);
    return () => window.clearTimeout(timer);
  }, [phase, page]);

  useEffect(() => {
    if (phase !== "in") return;
    const timer = window.setTimeout(() => setPhase("idle"), 210);
    return () => window.clearTimeout(timer);
  }, [phase]);

  const openAccount = useCallback(() => {
    setExtrasSection("account");
    go("extras");
  }, [go]);
  const openInstances = useCallback(() => {
    setExtrasSection("instances");
    go("extras");
  }, [go]);

  let content;
  switch (shown) {
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

  const stageClass = [
    "page-stage",
    shown === "home" ? "plain" : "panel",
    dismissPanel && phase === "out" ? "is-out" : "",
  ]
    .filter((value) => value.length > 0)
    .join(" ");
  const fadeClass = [
    "page-fade",
    phase === "out" ? "is-out" : "",
    phase === "in" ? "is-in" : "",
  ]
    .filter((value) => value.length > 0)
    .join(" ");

  return (
    <NavigationContext.Provider value={{ openAccount, openInstances }}>
      <div className="shell">
        <main className="page-host">
          {error.length > 0 ? <div className="backend-error">{error}</div> : null}
          <div className={stageClass}>
            <div className={fadeClass}>{content}</div>
          </div>
        </main>

        {/* 悬浮胶囊导航：不占布局高度，页面内容从窗口顶边开始、从胶囊下方穿过 */}
        <div className="nav-dock">
          <nav className="nav-pill" aria-label="主导航" ref={navRef}>
            <span
              className={`nav-indicator${slide ? " slide" : ""}`}
              style={{ transform: `translateX(${indicator.left}px)`, width: indicator.width }}
              aria-hidden="true"
            />
            {NAV.map((item) => (
              <button
                key={item.key}
                type="button"
                data-page={item.key}
                className={`btn topnav ${item.key === page ? "active" : ""}`}
                onClick={() => go(item.key)}
              >
                <img src={item.key === page ? item.iconActive : item.icon} alt="" width={16} height={16} />
                {item.label}
              </button>
            ))}
          </nav>
        </div>
      </div>
    </NavigationContext.Provider>
  );
}
