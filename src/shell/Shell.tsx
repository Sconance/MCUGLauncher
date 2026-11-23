import { useCallback, useEffect, useLayoutEffect, useRef, useState } from "react";
import DiscoverIcon from "../assets/icons/capsule/DiscoverIcon.svg";
import DiscoverIconActive from "../assets/icons/capsule/DiscoverIconActive.svg";
import accountIcon from "../assets/icons/capsule/AccountIcon.svg";
import accountIconActive from "../assets/icons/capsule/AccountIconActive.svg";
import downloadIcon from "../assets/icons/capsule/DownloadIcon.svg";
import downloadIconActive from "../assets/icons/capsule/DownloadIconActive.svg";
import launchIcon from "../assets/icons/capsule/LaunchIcon.svg";
import launchIconActive from "../assets/icons/capsule/LaunchIconActive.svg";
import settingsIcon from "../assets/icons/capsule/SettingsIcon.svg";
import settingsIconActive from "../assets/icons/capsule/SettingsIconActive.svg";
import AccountPage from "../pages/AccountPage";
import DiscoverPage, { type DiscoverSectionKey } from "../pages/DiscoverPage";
import DownloadPage from "../pages/DownloadPage";
import HomePage from "../pages/HomePage";
import InstanceManagerPage from "../pages/InstanceManagerPage";
import SettingsPage from "../pages/SettingsPage";
import TopStatus from "./TopStatusButtons";
import { useLauncher } from "../state/launcher";
import { useI18n } from "../i18n";
import { NavigationContext } from "./navigation";

type PageKey = "home" | "discover" | "download" | "settings" | "account" | "instances";

const NAV: { key: PageKey; labelKey: string; icon: string; iconActive: string }[] = [
  { key: "home", labelKey: "nav.home", icon: launchIcon, iconActive: launchIconActive },
  { key: "discover", labelKey: "nav.discover", icon: DiscoverIcon, iconActive: DiscoverIconActive },
  { key: "download", labelKey: "nav.download", icon: downloadIcon, iconActive: downloadIconActive },
  { key: "account", labelKey: "nav.account", icon: accountIcon, iconActive: accountIconActive },
  { key: "settings", labelKey: "nav.settings", icon: settingsIcon, iconActive: settingsIconActive },
];

