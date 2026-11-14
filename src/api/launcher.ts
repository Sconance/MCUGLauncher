/*
 * 前端业务门面（当前为预览实现）。
 *
 * 每个方法对应 origin 里 ILauncherService 的一个强类型调用；接入后端时把函数体
 * 换成 invoke("command", args) 即可，页面不需要改动。
 */
import {
  avatar,
  backups,
  community,
  contentItems,
  games,
  javaList,
  loaderVersions,
  state,
  systemInfo,
  tasks,
  updates,
  vanillaVersions,
} from "../data/mock";
import type {
  AvatarInfo,
  CommunityData,
  ContentSearchResult,
  ContentUpdatePreviewInfo,
  ContentUpdatesInfo,
  DataSourceSet,
  GameInfo,
  JavaInfo,
  KeyResultInfo,
  LaunchOptionsInfo,
  LaunchResultInfo,
  LaunchSettings,
  LauncherState,
  LoaderListResult,
  MuaProfileInfo,
  PackPreviewInfo,
  RecommendationInfo,
  ResourceVersionsResult,
  SaveBackupInfo,
  SavedInfo,
  ServerPingInfo,
  ServerResultInfo,
  SystemInfo,
  TaskInfo,
  TestResultInfo,
  UninstallPreviewInfo,
  VanillaVersionsResult,
} from "../types/launcher";

const wait = (ms = 140) => new Promise<void>((resolve) => setTimeout(resolve, ms));
const clone = <T>(value: T): T => JSON.parse(JSON.stringify(value)) as T;
const saved = (message: string): SavedInfo => ({ message });
const ok = (message: string): LaunchResultInfo => ({ ok: true, message });

let current: LauncherState = clone(state);
let taskList: TaskInfo[] = clone(tasks);
let gameList: GameInfo[] = clone(games);
let communityData: CommunityData = clone(community);
let existingBackups: SaveBackupInfo[] = clone(backups);

const uninstallTokens = new Map<string, string>();
const updateTokens = new Map<string, string>();

function ping(address: string): ServerPingInfo {
  const offline = address.startsWith("creative");
  return {
    id: address,
    online: !offline,
    players: offline ? 0 : 23,
    max: offline ? 0 : 60,
    ping: offline ? 0 : 38,
    motd: offline ? "" : "MCUG 生存服 · 1.21.1 · 正版验证",
    version: offline ? "" : "1.21.1",
    error: offline ? "连接超时" : "",
  };
}

