import { useCallback, useEffect, useRef, useState } from "react";
import navGames from "../assets/icons/nav/nav-games.svg";
import navTasks from "../assets/icons/nav/nav-tasks.svg";
import { stopGame } from "../api/commands/games";
import { taskAction } from "../api/commands/tasks";
import { useLauncher } from "../state/launcher";
import type { GameSessionInfo, TaskInfo } from "../types/launcher";
import { useTopStatusForced } from "./topStatusPreference";
import "./TopStatusButtons.css";

type StatusKey = "games" | "tasks";

/** 与 TopStatusButtons.css 的淡出时长保持一致。 */
const EXIT_MS = 140;

/** 这些状态的任务已经结束，不再占用右上角的下载入口。 */
const TERMINAL_TASKS = new Set(["succeeded", "failed", "cancelled"]);

const TASK_ACTIONS: { key: string; label: string }[] = [
  { key: "pause", label: "暂停" },
  { key: "resume", label: "继续" },
  { key: "cancel", label: "取消" },
  { key: "delete", label: "删除" },
];

const TASK_STATUS: Record<string, string> = {
  queued: "排队中",
  running: "进行中",
  paused: "已暂停",
  succeeded: "已完成",
  failed: "失败",
  cancelled: "已取消",
};

function statusText(value: string): string {
  return TASK_STATUS[value] ?? value;
}

function taskPercent(task: TaskInfo): number | null {
  const progress = task.progress;
  const total = progress?.total;
  if (!progress || total == null || total <= 0) return null;
  return Math.min(100, Math.max(0, Math.round((progress.completed / total) * 100)));
}

/**
 * 右上角的状态入口：有游戏在跑、或有未结束的下载任务时才出现。
 * 单按钮是圆形，两个按钮合成一枚胶囊；按钮的出现顺序决定左右顺序（先出现的更靠右）。
 */
