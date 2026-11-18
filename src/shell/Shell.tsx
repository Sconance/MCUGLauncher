import { useCallback, useEffect, useLayoutEffect, useRef, useState } from "react";
import wallpaperUrl from "../assets/wallpaper/TmpBackground.png";
import DiscoverIcon from "../assets/icons/capsule/DiscoverIcon.svg";
import DiscoverIconActive from "../assets/icons/capsule/DiscoverIconActive.svg";
import downloadIcon from "../assets/icons/capsule/DownloadIcon.svg";
import downloadIconActive from "../assets/icons/capsule/DownloadIconActive.svg";
import extraIcon from "../assets/icons/capsule/ExtraIcon.svg";
import extraIconActive from "../assets/icons/capsule/ExtraIconActive.svg";
import launchIcon from "../assets/icons/capsule/LaunchIcon.svg";
import launchIconActive from "../assets/icons/capsule/LaunchIconActive.svg";
import settingsIcon from "../assets/icons/capsule/SettingsIcon.svg";
import settingsIconActive from "../assets/icons/capsule/SettingsIconActive.svg";
import DiscoverPage from "../pages/DiscoverPage";
import DownloadPage from "../pages/DownloadPage";
import ExtrasPage from "../pages/ExtrasPage";
import HomePage from "../pages/HomePage";
import SettingsPage from "../pages/SettingsPage";
import GlassSurface from "../components/GlassSurface";
import { useLiquidGlass } from "./liquid";
import { useLauncher } from "../state/launcher";
import { useI18n } from "../i18n";
import { NavigationContext } from "./navigation";

type PageKey = "home" | "discover" | "download" | "settings" | "extras";

const NAV: { key: PageKey; labelKey: string; icon: string; iconActive: string }[] = [
  { key: "home", labelKey: "nav.home", icon: launchIcon, iconActive: launchIconActive },
  { key: "discover", labelKey: "nav.discover", icon: DiscoverIcon, iconActive: DiscoverIconActive },
  { key: "download", labelKey: "nav.download", icon: downloadIcon, iconActive: downloadIconActive },
  { key: "settings", labelKey: "nav.settings", icon: settingsIcon, iconActive: settingsIconActive },
  { key: "extras", labelKey: "nav.extras", icon: extraIcon, iconActive: extraIconActive },
];

/**
 * 液态玻璃按压的弹簧参数（半隐式欧拉，两步一半步长；写法借自 liquid-glass-react 的
 * glass-lens 预设，数值按这里要的手感调硬：移动 900/42，长出 800/38）。
 * 用弹簧而不是 CSS 过渡：到位快、末端只有很小的回弹，不会像 ease 那样越走越慢。
 */
const MOVE_SPRING = { stiffness: 900, damping: 42 };
const GROW_SPRING = { stiffness: 800, damping: 38 };

type Spring = { value: number; velocity: number; target: number };
const createSpring = (value = 0): Spring => ({ value, velocity: 0, target: value });

function springSnap(spring: Spring, value = spring.target) {
  spring.value = spring.target = value;
  spring.velocity = 0;
}

function stepSpring(spring: Spring, stiffness: number, damping: number, dt: number) {
  for (let i = 0; i < 2; i++) {
    const h = dt / 2;
    spring.velocity += (-stiffness * (spring.value - spring.target) - damping * spring.velocity) * h;
    spring.value += spring.velocity * h;
  }
}

const springSettled = (spring: Spring, distance: number, speed: number) =>
  Math.abs(spring.value - spring.target) <= distance && Math.abs(spring.velocity) <= speed;

const prefersReducedMotion = () =>
  typeof window !== "undefined" && window.matchMedia("(prefers-reduced-motion: reduce)").matches;


