import { createContext, useCallback, useContext, useEffect, useState, type ReactNode } from "react";
import * as accountCommands from "../api/commands/accounts";
import * as communityCommands from "../api/commands/community";
import * as gameCommands from "../api/commands/games";
import * as stateCommands from "../api/commands/state";
import * as taskCommands from "../api/commands/tasks";
import { isTauri } from "../api/invoke";
import { useToast } from "../shell/toast";
import type { AvatarInfo, CommunityData, GameInfo, LauncherState, TaskInfo } from "../types/launcher";

interface LauncherStore {
  state: LauncherState | null;
  tasks: TaskInfo[];
  games: GameInfo[];
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
  const [tasks, setTasks] = useState<TaskInfo[]>([]);
  const [games, setGames] = useState<GameInfo[]>([]);
  const [community, setCommunity] = useState<CommunityData | null>(null);
  const [avatar, setAvatar] = useState<AvatarInfo | null>(null);
  const [ready, setReady] = useState(false);
  const [error, setError] = useState("");

  const refreshState = useCallback(async () => {
    try {
      setState(await stateCommands.getState());
    } catch (failure) {
      notify(messageOf(failure));
    }
  }, [notify]);

  const refreshTasks = useCallback(async () => {
    try {
      setTasks(await taskCommands.listTasks());
    } catch (failure) {
      notify(messageOf(failure));
    }
  }, [notify]);

  const refreshGames = useCallback(async () => {
    try {
      setGames(await gameCommands.listGames());
    } catch (failure) {
      notify(messageOf(failure));
    }
  }, [notify]);

  const refreshCommunity = useCallback(async () => {
    try {
      setCommunity(await communityCommands.refreshCommunity());
    } catch (failure) {
      notify(messageOf(failure));
    }
  }, [notify]);

  const refreshAvatar = useCallback(async () => {
    try {
      setAvatar(await accountCommands.readAvatar());
    } catch (failure) {
      notify(messageOf(failure));
    }
  }, [notify]);

  useEffect(() => {
    if (!isTauri()) {
      setError("后端未连接：请使用 npm run tauri dev 启动桌面应用。");
      setReady(true);
      return;
    }

    let disposed = false;
    void (async () => {
      try {
        const [nextState, nextTasks, nextGames, nextCommunity, nextAvatar] = await Promise.all([
          stateCommands.getState(),
          taskCommands.listTasks(),
          gameCommands.listGames(),
          communityCommands.getCommunity(),
          accountCommands.readAvatar(),
        ]);
        if (disposed) return;
        setState(nextState);
        setTasks(nextTasks);
        setGames(nextGames);
        setCommunity(nextCommunity);
        setAvatar(nextAvatar);
        setError("");
      } catch (failure) {
        if (disposed) return;
        const message = messageOf(failure);
        setError(message);
        notify(message);
      } finally {
        if (!disposed) setReady(true);
      }
    })();

    return () => {
      disposed = true;
    };
  }, [notify]);

  const value: LauncherStore = {
    state,
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
