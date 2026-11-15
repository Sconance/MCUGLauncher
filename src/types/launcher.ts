/*
 * 与 src-tauri/src/model.rs 一一对应的前端数据模型。
 * 字段名与 Rust 的 serde(camelCase) 输出一致，命令返回值可直接使用。
 */

export interface ResourceSnapshot<T> {
  epoch: string;
  revision: number;
  data: T;
}

export interface InstanceInfo {
  id: string;
  name: string;
  loader: string;
  mods: number;
  updates: number;
  displayName: string;
  minecraft: string;
  isPlaceholder: boolean;
}

export interface ModInfo {
  id: string;
  name: string;
  file: string;
  version: string;
  enabled: boolean;
  path: string;
  size: number;
}

export interface SavedServerInfo {
  id: string;
  name: string;
  address: string;
  instanceId: string | null;
}

export interface FavoriteInfo {
  id: string;
  kind: string;
  name: string;
  description: string;
}

export interface AccountInfo {
  id: string;
  kind: "microsoft" | "mua" | "offline";
  player: string;
  status: "ready" | "expired" | "invalid";
}

export interface DataSourceSet {
  announcements: string;
  servers: string;
  recommendations: string;
  quotes: string;
}

export interface VersionChoice {
  name: string;
  meta: string;
}

export interface VersionGroupInfo {
  label: string;
  count: number;
  versions: VersionChoice[];
}

export interface PlayerInfo {
  id: string;
  name: string;
  /** microsoft = 正版 / mua = 外置登录 / offline = 离线 */
  playerType: string;
  /** 外置登录的验证服务器名称，正版与离线为空 */
  authServer: string;
  avatar: string;
}

export interface LauncherOverview {
  appVersion: string;
  selectedInstanceId: string | null;
  activeAccountId: string | null;
}

export interface SettingsInfo {
  root: string;
  gameRoots: string[];
  java: string;
  width: number;
  height: number;
  ram: number;
  autoMemory: boolean;
  anonymous: boolean;
  hasCommunityKey: boolean;
  hasAdminKey: boolean;
  skinStation: string;
  cape: string;
  dataSources: DataSourceSet;
  defaultDataSources: DataSourceSet;
  customDataSources: DataSourceSet;
  dataSourceMode: string;
  autoChinese: boolean;
}

export interface InstanceCatalogInfo {
  items: InstanceInfo[];
  versionGroups: VersionGroupInfo[];
}

export interface TaskInfo {
  id: string;
  name: string;
  kind: string;
  instanceId: string | null;
  status: "queued" | "running" | "paused" | "cancelling" | "succeeded" | "failed" | "cancelled";
  phase: string;
  progress: { completed: number; total: number | null; unit: "bytes" | "items" } | null;
  allowedActions: ("pause" | "resume" | "cancel" | "delete")[];
  error: { code: string; message: string; retryable: boolean } | null;
  result: { changed: { kind: string; instanceId: string | null }[]; entityIds: string[] } | null;
  createdAt: string;
  updatedAt: string;
}

export interface GameSessionInfo {
  sessionId: string;
  instanceId: string;
  accountId: string;
  status: "preparing" | "starting" | "running" | "stopping" | "exited" | "failed";
  pid: number | null;
  exitCode: number | null;
  error: { code: string; message: string; retryable: boolean } | null;
}

export interface ServerPingInfo {
  id: string;
  online: boolean;
  players: number;
  max: number;
  ping: number;
  motd: string;
  version: string;
  error: string;
}

export interface AnnouncementInfo {
  id: string;
  title: string;
  body: string;
  tag: string;
  author: string;
  publishedAt: string | null;
  imageUrl: string;
  markdown: string;
}

export interface RecommendationInfo {
  id: string;
  type: "server" | "material" | "shader";
  name: string;
  author: string;
  description: string;
  tags: string[];
  version: string;
  size: string;
  url: string;
}

export interface QuoteInfo {
  id: string;
  type: string;
  text: string;
  author: string;
  source: string;
  imageUrl: string;
}

