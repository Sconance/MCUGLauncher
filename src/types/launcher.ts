/*
 * 与 origin/src/MCUGLauncher.App/Contracts 对应的前端数据模型。
 * 字段名保持 C# JSON(Web) 的 camelCase，后端接入后可直接反序列化。
 */

export interface InstanceInfo {
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
  name: string;
  address: string;
  instance: string;
}

export interface FavoriteInfo {
  id: string;
  kind: string;
  name: string;
  description: string;
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

export interface LauncherState {
  appVersion: string;
  player: string;
  accountType: string;
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
  selectedVersion: string;
  selectedInstance: string;
  instances: InstanceInfo[];
  mods: ModInfo[];
  servers: SavedServerInfo[];
  favorites: FavoriteInfo[];
  versionGroups: VersionGroupInfo[];
  dataSources: DataSourceSet;
  defaultDataSources: DataSourceSet;
  customDataSources: DataSourceSet;
  dataSourceMode: string;
  autoChinese: boolean;
}

export interface TaskInfo {
  id: string;
  name: string;
  state: string;
  percent: number;
  message: string;
  committing: boolean;
}

export interface GameInfo {
  id: string;
  version: string;
  player: string;
  pid: number;
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
  error: string;
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

export interface ResourceVersionsResult {
  ok: boolean;
  result: ResourceVersionsInfo | null;
  error: string;
}

export interface VanillaVersionInfo {
  id: string;
  type: string;
  released: string;
}

export interface VanillaVersionsResult {
  ok: boolean;
  items: VanillaVersionInfo[];
  error: string;
}

export interface LoaderListInfo {
  version: string;
  loader: string;
  versions: string[];
}

export interface LoaderListResult {
  ok: boolean;
  result: LoaderListInfo | null;
  error: string;
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
  version: string;
  name: string;
  inherited: boolean;
  settings: LaunchSettings;
}

export interface UninstallPreviewInfo {
  token: string;
  version: string;
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
  token: string;
  version: string;
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
  token: string;
  version: string;
  bytes: number;
  changes: ContentChangeInfo[];
}

export interface PackPreviewInfo {
  token: string;
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

export interface SavedInfo {
  message: string;
}

export interface LaunchResultInfo {
  ok: boolean;
  message: string;
}

export interface ServerResultInfo {
  ok: boolean;
  message: string;
}
