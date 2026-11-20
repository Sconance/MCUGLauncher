import { useEffect, useRef, useState } from "react";
import LaunchPlayIcon from "../assets/icons/capsule/LaunchPlayIcon.svg";
import settingsIcon from "../assets/icons/capsule/SettingsIcon.svg";
import SwitchPlayerIcon from "../assets/icons/app/SwitchPlayerIcon.svg";
import closeIcon from "../assets/icons/ui/close.svg";
import SvgIcon from "../components/SvgIcon";
import { launchGame } from "../api/commands/launch";
import type { PlayerInfo } from "../types/view-models";
import { useHomeAlignment } from "../shell/homeAlignment";
import { useNavigation } from "../shell/navigation";
import { useToast } from "../shell/toast";
import { useLauncher } from "../state/launcher";
import LaunchToast from "./home/LaunchToast";
import "./HomePage.css";

/** 角色类型的中文说明；外置登录附带验证服务器名。 */
function playerTypeText(player: PlayerInfo): string {
  if (player.playerType === "microsoft") return "微软账号";
  if (player.playerType === "offline") return "离线账号";
  if (player.playerType === "mua") {
    return player.authServer.length > 0 ? `MUA 账号 · ${player.authServer}` : "MUA 账号";
  }
  return player.playerType;
}