/** 应用外壳：悬浮胶囊导航 + 页面宿主 + 后端错误提示（Toast 由 App 统一提供）。 */
export default function Shell() {
  const { error } = useLauncher();
  const { locale, t } = useI18n();
  const liquid = useLiquidGlass();

  const [page, setPage] = useState<PageKey>("home");
  /**
   * 导航按钮的「高亮」状态：页面可以立刻切（`page`），但按钮的图标/文字颜色要等玻璃滑到位再变，
   * 所以按钮外观单独用 activeKey，默认跟着 page 走，只有玻璃动画期间被延后。
   */
  const [activeKey, setActiveKey] = useState<PageKey>("home");
  const pendingActive = useRef<PageKey | null>(null);
  const [extrasSection, setExtrasSection] = useState("instances");

  // 页面切换的淡出淡入：page 是选中态（胶囊高亮立刻跟随），shown 是正在渲染的页
  const [shown, setShown] = useState<PageKey>("home");
  const [phase, setPhase] = useState<"idle" | "out" | "in">("idle");
  const [dismissPanel, setDismissPanel] = useState(false); // 回「启动」时让内容栏随内容一起淡出
  const pageRef = useRef<PageKey>("home");

  // 选中背景用一枚独立的滑块，位置与宽度跟随当前按钮
  const navRef = useRef<HTMLDivElement>(null);
  /**
   * 玻璃的采样条：只覆盖窗口顶部一条（胶囊 + 上下各一圈），里面放
   * 「预渲染好的壁纸条 + 白胶囊 + 导航文字」。库要把这块 DOM 拍快照，
   * 所以它必须又小又不含那张 4.4MB 的原始壁纸，否则每次按压会卡 100~250ms。
   */
  const bandRef = useRef<HTMLDivElement>(null);
  const shellRef = useRef<HTMLDivElement>(null);
  const bandImage = useRef<HTMLImageElement | null>(null);
  const bandCanvas = useRef<HTMLCanvasElement | null>(null);
  const [indicator, setIndicator] = useState({ left: 0, width: 0 });
  const [slide, setSlide] = useState(false);

  /*
   * FlClash 的做法：胶囊始终是白色磨砂，只有按住的那颗按钮浮出一层液态玻璃。
   * 灰胶囊 → 玻璃 → 跟着指针走 → 松手缩回灰胶囊，全程只让一枚元素在动：
   * 玻璃的初始/结束尺寸就是灰胶囊（--lens-pill-sx/sy），显隐交给 CSS 的 transform /
   * visibility（不能动 opacity，否则 Chromium 会把祖先当成 backdrop root，折射整块失效）。
   */
  const [pressing, setPressing] = useState(false);
  const [settling, setSettling] = useState(false);
  /** 玻璃中心的弹簧（px）与「长成玻璃」的弹簧（0=灰胶囊尺寸，1=完整玻璃）。 */
  const lensX = useRef(createSpring());
  const lensGrow = useRef(createSpring());
  const frameId = useRef(0);
  const lastFrame = useRef(0);
  /** 松手后：玻璃先滑到目标，到位才换高亮并把 grow 收回去。 */
  const releasing = useRef(false);
  const releaseTarget = useRef<PageKey | null>(null);
  const activeApplied = useRef(false);
  const lensRef = useRef<HTMLSpanElement>(null);
  const lensMetrics = useRef<{ nav: DOMRect; width: number; height: number; pillWidth: number; pillHeight: number } | null>(null);
  const lensMove = useRef<((event: PointerEvent) => void) | null>(null);
  const lensRelease = useRef<((event: PointerEvent) => void) | null>(null);
  const pressRef = useRef<PageKey | null>(null);

  /**
   * 采样条里的白胶囊必须和真实胶囊逐像素重合：WebGL 拍的是 `.nav-band`，
   * 玻璃才能折射到「壁纸条 + 白胶囊」，而不是只剩壁纸。
   */
  const syncSceneBar = useCallback(() => {
    const band = bandRef.current;
    const rect = navRef.current?.getBoundingClientRect();
    if (!band || !rect) return;
    band.style.setProperty("--nav-bar-left", `${Math.round(rect.left)}px`);
    band.style.setProperty("--nav-bar-top", `${Math.round(rect.top)}px`);
    band.style.setProperty("--nav-bar-w", `${Math.round(rect.width)}px`);
    band.style.setProperty("--nav-bar-h", `${Math.round(rect.height)}px`);
  }, []);

  /**
   * 采样条的背景：把壁纸按 `.shell-bg` 的 `center/cover` 规则裁出顶部一条，
   * 画进 canvas 后转成 PNG data URL。这样 html-to-image 每次只需要内联一张
   * 十几万字节的小图，而不是那张 4.4MB 的原图（实测能把单次快照从 275ms 压到个位数毫秒）。
   */
  const paintBandBackground = useCallback(() => {
    const band = bandRef.current;
    const shell = shellRef.current;
    const img = bandImage.current;
    if (!band || !shell || !img || !img.naturalWidth) return;
    const w = shell.clientWidth;
    const h = shell.clientHeight;
    const bandH = band.clientHeight;
    if (w <= 0 || h <= 0 || bandH <= 0) return;
    const dpr = Math.min(2, window.devicePixelRatio || 1);
    const canvas = bandCanvas.current ?? (bandCanvas.current = document.createElement("canvas"));
    canvas.width = Math.max(1, Math.round(w * dpr));
    canvas.height = Math.max(1, Math.round(bandH * dpr));
    const ctx = canvas.getContext("2d");
    if (!ctx) return;
    ctx.clearRect(0, 0, canvas.width, canvas.height);
    // 与 .shell-bg 的 center / cover 完全一致
    const scale = Math.max(w / img.naturalWidth, h / img.naturalHeight);
    const drawW = img.naturalWidth * scale;
    const drawH = img.naturalHeight * scale;
    const offsetX = (w - drawW) / 2;
    const offsetY = (h - drawH) / 2;
    ctx.drawImage(img, offsetX * dpr, offsetY * dpr, drawW * dpr, drawH * dpr);
    band.style.backgroundImage = `url(${canvas.toDataURL("image/png")})`;
  }, []);

  // 壁纸只需要加载一次；载入后把采样条铺好
  useEffect(() => {
    const img = new Image();
    img.decoding = "async";
    img.src = wallpaperUrl;
    const ready = () => {
      bandImage.current = img;
      paintBandBackground();
    };
    if (img.complete && img.naturalWidth > 0) ready();
    else img.addEventListener("load", ready, { once: true });
    return () => img.removeEventListener("load", ready);
  }, [paintBandBackground]);

  // 首帧先对齐一次；再等一帧（图标/字体加载完按钮宽度会变一点，采样条高度也要等到布局完成）
  useLayoutEffect(() => {
    syncSceneBar();
    paintBandBackground();
    const frame = window.requestAnimationFrame(() => {
      syncSceneBar();
      paintBandBackground();
    });
    return () => window.cancelAnimationFrame(frame);
  }, [syncSceneBar, paintBandBackground]);

  useLayoutEffect(() => {
    const active = navRef.current?.querySelector<HTMLButtonElement>(`button[data-page="${page}"]`);
    if (!active) return;
    setIndicator({ left: active.offsetLeft, width: active.offsetWidth });
  }, [page]);

  // 切语言后文案长度变了：胶囊宽度与选中底都要重新量一次，否则文字会溢出白底
  useLayoutEffect(() => {
    const frame = window.requestAnimationFrame(() => {
      syncSceneBar();
      paintBandBackground();
      const active = navRef.current?.querySelector<HTMLButtonElement>(`button[data-page="${pageRef.current}"]`);
      if (active) setIndicator({ left: active.offsetLeft, width: active.offsetWidth });
    });
    return () => window.cancelAnimationFrame(frame);
  }, [locale, syncSceneBar, paintBandBackground]);

  // 按钮高亮默认跟着页面走；只有玻璃动画期间（pendingActive 非空）延后到玻璃到位
  useEffect(() => {
    if (pendingActive.current === null) setActiveKey(page);
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

  /** 把玻璃中心夹在胶囊（含两侧各 8px 余量）里。 */
  const clampLensCenter = useCallback((clientX: number) => {
    const metrics = lensMetrics.current;
    if (!metrics) return clientX;
    const half = metrics.width / 2;
    const min = metrics.nav.left - 8 + half;
    const max = metrics.nav.right + 8 - half;
    return Math.min(Math.max(clientX, min), max);
  }, []);

  /**
   * 按两条弹簧的当前值摆玻璃：中心位置 + 从「灰胶囊尺寸」到「完整玻璃」的缩放。
   *
   * 用 `transform: scale()` 而不是逐帧改 width/height：canvas 固定按「完整玻璃」的尺寸渲染，
   * 弹簧只缩放它（always ≤ 1，是缩小，不会糊）；逐帧改盒子会让库每帧重算 canvas 尺寸，
   * 实测把 120Hz 压到 60Hz。
   */
  const paintLens = useCallback(() => {
    const element = lensRef.current;
    const metrics = lensMetrics.current;
    if (!element || !metrics) return;
    const grow = Math.min(1, Math.max(0, lensGrow.current.value));
    const width = metrics.pillWidth + (metrics.width - metrics.pillWidth) * grow;
    const height = metrics.pillHeight + (metrics.height - metrics.pillHeight) * grow;
    const centerX = lensX.current.value;
    const centerY = metrics.nav.top + metrics.nav.height / 2;
    element.style.width = `${metrics.width}px`;
    element.style.height = `${metrics.height}px`;
    element.style.left = `${(centerX - metrics.width / 2).toFixed(2)}px`;
    element.style.top = `${(centerY - metrics.height / 2).toFixed(2)}px`;
    element.style.transform = `scale(${(width / metrics.width).toFixed(4)}, ${(height / metrics.height).toFixed(4)})`;
    element.style.visibility = "visible";
    element.style.display = "";
    /*
     * 收起时用 opacity 隐藏：这样 WebGL 的画布一直活着、采样条的快照也一直是热的，
     * 按下那一刻不用重建 canvas / 重拍 DOM（display:none 会让库判 offscreen 并丢掉画布）。
     * WebGL 路径里没有 backdrop-filter，所以这里用 opacity 不会出现 SVG 路径那种「先白后糊」。
     */
    element.style.opacity = grow > 0.01 ? "1" : "0";
  }, []);

  /**
   * 量胶囊和按钮。玻璃固定为「按钮宽 + 14、胶囊高 + 10（上下各高出 5）」——
   * 只比栏厚一点点，读起来是「栏上浮起一层镜片」，而不是另一枚更大的胶囊。
   * 只在这些值真的变了才写回（换尺寸会让库重建位移贴图，能在按下那一刻省掉一次重算）。
   */
  const measureLens = useCallback((target?: HTMLElement) => {
    const nav = navRef.current?.getBoundingClientRect();
    const button = target ?? navRef.current?.querySelector<HTMLElement>("button[data-page]");
    const element = lensRef.current;
    if (!nav || !button || !element) return null;
    const buttonWidth = Math.round(button.getBoundingClientRect().width);
    const width = buttonWidth + 14;
    const height = Math.round(nav.height) + 10;
    const top = Math.round(nav.top) - 5;
    if (element.style.width !== `${width}px`) element.style.width = `${width}px`;
    if (element.style.height !== `${height}px`) element.style.height = `${height}px`;
    if (element.style.top !== `${top}px`) element.style.top = `${top}px`;
    // 缩到「按钮宽 - 4、胶囊高 - 4」时正好盖住灰胶囊（与 .nav-indicator 的内缩一致）
    const inset = 2;
    lensMetrics.current = {
      nav,
      width,
      height,
      pillWidth: buttonWidth - inset * 2,
      pillHeight: Math.round(nav.height) - inset * 2,
    };
    return lensMetrics.current;
  }, []);

  /** 摘掉按压期间挂的监听。 */
  const clearPressHandlers = useCallback(() => {
    if (lensMove.current) {
      window.removeEventListener("pointermove", lensMove.current);
      lensMove.current = null;
    }
    if (lensRelease.current) {
      window.removeEventListener("pointerup", lensRelease.current);
      window.removeEventListener("pointercancel", lensRelease.current);
      lensRelease.current = null;
    }
  }, []);

  const stopFrame = useCallback(() => {
    if (frameId.current) {
      window.cancelAnimationFrame(frameId.current);
      frameId.current = 0;
    }
  }, []);

  /** 弹簧循环：跑到两条弹簧都静止为止；到位那一刻换高亮、开始收回。 */
  const startFrame = useCallback(() => {
    if (frameId.current) return;
    lastFrame.current = 0;
    const tick = (now: number) => {
      const dt = Math.min(0.032, lastFrame.current ? (now - lastFrame.current) / 1000 : 1 / 60);
      lastFrame.current = now;
      stepSpring(lensX.current, MOVE_SPRING.stiffness, MOVE_SPRING.damping, dt);
      stepSpring(lensGrow.current, GROW_SPRING.stiffness, GROW_SPRING.damping, dt);
      if (releasing.current && springSettled(lensX.current, 2.5, 70)) {
        lensGrow.current.target = 0; // 到位：开始缩回灰胶囊
        if (!activeApplied.current) {
          activeApplied.current = true;
          const key = releaseTarget.current;
          pendingActive.current = null;
          if (key) setActiveKey(key);
          setPressing(false);
          setSettling(true);
        }
      }
      // 长按不保留玻璃：到位后自己收回（不换高亮，等松手再切页）
      if (!releasing.current && pressRef.current !== null && lensGrow.current.target === 1 && springSettled(lensX.current, 2.5, 60)) {
        lensGrow.current.target = 0;
        setPressing(false);
        setSettling(true);
      }

      const moving = !springSettled(lensX.current, 0.6, 8) || !springSettled(lensGrow.current, 0.02, 0.1);
      paintLens();
      if (moving) {
        frameId.current = window.requestAnimationFrame(tick);
        return;
      }
      frameId.current = 0;
      springSnap(lensX.current);
      springSnap(lensGrow.current, 0);
      releasing.current = false;
      activeApplied.current = false;
      releaseTarget.current = null;
      setPressing(false);
      setSettling(false);
      paintLens();
    };
    frameId.current = window.requestAnimationFrame(tick);
  }, [paintLens]);

  /** 直接取消按压（窗口 resize、关开关、卸载）：不做收回动画，立刻归位。 */
  const abortPress = useCallback(() => {
    clearPressHandlers();
    stopFrame();
    pressRef.current = null;
    pendingActive.current = null;
    releasing.current = false;
    activeApplied.current = false;
    releaseTarget.current = null;
    springSnap(lensGrow.current, 0);
    springSnap(lensX.current, lensX.current.value);
    setPressing(false);
    setSettling(false);
    paintLens();
  }, [clearPressHandlers, paintLens, stopFrame]);

  /**
   * 松手：
   * 1. 页面**立刻**切走（内容先换），按钮的高亮先不动；
   * 2. 玻璃沿弹簧滑到目标按钮中心（到位快，末端只有很小的回弹）；
   * 3. **到位之后**才切换导航高亮（图标/文字颜色在这一刻变），随后弹簧缩回灰胶囊。
   */
  const settlePress = useCallback(
    (clientX: number, clientY: number) => {
      clearPressHandlers();
      pressRef.current = null;

      if (!liquid) {
        setPressing(false);
        return;
      }

      const released = document.elementFromPoint(clientX, clientY)?.closest<HTMLElement>("button[data-page]");
      const target = released && navRef.current?.contains(released) ? released : null;
      const targetKey = (target?.dataset.page ?? null) as PageKey | null;

      // 页面先切：内容立刻开始淡出淡入；按钮高亮等玻璃到位
      if (targetKey) {
        pendingActive.current = targetKey;
        releaseTarget.current = targetKey;
        go(targetKey);
      }

      if (!target) {
        // 松在胶囊外：原地缩回去
        pendingActive.current = null;
        releasing.current = false;
        lensGrow.current.target = 0;
        startFrame();
        return;
      }

      const rect = target.getBoundingClientRect();
      if (prefersReducedMotion()) {
        springSnap(lensX.current, clampLensCenter(rect.left + rect.width / 2));
        springSnap(lensGrow.current, 0);
        if (targetKey) setActiveKey(targetKey);
        pendingActive.current = null;
        startFrame();
        return;
      }

      releasing.current = true;
      activeApplied.current = false;
      lensX.current.target = clampLensCenter(rect.left + rect.width / 2);
      startFrame();
    },
    [clearPressHandlers, clampLensCenter, go, liquid, startFrame],
  );

  /**
   * 按下：玻璃在**已选中**的按钮上长出来，同时弹簧滑向按下的位置；按住拖拽时跟手。
   */
  const startPress = useCallback(
    (button: HTMLElement, clientX: number) => {
      if (!liquid || !measureLens(button)) return;
      clearPressHandlers();
      stopFrame();
      setSettling(false);
      pendingActive.current = null;
      setActiveKey(page);
      const element = lensRef.current;
      if (!element) return;

      const selected = navRef.current?.querySelector<HTMLElement>(`button[data-page="${page}"]`) ?? button;
      const selectedRect = selected.getBoundingClientRect();
      springSnap(lensX.current, selectedRect.left + selectedRect.width / 2);
      springSnap(lensGrow.current, 0);
      setPressing(true);

      if (prefersReducedMotion()) {
        springSnap(lensGrow.current, 1);
        springSnap(lensX.current, clampLensCenter(clientX));
        paintLens();
      } else {
        // 起点先落位，再让两条弹簧跑：grow 立刻长，x 追按下的位置
        paintLens();
        lensGrow.current.target = 1;
        lensX.current.target = clampLensCenter(clientX);
        startFrame();
      }

      // 松手挂到 window 上：指针拖到胶囊外面也照样收玻璃，不会被 pointerleave 提前打断
      const onMove = (event: PointerEvent) => {
        lensX.current.target = clampLensCenter(event.clientX);
        if (prefersReducedMotion()) {
          springSnap(lensX.current);
          paintLens();
        }
      };
      const onRelease = (event: PointerEvent) => settlePress(event.clientX, event.clientY);
      lensMove.current = onMove;
      lensRelease.current = onRelease;
      window.addEventListener("pointermove", onMove);
      window.addEventListener("pointerup", onRelease);
      window.addEventListener("pointercancel", onRelease);
    },
    [clearPressHandlers, clampLensCenter, liquid, measureLens, page, paintLens, settlePress, startFrame, stopFrame],
  );

  // 液态玻璃开启时先把玻璃量好摆在胶囊中间（隐藏态），首次按压不必现算贴图
  useLayoutEffect(() => {
    if (!liquid) return;
    const metrics = measureLens();
    if (!metrics) return;
    springSnap(lensX.current, metrics.nav.left + metrics.nav.width / 2);
    springSnap(lensGrow.current, 0);
    paintLens();
  }, [liquid, measureLens, paintLens]);

  // 窗口尺寸变了：重新量胶囊（同时把玻璃收回去），采样条的白胶囊与壁纸条跟着对齐
  useEffect(() => {
    const onResize = () => {
      syncSceneBar();
      paintBandBackground();
      if (!liquid) return;
      abortPress();
      measureLens();
    };
    window.addEventListener("resize", onResize);
    return () => window.removeEventListener("resize", onResize);
  }, [abortPress, liquid, measureLens, paintBandBackground, syncSceneBar]);

  // 关闭开关时立刻收回玻璃，不留一枚按住的玻璃在栏上
  useEffect(() => {
    if (!liquid) abortPress();
  }, [abortPress, liquid]);

  // 卸载时摘掉可能还挂着的 window 监听并停掉弹簧循环
  useEffect(
    () => () => {
      clearPressHandlers();
      stopFrame();
    },
    [clearPressHandlers, stopFrame],
  );

  const stageClass = [
    "page-stage",
    shown === "home" ? "plain" : "panel",
    liquid && shown !== "home" ? "is-liquid" : "",
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
      <div className="shell" ref={shellRef}>
        {/*
          壁纸 + 页面：只是「看得见」的那一层，已经不参与折射采样了（太大、太慢）。
        */}
        <div className="glass-source">
        <div className="shell-scene">
          {/* 独立的壁纸层：铺满整窗，页面内容从它上面穿过 */}
          <div className="shell-bg" aria-hidden="true" />
          <main className="page-host">
            {error.length > 0 ? <div className="backend-error">{error}</div> : null}
            <div className={stageClass}>
              {/*
                液态玻璃只作为底层玻璃板，内容始终挂在同一个 .page-fade 上。
                这样开关切换不会重挂页面组件（分区状态、滚动位置都保留），
                内容的内边距与布局也与磨砂模式完全一致。
              */}
              {liquid && shown !== "home" ? (
                <div className="page-glass-layer" aria-hidden="true">
                  <GlassSurface className="page-glass" radius={18} />
                </div>
              ) : null}
              <div className={fadeClass}>{content}</div>
            </div>
          </main>
        </div>
        </div>

        {/*
          玻璃的采样条：窗口顶部 64px 一条，里面是「预渲染的壁纸条 + 白胶囊 + 导航文字」。
          库要把这块 DOM 拍成快照再折射，所以它必须小、且不含那张 4.4MB 的原图 ——
          这是本轮性能修复的核心（之前采整窗，单次快照 100~275ms，按压必然卡顿）。
          条高固定 64px：胶囊在 10~44，镜片最多到 y≈61，再高就会盖住 64px 起的面板页。
        */}
        <div className="nav-band" ref={bandRef}>
          {/* 采样条里的白胶囊：位置由 syncSceneBar() 按真实胶囊的 rect 逐像素对齐 */}
          <div className="nav-scene-bar" aria-hidden="true" />
          <div className="nav-dock">
            <div className="nav-stack">
              <nav
                className={`nav-pill${pressing ? " is-pressing" : ""}${settling ? " is-settling" : ""}`}
                aria-label={t("nav.main")}
                ref={navRef}
              >
              <span
                className={`nav-indicator${
                  // 液态玻璃下由玻璃负责动，灰胶囊只在收回结束后淡入，不再滑
                  slide && !(liquid && (pressing || settling)) ? " slide" : ""
                }`}
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
                  onPointerDown={(event) => {
                    pressRef.current = item.key;
                    if (!liquid) return; // 关闭液态玻璃时保持原来的纯按钮行为
                    startPress(event.currentTarget, event.clientX);
                  }}
                  onPointerUp={() => {
                    // 液态玻璃模式下，跳转由 settlePress 在「玻璃滑到位」之后处理
                    if (liquid) return;
                    // 按住 A 拖到 B 再松开：click 不会落在 B 上，这里补一次跳转
                    if (pressRef.current !== null && pressRef.current !== item.key) go(item.key);
                    pressRef.current = null;
                  }}
                  onClick={(event) => {
                    // 键盘激活（detail === 0）直接跳；指针点击交给 settlePress 按动画节奏处理
                    if (liquid && event.detail > 0) return;
                    go(item.key);
                  }}
                >
                  <img src={item.key === activeKey ? item.iconActive : item.icon} alt="" width={16} height={16} />
                  {t(item.labelKey)}
                </button>
              ))}
            </nav>
          </div>
        </div>
        </div>

        {/*
          按压时浮起的液态玻璃：比胶囊高出一圈，**盖在胶囊与按钮文字之上**，
          文字跟着一起被折射（所以中间不能糊：blur 0、只有边缘弯）。
          它挂在采样条外面，否则拍不到自己盖住的文字；pointer-events: none 不挡点击。
        */}
        <span className={`nav-press-glass${pressing ? " is-on" : ""}`} ref={lensRef} aria-hidden="true">
          {liquid ? <GlassSurface className="nav-press-glass-lens" radius={24} renderer="webgl" backdropSelector=".nav-band" optics="native" refraction="lens" lensProfile="material" lens="rim" displacementScale={9} lensOptions={{ depth: 0.22, curvature: 0.18, bendWidth: 0.10, sheen: 0.5, specular: 0.7 }} glassColor="rgba(255,255,255,0.06)" borderColor="rgba(255,255,255,0.9)" border={0.02} frost={0} blur={0} saturation={180} quality="high" maxScale={2} /> : null}
          {/* 灰胶囊 ↔ 玻璃的过渡色：按下时淡出、松手时淡回 */}
          <span className="nav-press-glass-tint" aria-hidden="true" />
          <span className="nav-press-glass-rim" />
        </span>
      </div>
    </NavigationContext.Provider>
  );
}
