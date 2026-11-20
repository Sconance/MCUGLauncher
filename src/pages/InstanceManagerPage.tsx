import { useEffect, useRef, useState } from "react";
import { selectInstance as selectInstanceCommand } from "../api/commands/state";
import CubeIcon from "../components/CubeIcon";
import { useFadeSwap } from "../shell/fade";
import { useNavigation } from "../shell/navigation";
import { useToast } from "../shell/toast";
import { useLauncher } from "../state/launcher";
import type { InstanceInfo } from "../types/launcher";
import InstancesPage from "./InstancesPage";
import "./InstanceManagerPage.css";

function validInstances(items: InstanceInfo[]): InstanceInfo[] {
  return items.filter((item) => item.name.length > 0 && !item.isPlaceholder);
}

/**
 * 实例管理器：点击主页实例卡进入的左右栏页面。
 * 左栏：「全部实例」按钮 + 固定下来的实例标签（hover 才显示 ×，新标签有入场动画）；
 * 右栏：全部实例列表（点卡片 = 切换启动页的当前实例，hover 出设置按钮）或某个实例的设置。
 */
export default function InstanceManagerPage() {
  const { instances: instanceCatalog, overview, refreshState } = useLauncher();
  const { notify } = useToast();
  const { goHome } = useNavigation();
  const instances = instanceCatalog ? validInstances(instanceCatalog.items) : [];
  const currentId = overview?.selectedInstanceId ?? "";

  /** 固定到左栏的实例 id，顺序即标签顺序 */
  const [pinned, setPinned] = useState<string[]>([]);
  /** 正在播放移除动画的标签；动画结束后才真正从 pinned 里移除 */
  const [closing, setClosing] = useState<string[]>([]);
  const closingTimers = useRef<number[]>([]);
  /** 正在切换启动实例的 id，切换期间禁用列表避免连点 */
  const [switching, setSwitching] = useState("");

  useEffect(
    () => () => {
      closingTimers.current.forEach((timer) => window.clearTimeout(timer));
    },
    [],
  );
  /** 当前右栏显示的内容：'all' = 全部实例，其它 = 某个实例的设置 */
  const [active, setActive] = useState<string>("all");
  /** 右栏内容跟随 fade 后的 shown，切换才有淡出淡入 */
  const { shown, out } = useFadeSwap(active);

  /** 把「全部实例」里的某一项固定到左栏并打开它的设置 */
  const openInstance = (id: string) => {
    setPinned((current) => (current.includes(id) ? current : [...current, id]));
    setActive(id);
  };

  /** 点实例框本身：切换启动页的当前实例，并退回启动页 */
  const chooseInstance = async (item: InstanceInfo) => {
    if (switching.length > 0) return;
    if (item.id === currentId) {
      goHome();
      return;
    }
    setSwitching(item.id);
    try {
      await selectInstanceCommand(item.id);
      await refreshState();
      goHome();
    } catch (error) {
      notify(error instanceof Error ? error.message : String(error));
    } finally {
      setSwitching("");
    }
  };

  /** × 取消固定：标签先播移除动画，结束后才真正移除；当前页立即回「全部实例」 */
  const unpin = (id: string) => {
    if (closing.includes(id)) return;
    setActive((current) => (current === id ? "all" : current));
    setClosing((current) => (current.includes(id) ? current : [...current, id]));
    const timer = window.setTimeout(() => {
      setPinned((current) => current.filter((entry) => entry !== id));
      setClosing((current) => current.filter((entry) => entry !== id));
    }, 200);
    closingTimers.current.push(timer);
  };

  /** 右栏实例设置内部切换了实例（下拉）：保持固定列表与右栏一致 */
  const handleInnerSelect = (id: string) => {
    setPinned((current) => (current.includes(id) ? current : [...current, id]));
    setActive(id);
  };

  const current = instances.find((item) => item.id === currentId);

  return (
    <div className="extras split-page">
      <aside className="extras-nav">
        <nav className="section-nav" aria-label="实例管理">
          {/* 全部实例 */}
          <div className="section-group">
            <button
              type="button"
              className={`section-item${active === "all" ? " selected" : ""}`}
              onClick={() => setActive("all")}
            >
              <span className="section-label">全部实例</span>
            </button>
          </div>

          {/* 固定的实例标签；为空时不渲染分组，第一个标签加入时随分组一起淡入 */}
          {pinned.length > 0 ? (
            <div className="section-group im-pinned-group">
              {pinned.map((id) => {
                const item = instances.find((entry) => entry.id === id);
                if (!item) return null;
                const isClosing = closing.includes(id);
                return (
                  <div
                    key={id}
                    className={`section-item im-tab${active === id && !isClosing ? " selected" : ""}${
                      isClosing ? " is-closing" : ""
                    }`}
                  >
                    <button type="button" className="im-tab-main" onClick={() => setActive(id)}>
                      <span className="section-label">{item.displayName}</span>
                    </button>
                    {isClosing ? null : (
                      <button
                        type="button"
                        className="im-close"
                        aria-label={`取消固定 ${item.displayName}`}
                        onClick={() => unpin(id)}
                      >
                        <svg viewBox="0 0 16 16" aria-hidden="true">
                          <path
                            d="M4.5 4.5l7 7M11.5 4.5l-7 7"
                            fill="none"
                            stroke="currentColor"
                            strokeWidth="1.5"
                            strokeLinecap="round"
                          />
                        </svg>
                      </button>
                    )}
                  </div>
                );
              })}
            </div>
          ) : null}
        </nav>
      </aside>

      <div className="split-body">
        <div className={`split-content${out ? " is-out" : ""}`}>
          {shown === "all" ? (
            <div className="im-all">
              <h1 className="page-title">全部实例</h1>
              <p className="muted">
                {instances.length === 0
                  ? "尚未找到已安装实例。请先在下载页安装或导入整合包。"
                  : `共 ${instances.length} 个实例 · 当前启动：${current?.displayName ?? "未选择"} · 点击实例设为当前实例并回到启动页，悬停后点右侧按钮打开它的设置。`}
              </p>
              <div className="im-all-list">
                {instances.map((item) => {
                  const isCurrent = item.id === currentId;
                  return (
                    <div
                      key={item.id}
                      className={`im-all-item${isCurrent ? " is-current" : ""}${
                        switching === item.id ? " is-switching" : ""
                      }`}
                    >
                      <button
                        type="button"
                        className="im-all-main"
                        aria-current={isCurrent}
                        disabled={switching.length > 0}
                        onClick={() => void chooseInstance(item)}
                      >
                        <CubeIcon className="im-all-icon" />
                        <span className="im-all-text">
                          <span className="im-all-name">{item.displayName}</span>
                          <span className="im-all-sub">
                            {item.minecraft} · {item.loader} · {item.mods} 个模组
                          </span>
                        </span>
                      </button>

                      <button
                        type="button"
                        className="im-all-settings"
                        aria-label={`打开 ${item.displayName} 的实例设置`}
                        onClick={() => openInstance(item.id)}
                      >
                        <svg viewBox="0 0 32 32" aria-hidden="true">
                          <path
                            d="M22.6669 27.3334L29.3333 16L22.6669 4.66669L9.33346 4.66669L2.66666 16L9.33346 27.3334L22.6669 27.3334Z"
                            fill="none"
                            stroke="currentColor"
                            strokeWidth="2"
                            strokeLinejoin="round"
                          />
                          <path
                            d="M16 19.3334C17.8409 19.3334 19.3333 17.841 19.3333 16C19.3333 14.1591 17.8409 12.6667 16 12.6667C14.1591 12.6667 12.6667 14.1591 12.6667 16C12.6667 17.841 14.1591 19.3334 16 19.3334Z"
                            fill="none"
                            stroke="currentColor"
                            strokeWidth="2"
                            strokeLinejoin="round"
                          />
                        </svg>
                      </button>
                    </div>
                  );
                })}
              </div>
            </div>
          ) : (
            <InstancesPage selectedId={shown} onSelectInstance={handleInnerSelect} />
          )}
        </div>
      </div>
    </div>
  );
}