/** 主页：底部按钮栏按设置靠右或居中排列角色、实例、启动三个按钮。 */
export default function HomePage() {
  const { openAccount, openInstanceManager } = useNavigation();
  const { notify } = useToast();
  const { overview, accounts, instances: instanceCatalog, avatar, refreshState } = useLauncher();
  const homeAlignment = useHomeAlignment();
  const [bumping, setBumping] = useState(false);
  const [ripples, setRipples] = useState<{ id: number; x: number; y: number; scale: number }[]>([]);
  /** 启动进度 toast：显示在按钮栏上方（居中时位于启动按钮正上方）；cancelled 由启动按钮的 X 触发 */
  const [launching, setLaunching] = useState<{
    id: number;
    name: string;
    task: Promise<void>;
    cancelled?: boolean;
  } | null>(null);
  /** 取消标记：启动请求回来时据此吞掉成功/失败提示 */
  const launchCancelled = useRef(false);
  const hostRef = useRef<HTMLDivElement>(null);
  const [playerPickerOpen, setPlayerPickerOpen] = useState(false);
  const [playerPickerClosing, setPlayerPickerClosing] = useState(false);

  // 角色浮层：点空白处或按 Esc 收起
  useEffect(() => {
    if (!playerPickerOpen) return;
    const close = (event: MouseEvent) => {
      if ((event.target as HTMLElement).closest(".player-card, .player-picker")) return;
      if (playerPickerClosing) return;
      setPlayerPickerClosing(true);
    };
    const onKey = (event: KeyboardEvent) => {
      if (event.key !== "Escape") return;
      if (playerPickerClosing) return;
      setPlayerPickerClosing(true);
    };
    window.addEventListener("click", close);
    window.addEventListener("keydown", onKey);
    return () => {
      window.removeEventListener("click", close);
      window.removeEventListener("keydown", onKey);
    };
  }, [playerPickerOpen, playerPickerClosing]);

  // 浮层先淡出，动画结束后再卸载
  useEffect(() => {
    if (!playerPickerClosing) return;
    const timer = window.setTimeout(() => {
      setPlayerPickerOpen(false);
      setPlayerPickerClosing(false);
    }, 140);
    return () => window.clearTimeout(timer);
  }, [playerPickerClosing]);

  const openPlayerPicker = () => {
    setPlayerPickerClosing(false);
    setPlayerPickerOpen(true);
  };

  const closePlayerPicker = () => {
    if (playerPickerOpen && !playerPickerClosing) setPlayerPickerClosing(true);
  };

  if (!overview || !accounts || !instanceCatalog) {
    return (
      <div className="page">
        <div className="empty">正在加载启动器状态…</div>
      </div>
    );
  }

  const centered = homeAlignment === "center";
  const instances = instanceCatalog.items.filter((item) => item.name.length > 0 && !item.isPlaceholder);
  const current = instances.find((item) => item.id === overview.selectedInstanceId);

  // 账号列表来自 list_accounts；头像只对当前账号可用
  const players: PlayerInfo[] =
    accounts.length > 0
      ? accounts.map((account) => ({
          id: account.id,
          name: account.player.length > 0 ? account.player : "未登录",
          playerType: account.kind,
          authServer: "",
          avatar: account.id === overview.activeAccountId ? avatar?.image ?? "" : "",
        }))
      : [
          {
            id: "current",
            name: "未登录",
            playerType: "offline",
            authServer: "",
            avatar: avatar?.image ?? "",
          },
        ];
  const selectedPlayer = players.find((item) => item.id === overview.activeAccountId) ?? players[0];

  /** 水波：以启动按钮为圆心铺开，半径覆盖整页。居中对齐时按钮在页面中线上，
   *  水平圆心直接取页面中线，水波严格左右对称。 */
  const spawnRipple = (button: HTMLElement) => {
    const host = hostRef.current;
    if (!host) return;
    const area = host.getBoundingClientRect();
    const rect = button.getBoundingClientRect();
    const x = centered
      ? area.width / 2
      : rect.left + rect.width / 2 - area.left;
    const y = rect.top + rect.height / 2 - area.top;
    const reach = Math.hypot(Math.max(x, area.width - x), Math.max(y, area.height - y));
    const id = Date.now() + Math.random();
    setRipples((previous) => [...previous, { id, x, y, scale: (reach * 2) / 20 }]);
    window.setTimeout(() => setRipples((previous) => previous.filter((item) => item.id !== id)), 900);
  };

  /** 相邻两张卡的跟随抖动：重置类名以便重复触发。 */
  const bumpCards = () => {
    setBumping(false);
    window.requestAnimationFrame(() => setBumping(true));
  };

  /** 真正发起启动：进度层只关心它什么时候 settle */
  const runLaunch = async (instanceId: string) => {
    try {
      const result = await launchGame(instanceId, overview.activeAccountId ?? "");
      if (launchCancelled.current) return;
      await refreshState();
      notify(`启动请求已接受：${result.sessionId}`);
    } catch (error) {
      if (launchCancelled.current) return;
      notify(error instanceof Error ? error.message : String(error));
    }
  };

  const launch = (button: HTMLElement) => {
    if (launching) return;
    spawnRipple(button);
    bumpCards();
    if (!current) {
      notify("请先安装或选择实例。");
      return;
    }
    launchCancelled.current = false;
    setLaunching({ id: Date.now(), name: current.name, task: runLaunch(current.id) });
  };

  /** 启动按钮在进度期间变成 X：点它取消，进度 toast 立刻滑出 */
  const cancelLaunch = () => {
    if (!launching || launching.cancelled) return;
    launchCancelled.current = true;
    setLaunching((current) => (current ? { ...current, cancelled: true } : current));
    notify("已取消启动。");
  };

  /** 角色切换目前由「账号」页完成，这里只做跳转。 */
  const choosePlayer = (id: string) => {
    closePlayerPicker();
    if (id === selectedPlayer.id) return;
    notify("在「账号」页切换登录的角色。");
    openAccount();
  };

  // 角色卡：头像 + 名称 + 账号类型，右上角切换角色
  const playerCard = (
    <div className="player-card" key="player-card">
      <div className="player-card-main">
        {selectedPlayer.avatar.length > 0 ? (
          <img className="player-card-avatar" src={selectedPlayer.avatar} alt="" />
        ) : null}
        <div className="player-card-text">
          <span className="player-card-name">{selectedPlayer.name}</span>
          <span className="muted player-card-type">{playerTypeText(selectedPlayer)}</span>
        </div>
      </div>
      <button
        type="button"
        className="btn card-action"
        data-tip="切换角色"
        aria-label="切换角色"
        aria-expanded={playerPickerOpen}
        onClick={() => {
          if (playerPickerOpen && !playerPickerClosing) closePlayerPicker();
          else openPlayerPicker();
        }}
      >
        <img src={SwitchPlayerIcon} alt="" />
      </button>

      {playerPickerOpen ? (
        <div className={`player-picker${playerPickerClosing ? " is-closing" : ""}`} role="listbox" aria-label="选择角色">
          {players.length === 0 ? (
            <p className="muted player-picker-empty">还没有登录的角色。</p>
          ) : (
            players.map((player) => (
              <button
                key={player.id}
                type="button"
                role="option"
                aria-selected={player.id === selectedPlayer.id}
                className={`player-picker-item ${player.id === selectedPlayer.id ? "selected" : ""}`}
                onClick={() => choosePlayer(player.id)}
              >
                <span className="player-picker-radio" />
                {player.avatar.length > 0 ? (
                  <img className="player-picker-avatar" src={player.avatar} alt="" />
                ) : null}
                <span className="player-picker-text">
                  <span className="player-picker-name">{player.name}</span>
                  <span className="muted">{playerTypeText(player)}</span>
                </span>
              </button>
            ))
          )}
        </div>
      ) : null}
    </div>
  );

  // 实例卡：点整块切换实例，右上角进实例设置
  const instanceBlock = (
    <div className="instance-block" key="instance-block">
      <button
        type="button"
        className="btn instance-card"
        aria-label="打开实例管理器"
        onClick={() => {
          closePlayerPicker();
          openInstanceManager();
        }}
      >
        <span className="instance-card-title">当前实例</span>
        <span className="instance-card-sub">{current ? current.name : "未选择实例"}</span>
      </button>
      <button
        type="button"
        className="btn card-action"
        data-tip="实例设置"
        aria-label="实例设置"
        onClick={() => openInstanceManager(current?.id)}
      >
        <img src={settingsIcon} alt="" />
      </button>
    </div>
  );

  // 启动：圆角正方形、纯色亮蓝、扁平，仅图标
  const launchButton = (
    <button
      type="button"
      className="btn launch-square"
      key="launch-button"
      data-tip={launching ? undefined : "启动游戏"}
      aria-label={launching ? "取消启动" : "启动游戏"}
      onClick={(event) => (launching ? cancelLaunch() : void launch(event.currentTarget))}
      disabled={!current && !launching}
    >
      {launching ? (
        <SvgIcon src={closeIcon} className="launch-cancel-icon" />
      ) : (
        <img src={LaunchPlayIcon} alt="" />
      )}
    </button>
  );

  return (
    <div className="home" ref={hostRef}>
      {/* 点击启动时的水波层，位于按钮之下、胶囊之下的同一平面 */}
      <div className="ripple-layer" aria-hidden="true">
        {ripples.map((item) => (
          <span
            key={item.id}
            className="ripple"
            style={{
              left: item.x,
              top: item.y,
              ["--ripple-scale" as string]: item.scale,
            }}
          />
        ))}
      </div>

      {/* 底部按钮栏：靠右为「角色 | 实例 | 启动」，居中为「实例 | 启动 | 角色」；
          启动进度 toast 始终浮在按钮栏上方，居中时正好在启动按钮正上方 */}
      <div className={`home-launch${centered ? " is-centered" : ""}`}>
        {launching ? (
          <LaunchToast
            key={launching.id}
            name={launching.name}
            task={launching.task}
            cancelled={launching.cancelled ?? false}
            onExit={() => setLaunching(null)}
          />
        ) : null}
        <div className={`launch-row${bumping ? " is-bumping" : ""}`} onAnimationEnd={() => setBumping(false)}>
          {centered ? (
            <>
              {instanceBlock}
              {launchButton}
              {playerCard}
            </>
          ) : (
            <>
              {playerCard}
              {instanceBlock}
              {launchButton}
            </>
          )}
        </div>
      </div>
    </div>
  );
}
