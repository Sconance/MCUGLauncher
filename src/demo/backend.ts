import type {
  ContentSearchResult,
  JavaInfo,
  LoaderListInfo,
  LoginSessionInfo,
  ResourceSnapshot,
  ResourceVersionsInfo,
  SavedInfo,
  ServerPingInfo,
  SystemInfo,
  TaskInfo,
  VanillaVersionInfo,
} from "../types/launcher";
import { createDemoState, EPOCH, type DemoState } from "./fixtures";

/**
 * 演示后端：在浏览器里模拟 `mcugl` 内核的命令。
 *
 * 只有 `npm run dev:demo` 会走到这里；`npm run tauri dev` 永远走真实 IPC。
 * 命令名与参数保持和 `docs/BACKEND_CONTRACT.md` 一致，
 * 所以页面代码不需要知道自己在演示模式里。
 */

export class DemoApiError extends Error {
  readonly code: string;
  readonly retryable: boolean;

  constructor(message: string, code = "UNSUPPORTED", retryable = false) {
    super(message);
    this.name = "DemoApiError";
    this.code = code;
    this.retryable = retryable;
  }
}

const state: DemoState = createDemoState();

const latency = (ms = 120) => new Promise((resolve) => setTimeout(resolve, ms));

function snapshot<T>(data: T): ResourceSnapshot<T> {
  return { epoch: EPOCH, revision: 1, data };
}

function saved(message: string, changed: SavedInfo["changed"] = [], entityIds: string[] = []): SavedInfo {
  return { message, changed, entityIds };
}

function text(args: Record<string, unknown> | undefined, key: string): string {
  const value = args?.[key];
  return typeof value === "string" ? value : "";
}

function boolean(args: Record<string, unknown> | undefined, key: string): boolean {
  return args?.[key] === true;
}

function nextId(prefix: string): string {
  state.sequence += 1;
  return `${prefix}-${state.sequence}`;
}

function now(): string {
  return new Date().toISOString();
}

function startTask(kind: string, name: string, instanceId: string | null): TaskInfo {
  const task: TaskInfo = {
    id: nextId("task"),
    name,
    kind,
    instanceId,
    status: "running",
    phase: "准备中",
    progress: { completed: 0, total: 100, unit: "%" },
    allowedActions: ["cancel"],
    error: null,
    result: null,
    createdAt: now(),
    updatedAt: now(),
  };
  state.tasks = [task, ...state.tasks];
  return task;
}

/** 每次查询时推进一点进度，让列表看起来是活的。 */
function tickTasks(): void {
  for (const task of state.tasks) {
    if (task.status !== "running") continue;
    const completed = Math.min(100, (task.progress?.completed ?? 0) + 25);
    task.progress = { completed, total: 100, unit: "%" };
    task.phase = completed >= 100 ? "已完成" : completed >= 50 ? "安装中" : "准备中";
    task.updatedAt = now();
    if (completed >= 100) {
      task.status = "succeeded";
      task.phase = "已完成";
      task.allowedActions = [];
      task.result = { changed: [], entityIds: task.instanceId ? [task.instanceId] : [] };
    }
  }
}

const unsupported = (command: string) =>
  new DemoApiError(`演示后端没有实现 ${command}：请在 tauri dev 里验证这条命令。`, "UNSUPPORTED", false);

const DEMO_METHODS = [
  "get_overview", "get_settings", "list_accounts", "list_instances", "list_all_instances",
  "list_instance_mods", "list_servers", "list_bookmarks", "list_tasks", "list_game_sessions",
  "get_community", "refresh_community", "read_avatar", "get_system_info", "kernel_manifest",
  "select_instance", "select_account", "forget_account", "save_settings", "toggle_bookmark",
  "toggle_mod", "delete_mod", "launch_game", "launch_to_server", "stop_game", "task_action",
  "save_server", "delete_server", "ping_server", "start_login", "get_login_session",
  "select_login_profile", "cancel_login", "scan_java", "verify_key", "test_data_source",
  "list_vanilla_versions", "list_loaders", "search_content", "list_content_versions",
  "check_updates", "open_folder", "read_log", "pick_file", "pick_folder", "reset_window",
  "backup_saves", "list_save_backups", "restore_save", "queue_content_version",
  "save_recommendations", "save_custom_sources", "use_data_sources", "remove_game_root",
  "set_offline_skin",
];

