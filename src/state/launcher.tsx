import { createContext, useCallback, useContext, useEffect, useRef, useState, type ReactNode } from "react";
import * as accountCommands from "../api/commands/accounts";
import * as communityCommands from "../api/commands/community";
import * as gameCommands from "../api/commands/games";
import * as stateCommands from "../api/commands/state";
import * as taskCommands from "../api/commands/tasks";
import { isTauri } from "../api/invoke";
import { useToast } from "../shell/toast";
import type {
  AccountInfo,
  AvatarInfo,
  CommunityData,
  FavoriteInfo,
  GameSessionInfo,
  InstanceCatalogInfo,
  LauncherOverview,
  LauncherState,
  ModInfo,
  ResourceSnapshot,
  SavedServerInfo,
  SettingsInfo,
  TaskInfo,
} from "../types/launcher";

type LoadStatus = "idle" | "loading" | "ready" | "error";

interface ResourceLoadState {
  status: LoadStatus;
  refreshing: boolean;
  error: string;
}

interface LauncherParts {
  overview: LauncherOverview;
  settings: SettingsInfo;
  accounts: AccountInfo[];
  instances: InstanceCatalogInfo;
  mods: ModInfo[];
  servers: SavedServerInfo[];
  bookmarks: FavoriteInfo[];
}

