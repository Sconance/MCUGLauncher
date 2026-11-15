import { createContext, useCallback, useContext, useEffect, useRef, useState, type ReactNode } from "react";
import * as accountCommands from "../api/commands/accounts";
import * as communityCommands from "../api/commands/community";
import * as gameCommands from "../api/commands/games";
import * as stateCommands from "../api/commands/state";
import * as taskCommands from "../api/commands/tasks";
import { isTauri } from "../api/invoke";
import { useToast } from "../shell/toast";
import type { AvatarInfo, CommunityData, GameSessionInfo, LauncherState, TaskInfo } from "../types/launcher";

interface LauncherStore {
  state: LauncherState | null;
  stateEpoch: string;
  stateRevision: number;
  tasks: TaskInfo[];
  games: GameSessionInfo[];
  community: CommunityData | null;
  avatar: AvatarInfo | null;
  ready: boolean;
  error: string;
  refreshState: () => Promise<void>;
  refreshTasks: () => Promise<void>;
  refreshGames: () => Promise<void>;
  refreshCommunity: () => Promise<void>;
  refreshAvatar: () => Promise<void>;
}

const LauncherContext = createContext<LauncherStore | null>(null);

function messageOf(error: unknown): string {
  return error instanceof Error ? error.message : String(error);
}

/**
 * 命令驱动的状态容器：所有数据来自 Tauri 命令。
 * 内核只需实现命令，不需要推送事件；页面在动作完成后调用 refresh* 重新取数。
 */
export function LauncherProvider({ children }: { children: ReactNode }) {
  const { notify } = useToast();
  const [state, setState] = useState<LauncherState | null>(null);
  const [stateEpoch, setStateEpoch] = useState("");
  const [stateRevision, setStateRevision] = useState(0);
  const stateVersion = useRef({ epoch: "", revision: 0 });
  const resourceVersions = useRef<Record<string, { epoch: string; revision: number }>>({});
  const [tasks, setTasks] = useState<TaskInfo[]>([]);
  const [games, setGames] = useState<GameSessionInfo[]>([]);
  const [community, setCommunity] = useState<CommunityData | null>(null);
  const [avatar, setAvatar] = useState<AvatarInfo | null>(null);
  const mounted = useRef(true);
  const requests = useRef<Record<string, number>>({});
  const [ready, setReady] = useState(false);
  const [error, setError] = useState("");

  const refreshState = useCallback(async () => {
    const sequence = (requests.current.State ?? 0) + 1;
    requests.current.State = sequence;
    try {
      const next = await stateCommands.getState();
      const current = stateVersion.current;
      const isCurrent = next.epoch === current.epoch;
      if (mounted.current && requests.current.State === sequence && (!isCurrent || next.settingsRevision >= current.revision)) {
        stateVersion.current = { epoch: next.epoch, revision: next.settingsRevision };
        setStateEpoch(next.epoch);
        setStateRevision(next.settingsRevision);
        setState(next.data);
      }
    } catch (failure) {
      if (mounted.current) notify(messageOf(failure));
    }
  }, [notify]);

  const refreshTasks = useCallback(async () => {
    const sequence = (requests.current.Tasks ?? 0) + 1;
    requests.current.Tasks = sequence;
    try {
      const next = await taskCommands.listTasks();
      const current = resourceVersions.current.Tasks;
      if (mounted.current && requests.current.Tasks === sequence &&
          (!current || current.epoch !== next.epoch || next.revision >= current.revision)) {
        resourceVersions.current.Tasks = { epoch: next.epoch, revision: next.revision };
        setTasks(next.data);
      }
    } catch (failure) {
      if (mounted.current) notify(messageOf(failure));
    }
  }, [notify]);

  const refreshGames = useCallback(async () => {
    const sequence = (requests.current.Games ?? 0) + 1;
    requests.current.Games = sequence;
    try {
      const next = await gameCommands.listGameSessions();
      const current = resourceVersions.current.Games;
      if (mounted.current && requests.current.Games === sequence &&
          (!current || current.epoch !== next.epoch || next.revision >= current.revision)) {
        resourceVersions.current.Games = { epoch: next.epoch, revision: next.revision };
        setGames(next.data);
      }
    } catch (failure) {
      if (mounted.current) notify(messageOf(failure));
    }
  }, [notify]);

  const refreshCommunity = useCallback(async () => {
    const sequence = (requests.current.Community ?? 0) + 1;
    requests.current.Community = sequence;
    try {
      const next = await communityCommands.refreshCommunity();
      const current = resourceVersions.current.Community;
      if (mounted.current && requests.current.Community === sequence &&
          (!current || current.epoch !== next.epoch || next.revision >= current.revision)) {
        resourceVersions.current.Community = { epoch: next.epoch, revision: next.revision };
        setCommunity(next.data);
      }
    } catch (failure) {
      if (mounted.current) notify(messageOf(failure));
    }
  }, [notify]);

  const refreshAvatar = useCallback(async () => {
    const sequence = (requests.current.Avatar ?? 0) + 1;
    requests.current.Avatar = sequence;
    try {
      const next = await accountCommands.readAvatar();
      if (mounted.current && requests.current.Avatar === sequence) setAvatar(next);
    } catch (failure) {
      if (mounted.current) notify(messageOf(failure));
    }
  }, [notify]);

  useEffect(() => {
    if (!isTauri()) {
      setError("后端未连接：请使用 npm run tauri dev 启动桌面应用。");
      setReady(true);
      return;
    }

    mounted.current = true;
    void Promise.allSettled([
      refreshState(), refreshTasks(), refreshGames(), refreshCommunity(), refreshAvatar(),
    ]).then(() => {
      if (mounted.current) setReady(true);
    });

    // 全局同步不依赖页面挂载；同时刷新任务产生的实例变化。
    let busy = false;
    const synchronize = async () => {
      if (busy) return;
      busy = true;
      try {
        await Promise.allSettled([refreshTasks(), refreshGames(), refreshState()]);
      } finally {
        busy = false;
      }
    };
    const timer = window.setInterval(() => void synchronize(), 3000);
    window.addEventListener("focus", synchronize);
    return () => {
      mounted.current = false;
      window.clearInterval(timer);
      window.removeEventListener("focus", synchronize);
    };
  }, [refreshState, refreshTasks, refreshGames, refreshCommunity, refreshAvatar]);

  const value: LauncherStore = {
    state,
    stateEpoch,
    stateRevision,
    tasks,
    games,
    community,
    avatar,
    ready,
    error,
    refreshState,
    refreshTasks,
    refreshGames,
    refreshCommunity,
    refreshAvatar,
  };

  return <LauncherContext.Provider value={value}>{children}</LauncherContext.Provider>;
}

export function useLauncher(): LauncherStore {
  const store = useContext(LauncherContext);
  if (!store) throw new Error("useLauncher 必须在 LauncherProvider 内使用");
  return store;
}
