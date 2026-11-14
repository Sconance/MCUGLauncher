import {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useState,
  type ReactNode,
} from "react";
import type { UnlistenFn } from "@tauri-apps/api/event";
import { EVENTS, on, type EventName } from "../api/events";
import * as accountCommands from "../api/commands/accounts";
import * as communityCommands from "../api/commands/community";
import * as gameCommands from "../api/commands/games";
import * as stateCommands from "../api/commands/state";
import * as taskCommands from "../api/commands/tasks";
import { isTauri } from "../api/invoke";
import { useToast } from "../shell/toast";
import type {
  AvatarInfo,
  CommunityData,
  GameInfo,
  LauncherState,
  TaskInfo,
  ToastInfo,
} from "../types/launcher";

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
}

const LauncherContext = createContext<LauncherStore | null>(null);

function messageOf(error: unknown): string {
  return error instanceof Error ? error.message : String(error);
}

/**
 * 后端状态容器：订阅 Tauri 事件，持有 state/tasks/games/community/avatar。
 * 页面只读这里的数据，不再各自请求与维护副本。
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

  useEffect(() => {
    if (!isTauri()) {
      setError("后端未连接：请使用 npm run tauri dev 启动桌面应用。");
      setReady(true);
      return;
    }

    let disposed = false;
    const unlisteners: UnlistenFn[] = [];

    const registrations: [EventName, (payload: never) => void][] = [
      [EVENTS.state, (payload) => setState(payload)],
      [EVENTS.tasks, (payload) => setTasks(payload)],
      [EVENTS.games, (payload) => setGames(payload)],
      [EVENTS.community, (payload) => setCommunity(payload)],
      [EVENTS.avatar, (payload) => setAvatar(payload)],
      [EVENTS.toast, (payload) => notify((payload as ToastInfo).message)],
      [EVENTS.error, (payload) => notify((payload as ToastInfo).message)],
    ];

    void (async () => {
      for (const [event, handler] of registrations) {
        const unlisten = await on(event, handler as (payload: never) => void);
        if (disposed) unlisten();
        else unlisteners.push(unlisten);
      }
    })();

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
      unlisteners.forEach((unlisten) => unlisten());
    };
  }, [notify]);

  const refreshState = useCallback(async () => setState(await stateCommands.getState()), []);
  const refreshTasks = useCallback(async () => setTasks(await taskCommands.listTasks()), []);
  const refreshGames = useCallback(async () => setGames(await gameCommands.listGames()), []);
  const refreshCommunity = useCallback(
    async () => setCommunity(await communityCommands.refreshCommunity()),
    [],
  );

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
  };

  return <LauncherContext.Provider value={value}>{children}</LauncherContext.Provider>;
}

export function useLauncher(): LauncherStore {
  const store = useContext(LauncherContext);
  if (!store) throw new Error("useLauncher 必须在 LauncherProvider 内使用");
  return store;
}