export const launcher = {
  /* ---------- 状态 ---------- */

  async getState(): Promise<LauncherState> {
    await wait();
    return clone(current);
  },

  async requestState(): Promise<LauncherState> {
    return this.getState();
  },

  async selectInstance(name: string): Promise<SavedInfo> {
    current = { ...current, selectedInstance: name, selectedVersion: name };
    return saved(`已切换到 ${name}`);
  },

  async selectVersion(name: string): Promise<SavedInfo> {
    current = { ...current, selectedVersion: name, selectedInstance: name };
    return saved(`已切换到 ${name}`);
  },

  /* ---------- 启动 ---------- */

  async launch(target: string): Promise<LaunchResultInfo> {
    await wait(420);
    return ok(`已启动 ${target}`);
  },

  async launchToServer(instance: string, address: string): Promise<LaunchResultInfo> {
    await wait(420);
    return ok(`正在用 ${instance} 打开 ${address}`);
  },

  async stopGame(id: string): Promise<SavedInfo> {
    gameList = gameList.filter((game) => game.id !== id);
    return saved("正在请求关闭游戏…");
  },

  /* ---------- 服务器 ---------- */

  async saveServer(name: string, address: string, oldAddress: string, instance = ""): Promise<ServerResultInfo> {
    await wait(200);
    const index = current.servers.findIndex((server) => server.address === oldAddress);
    const next = { name, address, instance };
    if (index >= 0) {
      current.servers[index] = next;
      return { ok: true, message: "服务器已更新。" };
    }
    if (current.servers.some((server) => server.address === address)) {
      return { ok: false, message: "这个地址已经在收藏里了。" };
    }
    current.servers = [...current.servers, next];
    return { ok: true, message: "服务器已添加。" };
  },

  async deleteServer(address: string): Promise<SavedInfo> {
    current.servers = current.servers.filter((server) => server.address !== address);
    return saved("服务器已删除。");
  },

  async pingServer(address: string): Promise<ServerPingInfo> {
    await wait(260);
    return ping(address);
  },

  /* ---------- 账号 ---------- */

  async readAvatar(): Promise<AvatarInfo> {
    await wait(80);
    return { ...avatar, player: current.player, label: accountLabel(current.accountType) };
  },

  async loginMicrosoft(): Promise<{ ok: boolean; pending: boolean; message: string }> {
    await wait(600);
    current = { ...current, player: "XiaoMu", accountType: "microsoft" };
    return { ok: true, pending: false, message: "微软账号登录成功。" };
  },

  async loginMua(account: string, _password = ""): Promise<{ ok: boolean; pending: boolean; message: string }> {
    await wait(500);
    current = { ...current, player: account || "MuaPlayer", accountType: "mua" };
    return { ok: true, pending: false, message: "MUA 账号登录成功。" };
  },

  async listMuaProfiles(): Promise<MuaProfileInfo[]> {
    await wait(200);
    return [
      { id: "p1", name: "MuaTester" },
      { id: "p2", name: "OtherTester" },
    ];
  },

  async switchAccountMode(mode: string): Promise<{ account: string; needsLogin: boolean; message: string }> {
    await wait(300);
    current = { ...current, accountType: mode };
    return { account: mode, needsLogin: false, message: `已切换到${accountLabel(mode)}。` };
  },

  async forgetAccount(mode: string): Promise<{ ok: boolean; pending: boolean; message: string }> {
    current = { ...current, accountType: "offline" };
    return { ok: true, pending: false, message: `已移除${accountLabel(mode)}。` };
  },

  async setOfflineSkin(path: string): Promise<AvatarInfo> {
    await wait(160);
    return { ...avatar, label: "离线账号 · 本地启动", account: "offline", player: current.player, fallback: path.length === 0 };
  },

  async selectMuaProfile(_token: string, id: string): Promise<SavedInfo> {
    await wait(200);
    return saved(`已选择角色 ${id}。`);
  },

  /* ---------- 设置 ---------- */

  async saveSettings(patch: Partial<LauncherState>): Promise<SavedInfo> {
    await wait(200);
    if (patch.root && patch.root !== current.root) {
      current = {
        ...current,
        gameRoots: Array.from(new Set([...current.gameRoots, current.root, patch.root])).filter((value) => value.length > 0),
      };
    }
    current = { ...current, ...patch };
    return saved("设置已保存。");
  },

  async saveCustomSources(sources: DataSourceSet): Promise<SavedInfo> {
    await wait(160);
    current = { ...current, customDataSources: sources };
    return saved("已保存自定义数据源，可点击「使用自定义」切换。");
  },

  async resetWindow(): Promise<SavedInfo> {
    await wait(80);
    return saved("已恢复默认窗口尺寸。");
  },

  async scanJava(): Promise<JavaInfo[]> {
    await wait(700);
    return clone(javaList);
  },

  async listSaveBackups(): Promise<SaveBackupInfo[]> {
    await wait(120);
    return clone(existingBackups);
  },

  async backupSaves(): Promise<SavedInfo> {
    await wait(500);
    existingBackups = [
      { id: `b${existingBackups.length + 1}`, created: new Date().toLocaleString("zh-CN", { hour12: false }), worlds: ["新世界"] },
      ...existingBackups,
    ];
    return saved("存档已备份。");
  },

  async restoreSave(id: string, world: string): Promise<SavedInfo> {
    await wait(400);
    return saved(`已恢复 ${world}（备份 ${id}）。`);
  },

  async readLog(): Promise<string> {
    await wait(120);
    return [
      "[2026-11-14 20:12:04] launcher 0.22.3 started",
      "[2026-11-14 20:12:05] config loaded: D:/Games/Minecraft/MCUG",
      "[2026-11-14 20:12:06] java 21.0.4 detected",
      "[2026-11-14 20:12:09] account: microsoft · XiaoMu",
      "[2026-11-14 20:12:12] task queue recovered: 1 running, 1 paused",
    ].join("\n");
  },

  async getSystemInfo(): Promise<SystemInfo> {
    await wait(80);
    return { ...systemInfo, root: current.root };
  },

  async openFolder(kind: string): Promise<SavedInfo> {
    await wait(80);
    return saved(`已打开${folderLabel(kind)}。`);
  },

  async testSource(name: string, url: string): Promise<TestResultInfo> {
    await wait(600);
    const valid = url.startsWith("http");
    return { name, url, ok: valid, message: valid ? "地址可访问（模拟）。" : "地址必须以 http(s) 开头。", ms: 128 };
  },

  async useDataSources(mode: string): Promise<SavedInfo> {
    current = { ...current, dataSourceMode: mode };
    return saved(mode === "custom" ? "已切换为自定义数据源。" : "已恢复默认数据源。");
  },

  async resetDataSources(): Promise<SavedInfo> {
    current = { ...current, dataSourceMode: "default", customDataSources: { announcements: "", servers: "", recommendations: "", quotes: "" } };
    return saved("已恢复默认数据源。");
  },

  async verifyKey(role: string, key: string): Promise<KeyResultInfo> {
    await wait(500);
    const okKey = key.trim().length >= 6;
    return { role, ok: okKey, message: okKey ? "校验通过（模拟）。" : "Key 至少 6 位。" };
  },

  /* ---------- 下载 ---------- */

  async searchContent(category: string, query: string): Promise<ContentSearchResult> {
    await wait(300);
    const keyword = query.trim().toLowerCase();
    const items = contentItems.filter(
      (item) => keyword.length === 0 || item.name.toLowerCase().includes(keyword) || item.tags.join(" ").toLowerCase().includes(keyword),
    );
    return { category, requestId: "req-1", items: clone(items), error: "" };
  },

  async listContentVersions(id: string): Promise<ResourceVersionsResult> {
    await wait(320);
    const item = contentItems.find((entry) => entry.id === id);
    return {
      ok: true,
      result: {
        requestId: "req-1",
        token: `token-${id}`,
        game: current.selectedVersion,
        loader: "Fabric",
        items: [
          { id: `${id}-1`, name: `${item?.name ?? id} 1.21.1`, files: [`${id}-1.21.1.jar`] },
          { id: `${id}-2`, name: `${item?.name ?? id} 1.20.6`, files: [`${id}-1.20.6.jar`] },
          { id: `${id}-3`, name: `${item?.name ?? id} 1.20.1`, files: [`${id}-1.20.1.jar`] },
        ],
      },
      error: "",
    };
  },

  async queueContentVersion(_token: string, versionId: string, _mode = "instance", _path = ""): Promise<SavedInfo> {
    await wait(240);
    return saved(`已加入下载队列：${versionId}`);
  },

  async toggleBookmark(id: string, name: string, description: string, _author: string, kind: string): Promise<SavedInfo> {
    const exists = current.favorites.some((favorite) => favorite.id === id);
    current.favorites = exists
      ? current.favorites.filter((favorite) => favorite.id !== id)
      : [...current.favorites, { id, kind, name, description }];
    return saved(exists ? `已取消收藏 ${name}。` : `已收藏 ${name}。`);
  },

  async listVanillaVersions(): Promise<VanillaVersionsResult> {
    await wait(260);
    return { ok: true, items: clone(vanillaVersions), error: "" };
  },

  async installVanilla(version: string): Promise<SavedInfo> {
    await wait(300);
    return saved(`已加入安装队列：${version}`);
  },

  async listLoaders(version: string, loader: string): Promise<LoaderListResult> {
    await wait(320);
    return { ok: true, result: { version, loader, versions: loaderVersions[loader] ?? [] }, error: "" };
  },

  async installLoader(version: string, loader: string, loaderVersion: string): Promise<SavedInfo> {
    await wait(300);
    return saved(`已加入安装队列：${loader} ${loaderVersion} · ${version}`);
  },

  async previewPack(path: string): Promise<PackPreviewInfo> {
    await wait(400);
    const file = path.split(/[\\/]/).pop() ?? "整合包";
    return { token: "pack-1", name: file.replace(/\.(mrpack|zip)$/i, ""), root: current.root, game: "1.21.1", loader: "Fabric" };
  },

  async confirmPack(_token: string, name: string, root: string): Promise<SavedInfo> {
    await wait(400);
    return saved(`整合包「${name}」已导入到 ${root}。`);
  },

  /* ---------- 实例维护 ---------- */

  async previewUninstall(version: string): Promise<UninstallPreviewInfo> {
    await wait(260);
    const token = `uninstall-${version}`;
    uninstallTokens.set(token, version);
    return {
      token,
      version,
      name: version,
      notice: "只删除列出的路径；默认保留共享游戏根目录数据。",
      paths: [`versions/${version}`, `instances/${version}.json`],
      shared: false,
    };
  },

  async applyUninstall(token: string): Promise<SavedInfo> {
    const version = uninstallTokens.get(token) ?? token;
    uninstallTokens.delete(token);
    await wait(320);
    return saved(`已加入卸载队列：${version}`);
  },

  async cloneInstance(version: string, name: string, _saves = false, _servers = false): Promise<SavedInfo> {
    await wait(400);
    return saved(`已克隆 ${version} 为 ${name}。`);
  },

  async exportInstance(version: string, path: string, _servers = false, _saves = false): Promise<SavedInfo> {
    await wait(400);
    return saved(`已导出 ${version} 到 ${path}。`);
  },

  async checkUpdates(version: string): Promise<ContentUpdatesInfo> {
    await wait(500);
    const token = `updates-${version}`;
    updateTokens.set(token, version);
    return { token, version, items: clone(updates) };
  },

  async previewUpdates(token: string): Promise<ContentUpdatePreviewInfo> {
    await wait(500);
    return {
      token,
      version: updateTokens.get(token) ?? current.selectedVersion,
      bytes: 8_412_160,
      changes: updates.map((item) => ({
        project: item.name,
        before: item.before,
        after: item.after,
        path: `mods/${item.id}.jar`,
        oldPath: "",
      })),
    };
  },

  async applyUpdates(token: string): Promise<SavedInfo> {
    await wait(300);
    updateTokens.delete(token);
    return saved("更新任务已加入队列。");
  },

  async toggleMod(file: string, enabled: boolean): Promise<SavedInfo> {
    current.mods = current.mods.map((mod) => (mod.file === file ? { ...mod, enabled } : mod));
    return saved(enabled ? "模组已启用。" : "模组已禁用。");
  },

  async deleteMod(file: string): Promise<SavedInfo> {
    current.mods = current.mods.filter((mod) => mod.file !== file);
    return saved("模组已删除。");
  },

  async readLaunchOptions(version: string): Promise<LaunchOptionsInfo> {
    await wait(220);
    return {
      requestId: "req-1",
      token: `launch-${version}`,
      version,
      name: version,
      inherited: false,
      settings: {
        autoMemory: current.autoMemory,
        fullScreen: false,
        ramMb: current.ram,
        width: current.width,
        height: current.height,
        java: current.java,
        jvmArguments: "",
        windowTitle: "MCUG Launcher",
        server: "",
        autoChinese: current.autoChinese,
      },
    };
  },

  async saveLaunchOptions(_token: string, settings: LaunchSettings): Promise<SavedInfo> {
    await wait(260);
    current = { ...current, ram: settings.ramMb, width: settings.width, height: settings.height, java: settings.java };
    return saved("启动选项已保存。");
  },

  /* ---------- 任务与游戏 ---------- */

  async listTasks(): Promise<TaskInfo[]> {
    await wait(100);
    return clone(taskList);
  },

  async taskAction(id: string, action: string): Promise<SavedInfo> {
    await wait(200);
    taskList = taskList.map((task) => {
      if (task.id !== id) return task;
      if (action === "cancel" || action === "delete") return { ...task, state: action === "delete" ? "Deleted" : "Cancelled" };
      if (action === "pause") return { ...task, state: "Paused" };
      if (action === "resume") return { ...task, state: "Running" };
      return task;
    });
    if (action === "delete") taskList = taskList.filter((task) => task.id !== id);
    return saved(`任务已${action === "pause" ? "暂停" : action === "resume" ? "继续" : action === "cancel" ? "取消" : "删除"}。`);
  },

  async listGames(): Promise<GameInfo[]> {
    await wait(100);
    return clone(gameList);
  },

  /* ---------- 社区 ---------- */

  async getCommunity(): Promise<CommunityData> {
    await wait(180);
    return clone(communityData);
  },

  async refreshCommunity(): Promise<CommunityData> {
    await wait(500);
    return clone(communityData);
  },

  async saveRecommendations(items: RecommendationInfo[]): Promise<SavedInfo> {
    await wait(300);
    communityData = { ...communityData, recommendations: items };
    return saved("本地内容已更新。");
  },
};

function accountLabel(mode: string): string {
  if (mode === "microsoft") return "正版账号 · 在线验证";
  if (mode === "mua") return "MUA 账号 · 外置登录";
  return "离线账号 · 本地启动";
}

function folderLabel(kind: string): string {
  if (kind === "version") return "版本文件夹";
  if (kind === "saves") return "存档文件夹";
  if (kind === "config") return "配置目录";
  return "游戏目录";
}