export async function demoInvoke<T>(command: string, args?: Record<string, unknown>): Promise<T> {
  await latency(60 + Math.random() * 140);

  switch (command) {
    case "get_overview":
      return snapshot(state.overview) as T;
    case "get_settings":
      return snapshot(state.settings) as T;
    case "list_accounts":
      return snapshot(state.accounts) as T;
    case "list_instances":
    case "list_all_instances":
      return snapshot(state.catalog) as T;
    case "list_instance_mods": {
      const instanceId = text(args, "instanceId");
      return snapshot(state.mods[instanceId] ?? []) as T;
    }
    case "list_servers":
      return snapshot(state.servers) as T;
    case "list_bookmarks":
      return snapshot(state.bookmarks) as T;
    case "list_tasks": {
      tickTasks();
      return snapshot(state.tasks) as T;
    }
    case "list_game_sessions":
      return snapshot(state.sessions) as T;
    case "get_community":
    case "refresh_community":
      return snapshot(state.community) as T;
    case "read_avatar":
      return { ...state.avatar } as T;
    case "get_system_info": {
      const info: SystemInfo = {
        app: "0.22.3 (demo)",
        dotnet: "—",
        webview: "浏览器（演示模式）",
        os: "Windows · 演示数据",
        root: state.settings.root,
        data: "未连接内核",
      };
      return info as T;
    }
    case "kernel_manifest":
      return {
        protocol: "mcugl.kernel/v1",
        apiVersion: "1.0.0",
        appVersion: "0.22.3 (demo)",
        backend: "demo",
        transports: ["tauri-ipc"],
        methods: DEMO_METHODS,
        events: [],
        capabilities: Object.fromEntries(DEMO_METHODS.map((method) => [method, true])),
        limits: { planTtlSeconds: 600 },
        frontend: null,
      } as T;

    case "select_instance": {
      const instanceId = text(args, "instanceId");
      state.overview = { ...state.overview, selectedInstanceId: instanceId };
      return saved(`已选择实例 ${instanceId}`, [
        { kind: "overview", instanceId: null },
        { kind: "instances", instanceId: null },
        { kind: "instanceMods", instanceId },
      ]) as T;
    }
    case "select_account": {
      const accountId = text(args, "accountId");
      state.overview = { ...state.overview, activeAccountId: accountId };
      const account = state.accounts.find((item) => item.id === accountId);
      state.avatar = { ...state.avatar, account: accountId, player: account?.player ?? state.avatar.player, label: account?.player ?? state.avatar.label };
      return saved("已切换账号", [
        { kind: "overview", instanceId: null },
        { kind: "accounts", instanceId: null },
        { kind: "avatar", instanceId: null },
      ]) as T;
    }
    case "forget_account": {
      const accountId = text(args, "accountId");
      state.accounts = state.accounts.filter((item) => item.id !== accountId);
      return saved("已移除账号", [{ kind: "accounts", instanceId: null }]) as T;
    }
    case "save_settings": {
      const patch = (args?.patch ?? {}) as Record<string, unknown>;
      state.settings = { ...state.settings, ...patch };
      return saved("设置已保存", [{ kind: "settings", instanceId: null }]) as T;
    }
    case "toggle_bookmark": {
      const id = text(args, "id");
      const existing = state.bookmarks.find((item) => item.id === id);
      state.bookmarks = existing
        ? state.bookmarks.filter((item) => item.id !== id)
        : [...state.bookmarks, { id, kind: text(args, "kind"), name: text(args, "name"), description: text(args, "description") }];
      return saved(existing ? "已取消收藏" : "已收藏", [{ kind: "bookmarks", instanceId: null }], [id]) as T;
    }
    case "toggle_mod": {
      const instanceId = text(args, "instanceId");
      const modId = text(args, "modId");
      const mods = state.mods[instanceId] ?? [];
      const mod = mods.find((item) => item.id === modId);
      if (!mod) throw new DemoApiError("没有这个模组。", "NOT_FOUND");
      mod.enabled = boolean(args, "enabled");
      return saved(`${mod.enabled ? "已启用" : "已禁用"} ${mod.name}`, [{ kind: "instanceMods", instanceId }], [modId]) as T;
    }
    case "delete_mod": {
      const instanceId = text(args, "instanceId");
      const modId = text(args, "modId");
      state.mods[instanceId] = (state.mods[instanceId] ?? []).filter((item) => item.id !== modId);
      return saved("已删除模组", [{ kind: "instanceMods", instanceId }], [modId]) as T;
    }

    case "launch_game": {
      const instanceId = text(args, "instanceId");
      const accountId = text(args, "accountId");
      const task = startTask("launch", "启动游戏", instanceId);
      const sessionId = nextId("session");
      state.sessions = [
        { sessionId, instanceId, accountId, status: "running", pid: 4242, exitCode: null, error: null },
        ...state.sessions,
      ];
      return { sessionId, taskId: task.id } as T;
    }
    case "launch_to_server": {
      const instanceId = text(args, "instanceId");
      const accountId = text(args, "accountId");
      const task = startTask("launch", "启动并加入服务器", instanceId);
      const sessionId = nextId("session");
      state.sessions = [
        { sessionId, instanceId, accountId, status: "running", pid: 4242, exitCode: null, error: null },
        ...state.sessions,
      ];
      return { sessionId, taskId: task.id } as T;
    }
    case "stop_game": {
      const sessionId = text(args, "sessionId");
      state.sessions = state.sessions.map((item) =>
        item.sessionId === sessionId ? { ...item, status: "stopped", exitCode: 0 } : item,
      );
      return saved("已结束游戏进程", [{ kind: "tasks", instanceId: null }], [sessionId]) as T;
    }
    case "task_action": {
      const id = text(args, "id");
      const action = text(args, "action");
      state.tasks = state.tasks.map((task) =>
        task.id === id
          ? { ...task, status: action === "cancel" ? "cancelled" : task.status, updatedAt: now(), allowedActions: [] }
          : task,
      );
      return saved("任务已更新", [{ kind: "tasks", instanceId: null }], [id]) as T;
    }

    case "save_server": {
      const id = text(args, "id") || nextId("server");
      const server = {
        id,
        name: text(args, "name"),
        address: text(args, "address"),
        instanceId: text(args, "instanceId") || null,
      };
      state.servers = [...state.servers.filter((item) => item.id !== id), server];
      return saved("服务器已保存", [{ kind: "servers", instanceId: null }], [id]) as T;
    }
    case "delete_server": {
      const id = text(args, "id");
      state.servers = state.servers.filter((item) => item.id !== id);
      return saved("服务器已删除", [{ kind: "servers", instanceId: null }], [id]) as T;
    }
    case "ping_server": {
      const id = text(args, "id");
      const server: ServerPingInfo = {
        id,
        online: true,
        players: 12,
        max: 100,
        ping: 42,
        motd: "演示服务器 · 不会真正连接",
        version: "1.21.1",
        error: "",
      };
      return server as T;
    }

    case "start_login":
      return {
        state: "awaitingProfile",
        authSessionId: nextId("auth"),
        profiles: [
          { id: "profile-demo-1", name: "Steve_CN", skin: "" },
          { id: "profile-demo-2", name: "Alex_Demo", skin: "" },
        ],
      } as LoginSessionInfo as T;
    case "get_login_session":
    case "select_login_profile":
      return {
        state: "succeeded",
        authSessionId: text(args, "authSessionId") || "auth-demo",
        accountId: state.accounts[0]?.id ?? "account-demo",
      } as LoginSessionInfo as T;
    case "cancel_login":
      return { state: "cancelled", authSessionId: text(args, "authSessionId") || "auth-demo" } as LoginSessionInfo as T;

    case "scan_java": {
      const java: JavaInfo[] = [
        { path: state.settings.java, major: 21, arch: "x64", vendor: "Eclipse Adoptium", error: "" },
        { path: "C:\\Program Files\\Java\\jdk-17\\bin\\javaw.exe", major: 17, arch: "x64", vendor: "Microsoft", error: "" },
      ];
      return java as T;
    }
    case "verify_key":
      return { role: "admin", ok: true, message: "演示模式：密钥校验一律通过" } as T;
    case "test_data_source":
      return { name: text(args, "name") || "演示源", url: text(args, "url"), ok: true, message: "演示模式：连接成功", ms: 42 } as T;
    case "read_log":
      return "[demo] 这是演示日志。真实日志由内核读取。" as T;
    case "pick_file":
    case "pick_folder":
      return null as T;
    case "open_folder":
    case "reset_window":
    case "backup_saves":
    case "restore_save":
    case "queue_content_version":
    case "install_vanilla":
    case "install_loader":
    case "confirm_pack":
    case "apply_uninstall":
    case "apply_updates":
    case "clone_instance":
    case "export_instance":
    case "save_launch_options":
    case "save_recommendations":
    case "save_custom_sources":
    case "use_data_sources":
    case "remove_game_root":
    case "set_offline_skin":
      return saved("演示模式：操作已模拟", []) as T;

    case "list_save_backups":
      return [{ id: "backup-demo", created: "2026-11-20T12:00:00+08:00", worlds: ["world", "world_nether"] }] as T;
    case "list_vanilla_versions": {
      const versions: VanillaVersionInfo[] = [
        { id: "1.21.5", type: "release", released: "2026-03-25T00:00:00+00:00" },
        { id: "1.21.1", type: "release", released: "2024-08-08T00:00:00+00:00" },
        { id: "1.20.4", type: "release", released: "2023-12-07T00:00:00+00:00" },
      ];
      return versions as T;
    }
    case "list_loaders": {
      const loader: LoaderListInfo = {
        minecraftVersion: text(args, "minecraftVersion") || "1.21.1",
        loader: text(args, "loader") || "neoforge",
        versions: ["21.1.72", "21.1.68", "21.1.60"],
      };
      return loader as T;
    }
    case "search_content": {
      const item = (id: string, name: string, category: string) => ({
        id,
        category,
        name,
        description: "演示数据：不会下载任何文件。",
        author: "MCUG",
        icon: "",
        downloads: "1.2 万",
        tags: ["演示"],
        gameVersions: ["1.21.1"],
        loaders: ["neoforge"],
      });
      const result: ContentSearchResult = {
        category: text(args, "category") || "mod",
        requestId: nextId("search"),
        items: [
          item("demo-sodium", "Sodium", "mod"),
          item("demo-iris", "Iris Shaders", "mod"),
          item("demo-pack", "MCUG 轻量生存", "modpack"),
        ],
      };
      return result as T;
    }
    case "list_content_versions": {
      const result: ResourceVersionsInfo = {
        requestId: text(args, "requestId") || nextId("versions"),
        token: nextId("token"),
        game: "1.21.1",
        loader: "neoforge",
        items: [
          { id: "ver-demo-1", name: "sodium-0.6.9", files: ["sodium-0.6.9.jar"] },
          { id: "ver-demo-2", name: "sodium-0.6.5", files: ["sodium-0.6.5.jar"] },
        ],
      };
      return result as T;
    }
    case "check_updates":
      return { checkId: nextId("check"), minecraftVersion: "1.21.1", items: [] } as T;

    default:
      throw unsupported(command);
  }
}