/** 应用外壳：悬浮胶囊导航 + 页面宿主 + 后端错误提示（Toast 由 App 统一提供）。 */
export default function Shell() {
  const { error } = useLauncher();
  const { locale, t } = useI18n();

  const [page, setPage] = useState<PageKey>("home");
  /** 实例管理器要直接展开的实例；undefined 表示显示「全部实例」。 */
  const [instanceTarget, setInstanceTarget] = useState<string | undefined>();
  /** 发现页当前分区；由外壳保存，跨页面跳转时才能直接落到「我的收藏」。 */
  const [discoverSection, setDiscoverSection] = useState<DiscoverSectionKey>("servers");
  /**
   * 导航按钮的「高亮」状态：页面可以立刻切（`page`），但按钮的图标/文字颜色要等玻璃滑到位再变，
   * 所以按钮外观单独用 activeKey，默认跟着 page 走，只有玻璃动画期间被延后。
   */
  const [activeKey, setActiveKey] = useState<PageKey>("home");

  // 页面切换的淡出淡入：page 是选中态（胶囊高亮立刻跟随），shown 是正在渲染的页
  const [shown, setShown] = useState<PageKey>("home");
  const [phase, setPhase] = useState<"idle" | "out" | "in">("idle");
  const [dismissPanel, setDismissPanel] = useState(false); // 回「启动」时让内容栏随内容一起淡出
  const pageRef = useRef<PageKey>("home");

  // 选中背景用一枚独立的滑块，位置与宽度跟随当前按钮
  const navRef = useRef<HTMLDivElement>(null);
  /**
   * 导航层的定位条：只负责把白胶囊放到真实胶囊的位置上。
   * 位置/尺寸由 syncSceneBar() 按真实胶囊的 rect 逐像素写进 --nav-bar-*。
   */
  const bandRef = useRef<HTMLDivElement>(null);
  const [indicator, setIndicator] = useState({ left: 0, width: 0 });
  const [slide, setSlide] = useState(false);

  /** 白胶囊与真实胶囊逐像素重合：把 nav 的 rect 写进采样条的 CSS 变量。 */
  const syncSceneBar = useCallback(() => {
    const band = bandRef.current;
    const rect = navRef.current?.getBoundingClientRect();
    if (!band || !rect) return;
    band.style.setProperty("--nav-bar-left", `${Math.round(rect.left)}px`);
    band.style.setProperty("--nav-bar-top", `${Math.round(rect.top)}px`);
    band.style.setProperty("--nav-bar-w", `${Math.round(rect.width)}px`);
    band.style.setProperty("--nav-bar-h", `${Math.round(rect.height)}px`);
  }, []);

  // 首帧先对齐一次；再等一帧（图标/字体加载完按钮宽度会变一点）
  useLayoutEffect(() => {
    syncSceneBar();
    const frame = window.requestAnimationFrame(syncSceneBar);
    return () => window.cancelAnimationFrame(frame);
  }, [syncSceneBar]);

  useLayoutEffect(() => {
    const active = navRef.current?.querySelector<HTMLButtonElement>(`button[data-page="${page}"]`);
    if (!active) return;
    setIndicator({ left: active.offsetLeft, width: active.offsetWidth });
  }, [page]);

  // 切语言后文案长度变了：胶囊宽度与选中底都要重新量一次，否则文字会溢出白底
  useLayoutEffect(() => {
    const frame = window.requestAnimationFrame(() => {
      syncSceneBar();
      const active = navRef.current?.querySelector<HTMLButtonElement>(`button[data-page="${pageRef.current}"]`);
      if (active) setIndicator({ left: active.offsetLeft, width: active.offsetWidth });
    });
    return () => window.cancelAnimationFrame(frame);
  }, [locale, syncSceneBar]);

  // 按钮高亮跟着页面走
  useEffect(() => {
    setActiveKey(page);
  }, [page]);

  // 首帧定位不加过渡，避免从左侧滑入
  useEffect(() => {
    const frame = window.requestAnimationFrame(() => setSlide(true));
    return () => window.cancelAnimationFrame(frame);
  }, []);

  useEffect(() => {
    window.addEventListener("resize", syncSceneBar);
    return () => window.removeEventListener("resize", syncSceneBar);
  }, [syncSceneBar]);

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
      // 换页要赶在玻璃到位之前：玻璃大约 150ms 到达，这里 100ms 就把内容换掉
    }, 100);
    return () => window.clearTimeout(timer);
  }, [phase, page]);

  useEffect(() => {
    if (phase !== "in") return;
    const timer = window.setTimeout(() => setPhase("idle"), 210);
    return () => window.clearTimeout(timer);
  }, [phase]);

  const openAccount = useCallback(() => {
    go("account");
  }, [go]);
  const openDiscoverBookmarks = useCallback(() => {
    setDiscoverSection("bookmarks");
    go("discover");
  }, [go]);
  const openInstanceManager = useCallback((instanceId?: string) => {
    setInstanceTarget(instanceId);
    go("instances");
  }, [go]);
  /** 顶部导航进入发现时回到默认分区；只有下载页的收藏快捷入口会指定分区。 */
  const goTop = useCallback((key: PageKey) => {
    if (key === "discover") setDiscoverSection("servers");
    go(key);
  }, [go]);
  const goHome = useCallback(() => go("home"), [go]);

  let content;
  switch (shown) {
    case "home":
      content = <HomePage />;
      break;
    case "discover":
      content = <DiscoverPage section={discoverSection} onSectionChange={setDiscoverSection} />;
      break;
    case "download":
      content = <DownloadPage />;
      break;
    case "settings":
      content = <SettingsPage />;
      break;
    case "account":
      content = <AccountPage />;
      break;
    case "instances":
      content = (
        <InstanceManagerPage
          key={instanceTarget ?? "all"}
          initialInstanceId={instanceTarget}
        />
      );
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
    <NavigationContext.Provider value={{ goHome, openAccount, openDiscoverBookmarks, openInstanceManager }}>
      <div className="shell">
        <div className="glass-source">
          <div className="shell-scene">
            {/* 独立的壁纸层：铺满整窗，页面内容从它上面穿过 */}
            <div className="shell-bg" aria-hidden="true" />
            <main className="page-host">
              {error.length > 0 ? <div className="backend-error">{error}</div> : null}
              <div className={stageClass}>
                <div className={fadeClass}>{content}</div>
              </div>
            </main>
          </div>
        </div>

        {/* 导航层：白胶囊浮在页面之上，里面是导航按钮 */}
        <div className="nav-band" ref={bandRef}>
          {/* 白胶囊的位置由 syncSceneBar() 按真实胶囊的 rect 逐像素对齐 */}
          <div className="nav-scene-bar glass-surface glass-pill" aria-hidden="true" />
          <div className="nav-dock">
            <div className="nav-stack">
              <nav className="nav-pill" aria-label={t("nav.main")} ref={navRef}>
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
                    aria-pressed={item.key === activeKey}
                    className={`btn topnav ${item.key === activeKey ? "active" : ""}`}
                    onClick={() => goTop(item.key)}
                  >
                    <img src={item.key === activeKey ? item.iconActive : item.icon} alt="" width={16} height={16} />
                    {t(item.labelKey)}
                  </button>
                ))}
              </nav>
            </div>
          </div>
        </div>

        {/* 右上角状态入口：运行中的实例与下载任务，按需出现 */}
        <TopStatus />
      </div>
    </NavigationContext.Provider>
  );
}