export interface CommunityData {
  announcements: AnnouncementInfo[];
  recommendations: RecommendationInfo[];
  quotes: QuoteInfo[];
}

export interface JavaInfo {
  path: string;
  major: number;
  arch: string;
  vendor: string;
  error: string;
}

export interface SaveBackupInfo {
  id: string;
  created: string;
  worlds: string[];
}

export interface ContentItem {
  id: string;
  name: string;
  description: string;
  author: string;
  icon: string;
  downloads: string;
  tags: string[];
}

export interface ContentSearchResult {
  category: string;
  requestId: string;
  items: ContentItem[];
}

export interface ResourceVersionInfo {
  id: string;
  name: string;
  files: string[];
}

export interface ResourceVersionsInfo {
  requestId: string;
  token: string;
  game: string;
  loader: string;
  items: ResourceVersionInfo[];
}

export interface VanillaVersionInfo {
  id: string;
  type: string;
  released: string;
}

export interface LoaderListInfo {
  minecraftVersion: string;
  loader: string;
  versions: string[];
}

export interface LaunchSettings {
  autoMemory: boolean;
  fullScreen: boolean;
  ramMb: number;
  width: number;
  height: number;
  java: string;
  jvmArguments: string;
  windowTitle: string;
  server: string;
  autoChinese: boolean;
}

export interface LaunchOptionsInfo {
  requestId: string;
  token: string;
  instanceId: string;
  displayName: string;
  inherited: boolean;
  settings: LaunchSettings;
}

export interface UninstallPreviewInfo {
  planId: string;
  targetId: string;
  targetRevision: number;
  expiresAt: string;
  name: string;
  notice: string;
  paths: string[];
  shared: boolean;
}

export interface ContentUpdateInfo {
  id: string;
  name: string;
  before: string;
  after: string;
  available: boolean;
  error: string;
}

export interface ContentUpdatesInfo {
  checkId: string;
  minecraftVersion: string;
  items: ContentUpdateInfo[];
}

export interface ContentChangeInfo {
  project: string;
  before: string;
  after: string;
  path: string;
  oldPath: string;
}

export interface ContentUpdatePreviewInfo {
  planId: string;
  targetId: string;
  targetRevision: number;
  expiresAt: string;
  minecraftVersion: string;
  bytes: number;
  changes: ContentChangeInfo[];
}

export interface PackPreviewInfo {
  planId: string;
  targetId: string;
  targetRevision: number;
  expiresAt: string;
  name: string;
  root: string;
  game: string;
  loader: string;
}

export interface AvatarInfo {
  image: string;
  label: string;
  fallback: boolean;
  account: string;
  player: string;
}

export interface MuaProfileInfo {
  id: string;
  name: string;
}

export type LoginSessionInfo =
  | { state: "awaitingAuthorization"; authSessionId: string; authorizationUrl: string }
  | { state: "awaitingProfile"; authSessionId: string; profiles: MuaProfileInfo[] }
  | { state: "succeeded"; authSessionId: string; accountId: string }
  | { state: "failed"; authSessionId: string; error: { code: string; message: string; retryable: boolean } }
  | { state: "cancelled"; authSessionId: string }
  | { state: "expired"; authSessionId: string };

export interface SystemInfo {
  app: string;
  dotnet: string;
  webview: string;
  os: string;
  root: string;
  data: string;
}

export interface KeyResultInfo {
  role: string;
  ok: boolean;
  message: string;
}

export interface TestResultInfo {
  name: string;
  url: string;
  ok: boolean;
  message: string;
  ms: number;
}

export type SavedInfo = import("./generated").SavedInfo;
export type ResourceKeyInfo = import("./generated").ResourceKeyInfo;

/** `save_settings` 的补丁：只提交需要修改的字段。 */
export interface AppSettingsPatch {
  root?: string;
  java?: string;
  ram?: number;
  width?: number;
  height?: number;
  autoMemory?: boolean;
  autoChinese?: boolean;
  anonymous?: boolean;
  player?: string;
}

export interface LaunchAcceptedInfo {
  sessionId: string;
  taskId: string;
}
