import { createContext, useCallback, useContext, useEffect, useRef, useState, type ReactNode } from "react";
import * as accountCommands from "../api/commands/accounts";
import * as communityCommands from "../api/commands/community";
import * as gameCommands from "../api/commands/games";
import * as stateCommands from "../api/commands/state";
import * as taskCommands from "../api/commands/tasks";
import { backendAvailable } from "../api/invoke";
import { useToast } from "../shell/toast";
import type {
  AccountInfo,
  AvatarInfo,
  CommunityData,
  FavoriteInfo,
  GameSessionInfo,
  InstanceCatalogInfo,
  LauncherOverview,
  ModInfo,
  ResourceKeyInfo,
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

type CoreResource = "overview" | "settings" | "accounts" | "instances" | "mods" | "servers" | "bookmarks";
type RuntimeResource = "tasks" | "games" | "community" | "avatar";
type ResourceKey = CoreResource | RuntimeResource;
type ResourceStatus = Record<ResourceKey, ResourceLoadState>;
export type ResourceVersion = { epoch: string; revision: number };

interface LauncherStore {
  overview: LauncherOverview | null;
  settings: SettingsInfo | null;
  accounts: AccountInfo[] | null;
  instances: InstanceCatalogInfo | null;
  mods: ModInfo[] | null;
  servers: SavedServerInfo[] | null;
  bookmarks: FavoriteInfo[] | null;
  stateEpoch: string;
  resourceVersions: Partial<Record<ResourceKey, ResourceVersion>>;
  tasks: TaskInfo[];
  games: GameSessionInfo[];
  community: CommunityData | null;
  avatar: AvatarInfo | null;
  ready: boolean;
  error: string;
  resourceStatus: Partial<ResourceStatus>;
  refreshState: () => Promise<void>;
  refreshSettingsResources: (changed: readonly ResourceKeyInfo[]) => Promise<void>;
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
  const [overview, setOverview] = useState<LauncherOverview | null>(null);
  const [settings, setSettings] = useState<SettingsInfo | null>(null);
  const [accounts, setAccounts] = useState<AccountInfo[] | null>(null);
  const [instances, setInstances] = useState<InstanceCatalogInfo | null>(null);
  const [mods, setMods] = useState<ModInfo[] | null>(null);
  const [servers, setServers] = useState<SavedServerInfo[] | null>(null);
  const [bookmarks, setBookmarks] = useState<FavoriteInfo[] | null>(null);
  const [stateEpoch, setStateEpoch] = useState("");
  const epochRef = useRef("");
  const resourceVersions = useRef<Partial<Record<ResourceKey, { epoch: string; revision: number }>>>({});
  const [tasks, setTasks] = useState<TaskInfo[]>([]);
  const [games, setGames] = useState<GameSessionInfo[]>([]);
  const [community, setCommunity] = useState<CommunityData | null>(null);
  const [avatar, setAvatar] = useState<AvatarInfo | null>(null);
  const mounted = useRef(true);
  const requests = useRef<Partial<Record<ResourceKey, number>>>({});
  const [ready, setReady] = useState(false);
  const [error, setError] = useState("");
  const [resourceStatus, setResourceStatus] = useState<Partial<ResourceStatus>>({});

  const resetForEpoch = useCallback((epoch: string) => {
    if (epochRef.current === epoch) return;
    epochRef.current = epoch;
    resourceVersions.current = {};
    setResourceStatus({});
    setOverview(null);
    setSettings(null);
    setAccounts(null);
    setInstances(null);
    setMods(null);
    setServers(null);
    setBookmarks(null);
    setTasks([]);
    setGames([]);
    setCommunity(null);
    setAvatar(null);
    setStateEpoch(epoch);
  }, []);

  const receiveSnapshot = useCallback(<T,>(
    key: ResourceKey,
    next: ResourceSnapshot<T>,
    apply: (value: T) => void,
  ): boolean => {
    resetForEpoch(next.epoch);
    const current = resourceVersions.current[key];
    if (current && current.epoch === next.epoch && next.revision < current.revision) return false;
    resourceVersions.current[key] = { epoch: next.epoch, revision: next.revision };
    apply(next.data);
    setResourceStatus((previous) => ({
      ...previous,
      [key]: { status: "ready", refreshing: false, error: "" },
    }));
    return true;
  }, [resetForEpoch]);

  const loadCore = useCallback(async <T,>(
    key: CoreResource,
    loader: () => Promise<ResourceSnapshot<T>>,
    apply: (value: T) => void,
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
      receiveSnapshot(key, next, apply);
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
  }, [notify, receiveSnapshot]);

  const refreshSettings = useCallback(
    () => loadCore("settings", stateCommands.getSettings, setSettings),
    [loadCore],
  );
  const refreshAccounts = useCallback(
    () => loadCore("accounts", stateCommands.listAccounts, setAccounts),
    [loadCore],
  );

  const refreshState = useCallback(async () => {
    const overview = await loadCore(
      "overview",
      stateCommands.getOverview,
      setOverview,
    );
    const instanceId = overview?.data.selectedInstanceId ?? null;
    await Promise.allSettled([
      refreshSettings(),
      refreshAccounts(),
      loadCore("instances", stateCommands.listInstances, setInstances),
      loadCore("servers", stateCommands.listServers, setServers),
      loadCore("bookmarks", stateCommands.listBookmarks, setBookmarks),
      instanceId
        ? loadCore("mods", () => stateCommands.listInstanceMods(instanceId), setMods)
        : Promise.resolve(null).then(() => setMods([])),
    ]);
  }, [loadCore, refreshSettings, refreshAccounts]);

  const refreshTasks = useCallback(async () => {
    const sequence = (requests.current.tasks ?? 0) + 1;
    requests.current.tasks = sequence;
    setResourceStatus((previous) => ({ ...previous, tasks: { status: previous.tasks?.status === "ready" ? "ready" : "loading", refreshing: true, error: "" } }));
    try {
      const next = await taskCommands.listTasks();
      if (mounted.current && requests.current.tasks === sequence) {
        receiveSnapshot("tasks", next, setTasks);
      }
    } catch (failure) {
      if (mounted.current) {
        const message = messageOf(failure);
        setResourceStatus((previous) => ({ ...previous, tasks: { status: "error", refreshing: false, error: message } }));
        notify(message);
      }
    }
  }, [notify, receiveSnapshot]);

  const refreshGames = useCallback(async () => {
    const sequence = (requests.current.games ?? 0) + 1;
    requests.current.games = sequence;
    setResourceStatus((previous) => ({ ...previous, games: { status: previous.games?.status === "ready" ? "ready" : "loading", refreshing: true, error: "" } }));
    try {
      const next = await gameCommands.listGameSessions();
      if (mounted.current && requests.current.games === sequence) {
        receiveSnapshot("games", next, setGames);
      }
    } catch (failure) {
      if (mounted.current) {
        const message = messageOf(failure);
        setResourceStatus((previous) => ({ ...previous, games: { status: "error", refreshing: false, error: message } }));
        notify(message);
      }
    }
  }, [notify, receiveSnapshot]);

  const refreshCommunity = useCallback(async () => {
    const sequence = (requests.current.community ?? 0) + 1;
    requests.current.community = sequence;
    setResourceStatus((previous) => ({ ...previous, community: { status: previous.community?.status === "ready" ? "ready" : "loading", refreshing: true, error: "" } }));
    try {
      const next = await communityCommands.refreshCommunity();
      if (mounted.current && requests.current.community === sequence) {
        receiveSnapshot("community", next, setCommunity);
      }
    } catch (failure) {
      if (mounted.current) {
        const message = messageOf(failure);
        setResourceStatus((previous) => ({ ...previous, community: { status: "error", refreshing: false, error: message } }));
        notify(message);
      }
    }
  }, [notify, receiveSnapshot]);

  const refreshAvatar = useCallback(async () => {
    const sequence = (requests.current.avatar ?? 0) + 1;
    requests.current.avatar = sequence;
    setResourceStatus((previous) => ({ ...previous, avatar: { status: previous.avatar?.status === "ready" ? "ready" : "loading", refreshing: true, error: "" } }));
    try {
      const next = await accountCommands.readAvatar();
      if (mounted.current && requests.current.avatar === sequence) {
        setAvatar(next);
        setResourceStatus((previous) => ({ ...previous, avatar: { status: "ready", refreshing: false, error: "" } }));
      }
    } catch (failure) {
      if (mounted.current) {
        const message = messageOf(failure);
        setResourceStatus((previous) => ({ ...previous, avatar: { status: "error", refreshing: false, error: message } }));
        notify(message);
      }
    }
  }, [notify]);

  const refreshSettingsResources = useCallback(async (changed: readonly ResourceKeyInfo[]) => {
    const kinds = new Set(changed.map(({ kind }) => kind));
    const mods = changed.find(({ kind }) => kind === "instanceMods");
    const instanceId = mods?.instanceId;
    if (mods && !instanceId) {
      requests.current.mods = (requests.current.mods ?? 0) + 1;
      setMods([]);
    }
    await Promise.allSettled([
      kinds.has("settings") ? refreshSettings() : undefined,
      kinds.has("accounts") ? refreshAccounts() : undefined,
      kinds.has("avatar") ? refreshAvatar() : undefined,
      kinds.has("overview") ? loadCore("overview", stateCommands.getOverview, setOverview) : undefined,
      kinds.has("instances") ? loadCore("instances", stateCommands.listInstances, setInstances) : undefined,
      instanceId
        ? loadCore("mods", () => stateCommands.listInstanceMods(instanceId), setMods)
        : undefined,
    ]);
  }, [loadCore, refreshSettings, refreshAccounts, refreshAvatar]);

  useEffect(() => {
    if (!backendAvailable()) {
      setError("后端未连接：请使用 npm run tauri dev 启动桌面应用，或用 npm run dev:demo 打开演示模式。");
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
    overview,
    settings,
    accounts,
    instances,
    mods,
    servers,
    bookmarks,
    stateEpoch,
    resourceVersions: resourceVersions.current,
    tasks,
    games,
    community,
    avatar,
    ready,
    error,
    resourceStatus,
    refreshState,
    refreshSettingsResources,
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