function assembleState(parts: Partial<LauncherParts>): LauncherState | null {
  const { overview, settings, accounts, instances, mods, servers, bookmarks } = parts;
  if (!overview || !settings || !accounts || !instances || !mods || !servers || !bookmarks) return null;
  return {
    ...overview,
    ...settings,
    accounts,
    instances: instances.items,
    versionGroups: instances.versionGroups,
    mods,
    servers,
    favorites: bookmarks,
  };
}

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
  resourceStatus: Record<string, ResourceLoadState>;
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
  const epochRef = useRef("");
  const resourceVersions = useRef<Record<string, { epoch: string; revision: number }>>({});
  const [tasks, setTasks] = useState<TaskInfo[]>([]);
  const [games, setGames] = useState<GameSessionInfo[]>([]);
  const [community, setCommunity] = useState<CommunityData | null>(null);
  const [avatar, setAvatar] = useState<AvatarInfo | null>(null);
  const mounted = useRef(true);
  const requests = useRef<Record<string, number>>({});
  const [ready, setReady] = useState(false);
  const [error, setError] = useState("");
  const [resourceStatus, setResourceStatus] = useState<Record<string, ResourceLoadState>>({});
  const parts = useRef<Partial<LauncherParts>>({});

  const loadCore = useCallback(async <T,>(
    key: keyof LauncherParts,
    loader: () => Promise<ResourceSnapshot<T>>,
    apply: (value: T) => void,
    epochAuthority = false,
  ): Promise<ResourceSnapshot<T> | null> => {
    const sequence = (requests.current[key] ?? 0) + 1;
    requests.current[key] = sequence;
    setResourceStatus((previous) => ({
      ...previous,
      [key]: { status: previous[key]?.status === "ready" ? "ready" : "loading", refreshing: true, error: "" },
    }));
    try {
      const next = await loader();
      if (!mounted.current || requests.current[key] !== sequence) return null;
      if (epochAuthority && epochRef.current !== next.epoch) {
        epochRef.current = next.epoch;
        parts.current = {};
        resourceVersions.current = {};
        setState(null);
        setTasks([]);
        setGames([]);
        setCommunity(null);
        setAvatar(null);
        setStateEpoch(next.epoch);
      } else if (epochRef.current.length > 0 && epochRef.current !== next.epoch) {
        throw new Error("后端运行代次已变化，请重新读取状态。");
      }
      const current = resourceVersions.current[key];
      if (current && current.epoch === next.epoch && next.revision < current.revision) return null;
      resourceVersions.current[key] = { epoch: next.epoch, revision: next.revision };
      apply(next.data);
      setState(assembleState(parts.current));
      setResourceStatus((previous) => ({
        ...previous,
        [key]: { status: "ready", refreshing: false, error: "" },
      }));
      return next;
    } catch (failure) {
      const message = messageOf(failure);
      if (mounted.current) {
        setResourceStatus((previous) => ({
          ...previous,
          [key]: { status: "error", refreshing: false, error: message },
        }));
        notify(message);
      }
      return null;
    }
  }, [notify]);

  const refreshState = useCallback(async () => {
    const overview = await loadCore(
      "overview",
      stateCommands.getOverview,
      (value) => { parts.current.overview = value; },
      true,
    );
    const instanceId = overview?.data.selectedInstanceId ?? parts.current.overview?.selectedInstanceId ?? null;
    await Promise.allSettled([
      loadCore("settings", stateCommands.getSettings, (value) => {
        parts.current.settings = value;
        const version = resourceVersions.current.settings;
        if (version) setStateRevision(version.revision);
      }),
      loadCore("accounts", stateCommands.listAccounts, (value) => { parts.current.accounts = value; }),
      loadCore("instances", stateCommands.listInstances, (value) => { parts.current.instances = value; }),
      loadCore("servers", stateCommands.listServers, (value) => { parts.current.servers = value; }),
      loadCore("bookmarks", stateCommands.listBookmarks, (value) => { parts.current.bookmarks = value; }),
      instanceId
        ? loadCore("mods", () => stateCommands.listInstanceMods(instanceId), (value) => { parts.current.mods = value; })
        : Promise.resolve(null).then(() => { parts.current.mods = []; }),
    ]);
    setState(assembleState(parts.current));
  }, [loadCore]);

  const refreshTasks = useCallback(async () => {
    const sequence = (requests.current.Tasks ?? 0) + 1;
    requests.current.Tasks = sequence;
    setResourceStatus((previous) => ({ ...previous, Tasks: { status: previous.Tasks?.status === "ready" ? "ready" : "loading", refreshing: true, error: "" } }));
    try {
      const next = await taskCommands.listTasks();
      const current = resourceVersions.current.Tasks;
      if (mounted.current && requests.current.Tasks === sequence &&
          (!current || current.epoch !== next.epoch || next.revision >= current.revision)) {
        resourceVersions.current.Tasks = { epoch: next.epoch, revision: next.revision };
        setTasks(next.data);
        setResourceStatus((previous) => ({ ...previous, Tasks: { status: "ready", refreshing: false, error: "" } }));
      }
    } catch (failure) {
      if (mounted.current) {
        const message = messageOf(failure);
        setResourceStatus((previous) => ({ ...previous, Tasks: { status: "error", refreshing: false, error: message } }));
        notify(message);
      }
    }
  }, [notify]);

  const refreshGames = useCallback(async () => {
    const sequence = (requests.current.Games ?? 0) + 1;
    requests.current.Games = sequence;
    setResourceStatus((previous) => ({ ...previous, Games: { status: previous.Games?.status === "ready" ? "ready" : "loading", refreshing: true, error: "" } }));
    try {
      const next = await gameCommands.listGameSessions();
      const current = resourceVersions.current.Games;
      if (mounted.current && requests.current.Games === sequence &&
          (!current || current.epoch !== next.epoch || next.revision >= current.revision)) {
        resourceVersions.current.Games = { epoch: next.epoch, revision: next.revision };
        setGames(next.data);
        setResourceStatus((previous) => ({ ...previous, Games: { status: "ready", refreshing: false, error: "" } }));
      }
    } catch (failure) {
      if (mounted.current) {
        const message = messageOf(failure);
        setResourceStatus((previous) => ({ ...previous, Games: { status: "error", refreshing: false, error: message } }));
        notify(message);
      }
    }
  }, [notify]);

  const refreshCommunity = useCallback(async () => {
    const sequence = (requests.current.Community ?? 0) + 1;
    requests.current.Community = sequence;
    setResourceStatus((previous) => ({ ...previous, Community: { status: previous.Community?.status === "ready" ? "ready" : "loading", refreshing: true, error: "" } }));
    try {
      const next = await communityCommands.refreshCommunity();
      const current = resourceVersions.current.Community;
      if (mounted.current && requests.current.Community === sequence &&
          (!current || current.epoch !== next.epoch || next.revision >= current.revision)) {
        resourceVersions.current.Community = { epoch: next.epoch, revision: next.revision };
        setCommunity(next.data);
        setResourceStatus((previous) => ({ ...previous, Community: { status: "ready", refreshing: false, error: "" } }));
      }
    } catch (failure) {
      if (mounted.current) {
        const message = messageOf(failure);
        setResourceStatus((previous) => ({ ...previous, Community: { status: "error", refreshing: false, error: message } }));
        notify(message);
      }
    }
  }, [notify]);

  const refreshAvatar = useCallback(async () => {
    const sequence = (requests.current.Avatar ?? 0) + 1;
    requests.current.Avatar = sequence;
    setResourceStatus((previous) => ({ ...previous, Avatar: { status: previous.Avatar?.status === "ready" ? "ready" : "loading", refreshing: true, error: "" } }));
    try {
      const next = await accountCommands.readAvatar();
      if (mounted.current && requests.current.Avatar === sequence) {
        setAvatar(next);
        setResourceStatus((previous) => ({ ...previous, Avatar: { status: "ready", refreshing: false, error: "" } }));
      }
    } catch (failure) {
      if (mounted.current) {
        const message = messageOf(failure);
        setResourceStatus((previous) => ({ ...previous, Avatar: { status: "error", refreshing: false, error: message } }));
        notify(message);
      }
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
    resourceStatus,
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