export default function TopStatus() {
  const { games, tasks, instances, refreshGames, refreshTasks } = useLauncher();
  const forceGames = useTopStatusForced("games");
  const forceTasks = useTopStatusForced("tasks");
  const rootRef = useRef<HTMLDivElement>(null);

  const runningGames = games.filter((game) => game.status === "running");
  const activeTasks = tasks.filter((task) => !TERMINAL_TASKS.has(task.status));
  const showGames = forceGames || runningGames.length > 0;
  const showTasks = forceTasks || activeTasks.length > 0;

  /** 按「先出现」记录顺序；row-reverse 渲染时数组开头的一枚落在最右边。 */
  const [order, setOrder] = useState<StatusKey[]>([]);
  const [open, setOpen] = useState<StatusKey | null>(null);
  const [closing, setClosing] = useState(false);
  const [busy, setBusy] = useState("");
  const [status, setStatus] = useState("");
  const closeTimer = useRef<number | null>(null);
  const pendingOpen = useRef<StatusKey | null | undefined>(undefined);

  const clearCloseTimer = useCallback(() => {
    if (closeTimer.current === null) return;
    window.clearTimeout(closeTimer.current);
    closeTimer.current = null;
  }, []);

  /**
   * 先让当前浮窗淡出，再决定是打开另一个浮窗还是收起。
   * 切换入口时旧内容会保留到淡出结束，避免内容瞬间替换造成的闪跳。
   */
  const closePanel = useCallback(
    (next: StatusKey | null) => {
      if (open === null) {
        if (next !== null) setOpen(next);
        return;
      }

      pendingOpen.current = next;
      if (closing) return;

      setClosing(true);
      clearCloseTimer();
      closeTimer.current = window.setTimeout(() => {
        closeTimer.current = null;
        const value = pendingOpen.current;
        pendingOpen.current = undefined;
        setClosing(false);
        setOpen(value === undefined ? null : value);
      }, EXIT_MS);
    },
    [open, closing, clearCloseTimer]
  );

  useEffect(() => clearCloseTimer, [clearCloseTimer]);

  useEffect(() => {
    setOrder((previous) => {
      const next = previous.filter((key) => (key === "games" ? showGames : showTasks));
      if (showGames && !next.includes("games")) next.push("games");
      if (showTasks && !next.includes("tasks")) next.push("tasks");
      if (next.length === previous.length && next.every((key, index) => key === previous[index])) {
        return previous;
      }
      return next;
    });
  }, [showGames, showTasks]);

  // 入口消失时，连带收掉它下面的浮窗。
  useEffect(() => {
    if (open === "games" && !showGames) closePanel(null);
    else if (open === "tasks" && !showTasks) closePanel(null);
  }, [open, showGames, showTasks, closePanel]);

  // 点浮窗外面或按 Esc 关闭。
  useEffect(() => {
    if (!open) return;
    const onPointerDown = (event: PointerEvent) => {
      if (!rootRef.current?.contains(event.target as Node)) closePanel(null);
    };
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape") closePanel(null);
    };
    document.addEventListener("pointerdown", onPointerDown);
    document.addEventListener("keydown", onKeyDown);
    return () => {
      document.removeEventListener("pointerdown", onPointerDown);
      document.removeEventListener("keydown", onKeyDown);
    };
  }, [open, closePanel]);

  if (!showGames && !showTasks && open === null) return null;

  let shownOrder = order.filter((key) => (key === "games" ? showGames : showTasks));
  if (shownOrder.length === 0) {
    shownOrder = [];
    if (showGames) shownOrder.push("games");
    if (showTasks) shownOrder.push("tasks");
  }

  const title = open === "games" ? "正在运行的实例" : "下载任务";

  const toggle = (key: StatusKey) => {
    setStatus("");
    if (key === "games") void refreshGames();
    else void refreshTasks();
    closePanel(open === key ? null : key);
  };

  const instanceName = (id: string) =>
    instances?.items.find((item) => item.id === id)?.displayName ?? id;

  const closeGame = async (game: GameSessionInfo) => {
    setBusy(`stop:${game.sessionId}`);
    setStatus("");
    try {
      const result = await stopGame(game.sessionId);
      await refreshGames();
      setStatus(result.message);
    } catch (error) {
      setStatus(error instanceof Error ? error.message : String(error));
    } finally {
      setBusy("");
    }
  };

  const actTask = async (task: TaskInfo, action: string) => {
    setBusy(`${task.id}:${action}`);
    setStatus("");
    try {
      const result = await taskAction(task.id, action);
      await refreshTasks();
      setStatus(result.message);
    } catch (error) {
      setStatus(error instanceof Error ? error.message : String(error));
    } finally {
      setBusy("");
    }
  };

  return (
    <div
      ref={rootRef}
      className={`top-status${shownOrder.length > 1 ? " is-paired" : " is-single"}`}
    >
      <div className="top-status-buttons glass-surface glass-pill">
        {shownOrder.map((key) => (
          <button
            key={key}
            type="button"
            className={`top-status-button${open === key ? " is-open" : ""}`}
            data-tip={key === "games" ? "正在运行的实例" : "下载任务"}
            data-tip-side="bottom"
            aria-label={key === "games" ? "正在运行的实例" : "下载任务"}
            aria-haspopup="dialog"
            aria-expanded={open === key}
            onClick={() => toggle(key)}
          >
            <img src={key === "games" ? navGames : navTasks} alt="" width={16} height={16} />
          </button>
        ))}
      </div>

      {open ? (
        <div
          key={open}
          className={`top-status-popover glass-surface glass-panel${closing ? " is-closing" : ""}`}
          role="dialog"
          aria-label={title}
        >
          {open === "games" ? (
            runningGames.length === 0 ? (
              <p className="top-status-empty">当前没有正在运行的实例。</p>
            ) : (
              <div className="top-status-list">
                {runningGames.map((game) => (
                  <article className="top-status-item" key={game.sessionId}>
                    <div className="top-status-item-head">
                      <span className="top-status-item-name">{instanceName(game.instanceId)}</span>
                      <span className="top-status-pill">运行中</span>
                    </div>
                    <span className="top-status-meta">
                      {game.pid === null ? "PID 未知" : `PID ${game.pid}`}
                    </span>
                    <div className="top-status-actions">
                      <button
                        type="button"
                        className="top-status-action"
                        disabled={busy !== ""}
                        onClick={() => void closeGame(game)}
                      >
                        关闭游戏
                      </button>
                    </div>
                  </article>
                ))}
              </div>
            )
          ) : activeTasks.length === 0 ? (
            <p className="top-status-empty">当前没有排队或运行中的任务。</p>
          ) : (
            <div className="top-status-list">
              {activeTasks.map((task) => {
                const percent = taskPercent(task);
                const actions = TASK_ACTIONS.filter((action) =>
                  task.allowedActions.includes(action.key)
                );
                return (
                  <article className="top-status-item" key={task.id}>
                    <div className="top-status-item-head">
                      <span className="top-status-item-name">{task.name}</span>
                      <span className="top-status-pill">{statusText(task.status)}</span>
                    </div>
                    <span className="top-status-meta">
                      {task.phase}
                      {percent === null ? "" : ` · ${percent}%`}
                    </span>
                    {percent === null ? null : (
                      <div className="top-status-track" aria-hidden="true">
                        <span className="top-status-fill" style={{ width: `${percent}%` }} />
                      </div>
                    )}
                    {task.error ? <p className="top-status-error">{task.error.message}</p> : null}
                    {actions.length > 0 ? (
                      <div className="top-status-actions">
                        {actions.map((action) => (
                          <button
                            key={action.key}
                            type="button"
                            className="top-status-action"
                            disabled={busy !== ""}
                            onClick={() => void actTask(task, action.key)}
                          >
                            {action.label}
                          </button>
                        ))}
                      </div>
                    ) : null}
                  </article>
                );
              })}
            </div>
          )}

          {status ? (
            <p className="top-status-result" role="status">
              {status}
            </p>
          ) : null}
        </div>
      ) : null}
    </div>
  );
}
