import { useEffect, useState } from "react";
import InstanceSettingsIcon from "../assets/InstanceSettingsIcon.svg";
import SwitchInstanceIcon from "../assets/SwitchInstanceIcon.svg";
import SwitchPlayerIcon from "../assets/SwitchPlayerIcon.svg";
import { launchGame } from "../api/commands/launch";
import { selectInstance as selectInstanceCommand } from "../api/commands/state";
import type { PlayerInfo } from "../types/launcher";
import { useNavigation } from "../shell/navigation";
import { useToast } from "../shell/toast";
import { useLauncher } from "../state/launcher";
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

/** 主页：右下角是角色卡与启动按钮；服务器、公告、推荐与语录在「发现」。 */
export default function HomePage() {
  const { openAccount, openInstances } = useNavigation();
  const { notify } = useToast();
  const { overview, accounts, instances: instanceCatalog, avatar, refreshState } = useLauncher();
  const [status, setStatus] = useState("");
  const [pickerOpen, setPickerOpen] = useState(false);
  const [playerPickerOpen, setPlayerPickerOpen] = useState(false);

  // 实例/角色浮层：点空白处或按 Esc 一起收起
  useEffect(() => {
    if (!pickerOpen && !playerPickerOpen) return;
    const close = (event: MouseEvent) => {
      if ((event.target as HTMLElement).closest(".launch-tools, .instance-picker, .player-card, .player-picker")) return;
      setPickerOpen(false);
      setPlayerPickerOpen(false);
    };
    const onKey = (event: KeyboardEvent) => {
      if (event.key !== "Escape") return;
      setPickerOpen(false);
      setPlayerPickerOpen(false);
    };
    window.addEventListener("click", close);
    window.addEventListener("keydown", onKey);
    return () => {
      window.removeEventListener("click", close);
      window.removeEventListener("keydown", onKey);
    };
  }, [pickerOpen, playerPickerOpen]);

  if (!overview || !accounts || !instanceCatalog) {
    return (
      <div className="page">
        <div className="empty">正在加载启动器状态…</div>
      </div>
    );
  }

  const instances = instanceCatalog.items.filter((item) => item.name.length > 0 && !item.isPlaceholder);
  const current = instances.find((item) => item.id === overview.selectedInstanceId);
  const launchSubtitle = current ? current.name : "未选择实例";

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

  /** 选中一个实例后收起浮层。 */
  const chooseInstance = async (instanceId: string) => {
    setPickerOpen(false);
    if (instanceId === current?.id) return;
    const result = await selectInstanceCommand(instanceId);
    await refreshState();
    setStatus(result.message);
  };

  const launch = async () => {
    if (!current) {
      setStatus("请先安装或选择实例。");
      return;
    }
    setStatus("正在准备启动…");
    const result = await launchGame(current.id, overview.activeAccountId ?? "");
    await refreshState();
    setStatus(`启动请求已接受：${result.sessionId}`);
  };

  /** 角色切换目前由「账号」页完成，这里只做跳转。 */
  const choosePlayer = (id: string) => {
    setPlayerPickerOpen(false);
    if (id === selectedPlayer.id) return;
    notify("在「账号」页切换登录的角色。");
    openAccount();
  };

  return (
    <div className="home">
      {/* 右下角的启动区 */}
      <div className="home-launch">
        {status.length > 0 ? <p className="status home-status">{status}</p> : null}

        <div className="launch-row">
          {/* 角色卡：头像 + 名称 + 账号类型，右上角切换角色 */}
          <div className="player-card">
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
              className="btn player-switch"
              data-tip="切换角色"
              aria-label="切换角色"
              aria-expanded={playerPickerOpen}
              onClick={() => {
                setPickerOpen(false);
                setPlayerPickerOpen((open) => !open);
              }}
            >
              <img src={SwitchPlayerIcon} alt="" />
            </button>

            {playerPickerOpen ? (
              <div className="player-picker" role="listbox" aria-label="选择角色">
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

          <div className="launch-block">
            <button type="button" className="btn launch" onClick={() => void launch()} disabled={!current}>
              <span className="stack" style={{ gap: 2, alignItems: "center" }}>
                <span className="launch-title">启动游戏</span>
                <span className="launch-subtitle">{launchSubtitle}</span>
              </span>
            </button>

            {/* 叠在启动按钮右上角的快捷键：外观是一整块，点击区域各自独立 */}
            <div className="launch-tools">
              <button
                type="button"
                className="btn launch-tool"
                data-tip="切换实例"
                data-tip-side="left"
                aria-label="切换实例"
                aria-expanded={pickerOpen}
                onClick={() => {
                  setPlayerPickerOpen(false);
                  setPickerOpen((open) => !open);
                }}
              >
                <img src={SwitchInstanceIcon} alt="" />
              </button>
              <button
                type="button"
                className="btn launch-tool"
                data-tip="实例设置"
                data-tip-side="left"
                aria-label="实例设置"
                onClick={openInstances}
              >
                <img src={InstanceSettingsIcon} alt="" />
              </button>
            </div>

            {/* 实例选择浮层：从启动按钮上方展开，实例多时内部滚动 */}
            {pickerOpen ? (
              <div className="instance-picker" role="listbox" aria-label="选择实例">
                {instances.length === 0 ? (
                  <p className="muted instance-picker-empty">还没有可用的实例。</p>
                ) : (
                  instances.map((item) => (
                    <button
                      key={item.id}
                      type="button"
                      role="option"
                      aria-selected={item.id === current?.id}
                      className={`instance-picker-item ${item.id === current?.id ? "selected" : ""}`}
                      onClick={() => void chooseInstance(item.id)}
                    >
                      <span className="instance-picker-name">{item.displayName}</span>
                      <span className="muted">
                        {item.loader} · {item.minecraft}
                      </span>
                    </button>
                  ))
                )}
              </div>
            ) : null}
          </div>
        </div>
      </div>
    </div>
  );
}
