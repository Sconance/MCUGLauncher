import { useEffect, useRef, useState } from "react";
import { saveSettings } from "../api/commands/settings";
import { selectInstance as selectInstanceCommand } from "../api/commands/state";
import SvgIcon from "../components/SvgIcon";
import closeIcon from "../assets/icons/ui/close.svg";
import cubeIcon from "../assets/icons/ui/cube.svg";
import gearIcon from "../assets/icons/ui/gear.svg";
import { useFadeSwap } from "../shell/fade";
import { useNavigation } from "../shell/navigation";
import { useToast } from "../shell/toast";
import { useLauncher } from "../state/launcher";
import type { InstanceInfo } from "../types/launcher";
import InstancesPage from "./InstancesPage";
import GameDirectoryList from "./instances/GameDirectoryList";
import { getPinnedInstanceTabs, setPinnedInstanceTabs } from "./instanceTabs";
import "./InstanceManagerPage.css";

function validInstances(items: InstanceInfo[]): InstanceInfo[] {
  return items.filter((item) => item.name.length > 0 && !item.isPlaceholder);
}

/**
 * 实例管理器：点击主页实例卡进入的左右栏页面。
 * 从游戏目录进入当前目录的实例清单，也可通过固定标签打开实例设置。
 */
interface InstanceManagerPageProps {
  /** 进入页面时直接展开设置的实例；不传则显示游戏目录。 */
  initialInstanceId?: string;
}

export default function InstanceManagerPage({ initialInstanceId }: InstanceManagerPageProps) {
  const { instances: instanceCatalog, overview, settings, resourceVersions, refreshState, refreshSettingsResources } = useLauncher();
  const { notify } = useToast();
  const { goHome } = useNavigation();
  const instances = instanceCatalog ? validInstances(instanceCatalog.items) : [];
  const currentId = overview?.selectedInstanceId ?? "";

  /** 固定到左栏的实例 id，顺序即标签顺序；重新进入页面时从模块级 store 恢复 */
  const [pinned, setPinned] = useState<string[]>(() => {
    const stored = [...getPinnedInstanceTabs()];
    if (!initialInstanceId || stored.includes(initialInstanceId)) return stored;
    return [...stored, initialInstanceId];
  });
  /** 离开页面后仍保留已打开的标签，下次进入继续显示 */
  useEffect(() => {
    setPinnedInstanceTabs(pinned);
  }, [pinned]);
  /** 正在播放移除动画的标签；动画结束后才真正从 pinned 里移除 */
  const [closing, setClosing] = useState<string[]>([]);
  const closingTimers = useRef<number[]>([]);
  /** 正在切换启动实例的 id，切换期间禁用列表避免连点 */
  const [switching, setSwitching] = useState("");
  const [switchingRoot, setSwitchingRoot] = useState(false);

  useEffect(
    () => () => {
      closingTimers.current.forEach((timer) => window.clearTimeout(timer));
    },
    [],
  );
  const [active, setActive] = useState<string>(initialInstanceId ?? "roots");
  /** 右栏内容跟随 fade 后的 shown，切换才有淡出淡入 */
  const { shown, out } = useFadeSwap(active);

  const openGameRoot = async (path: string) => {
    if (switchingRoot) return;
    setSwitchingRoot(true);
    try {
      const result = await saveSettings({ root: path }, resourceVersions.settings?.revision ?? 0);
      await refreshSettingsResources(result.changed);
      setActive("all");
    } catch (error) {
      notify(error instanceof Error ? error.message : String(error));
    } finally {
      setSwitchingRoot(false);
    }
  };

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
          <div className="section-group">
            <button
              type="button"
              className={`section-item${active === "roots" ? " selected" : ""}`}
              onClick={() => setActive("roots")}
            >
              <span className="section-label">游戏目录</span>
            </button>
            <button
              type="button"
              className={`section-item${active === "all" ? " selected" : ""}`}
              onClick={() => setActive("all")}
            >
              <span className="section-label">目录内实例</span>
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
                        <SvgIcon src={closeIcon} />
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
          {shown === "roots" ? (
            <GameDirectoryList
              roots={settings?.gameRoots ?? []}
              activeRoot={settings?.root ?? ""}
              pending={switchingRoot || !settings}
              onOpen={(path) => void openGameRoot(path)}
            />
          ) : shown === "all" ? (
            <div className="im-all">
              <h1 className="page-title">目录内实例</h1>
              {settings?.root ? <p className="muted im-directory-path">{settings.root}</p> : null}
              <p className="muted">
                {instances.length === 0
                  ? "当前游戏目录中未找到已安装实例。"
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
                        <SvgIcon src={cubeIcon} className="im-all-icon" />
                        <span className="im-all-text">
                          <span className="im-all-name">{item.displayName}</span>
                          <span className="im-all-sub">
                            {item.minecraft} · {item.loader}
                          </span>
                        </span>
                      </button>

                      <button
                        type="button"
                        className="im-all-settings"
                        aria-label={`打开 ${item.displayName} 的实例设置`}
                        onClick={() => openInstance(item.id)}
                      >
                        <SvgIcon src={gearIcon} />
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
