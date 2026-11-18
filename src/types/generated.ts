/* eslint-disable */
/**
 * GENERATED FILE — do not edit by hand.
 * Source: schemas/*.schema.json
 * Run: npm run dto:generate
 */

/**
 * LoginSessionInfo：通过 Tauri 命令在前后端之间传输的数据对象。
 */
export type LoginSessionInfo =
  | {
      state: "awaitingAuthorization";
      authSessionId: string;
      authorizationUrl: string;
    }
  | {
      state: "awaitingProfile";
      authSessionId: string;
      profiles: unknown[];
    }
  | {
      state: "succeeded";
      authSessionId: string;
      accountId: string;
    }
  | {
      state: "failed";
      authSessionId: string;
      error: ApiError;
    }
  | {
      state: "cancelled" | "expired";
      authSessionId: string;
    };

export interface LauncherDTO {
  ApiError?: ApiError;
  ResourceKeyInfo?: ResourceKeyInfo;
  SavedInfo?: SavedInfo;
  ResourceSnapshot?: ResourceSnapshot;
  AccountInfo?: AccountInfo;
  MuaProfileInfo?: MuaProfileInfo;
  LoginSessionInfo?: LoginSessionInfo;
  AvatarInfo?: AvatarInfo;
  AnnouncementInfo?: AnnouncementInfo;
  RecommendationInfo?: RecommendationInfo;
  QuoteInfo?: QuoteInfo;
  CommunityData?: CommunityData;
  ResourceVersionInfo?: ResourceVersionInfo;
  ResourceVersionsInfo?: ResourceVersionsInfo;
  VanillaVersionInfo?: VanillaVersionInfo;
  ContentItem?: ContentItem;
  ContentSearchResult?: ContentSearchResult;
  LoaderListInfo?: LoaderListInfo;
  LaunchSettings?: LaunchSettings;
  LaunchOptionsInfo?: LaunchOptionsInfo;
  VersionChoice?: VersionChoice;
  VersionGroupInfo?: VersionGroupInfo;
  LauncherOverview?: LauncherOverview;
  InstanceInfo?: InstanceInfo;
  InstanceCatalogInfo?: InstanceCatalogInfo;
  ModInfo?: ModInfo;
  SavedServerInfo?: SavedServerInfo;
  FavoriteInfo?: FavoriteInfo;
  DataSourceSet?: DataSourceSet;
  SettingsInfo?: SettingsInfo;
  AppSettingsPatch?: AppSettingsPatch;
  ContentUpdateInfo?: ContentUpdateInfo;
  ContentUpdatesInfo?: ContentUpdatesInfo;
  ContentChangeInfo?: ContentChangeInfo;
  ContentUpdatePreviewInfo?: ContentUpdatePreviewInfo;
  PackPreviewInfo?: PackPreviewInfo;
  JavaInfo?: JavaInfo;
  ServerPingInfo?: ServerPingInfo;
  SaveBackupInfo?: SaveBackupInfo;
  SystemInfoInfo?: SystemInfoInfo;
  KeyResultInfo?: KeyResultInfo;
  TestResultInfo?: TestResultInfo;
  UninstallPreviewInfo?: UninstallPreviewInfo;
  TaskProgressInfo?: TaskProgressInfo;
  TaskResultInfo?: TaskResultInfo;
  TaskInfo?: TaskInfo;
  GameSessionInfo?: GameSessionInfo;
  LaunchAcceptedInfo?: LaunchAcceptedInfo;
}
/**
 * ApiError：通过 Tauri 命令在前后端之间传输的数据对象。
 */
export interface ApiError {
  /**
   * code 字段。
   */
  code:
    | "VALIDATION_ERROR"
    | "NOT_FOUND"
    | "CONFLICT"
    | "BUSY"
    | "AUTH_REQUIRED"
    | "NETWORK_ERROR"
    | "IO_ERROR"
    | "PLAN_EXPIRED"
    | "INTERNAL_ERROR"
    | "UNSUPPORTED";
  /**
   * message 字段。
   */
  message: string;
  /**
   * retryable 字段。
   */
  retryable: boolean;
  /**
   * fieldErrors 字段。
   */
  fieldErrors?: {
    [k: string]: string;
  };
}
/**
 * ResourceKeyInfo：通过 Tauri 命令在前后端之间传输的数据对象。
 */
export interface ResourceKeyInfo {
  /**
   * kind 字段。
   */
  kind: string;
  /**
   * instanceId 字段。
   */
  instanceId: string | null;
}
/**
 * SavedInfo：通过 Tauri 命令在前后端之间传输的数据对象。
 */
export interface SavedInfo {
  /**
   * message 字段。
   */
  message: string;
  /**
   * changed 字段。
   */
  changed: ResourceKeyInfo[];
  /**
   * entityIds 字段。
   */
  entityIds: string[];
}
/**
 * ResourceSnapshot：通过 Tauri 命令在前后端之间传输的数据对象。
 */
export interface ResourceSnapshot {
  /**
   * epoch 字段。
   */
  epoch: string;
  /**
   * revision 字段。
   */
  revision: number;
  /**
   * data 字段。
   */
  data: {
    [k: string]: unknown;
  };
}
/**
 * AccountInfo：通过 Tauri 命令在前后端之间传输的数据对象。
 */
export interface AccountInfo {
  /**
   * id 字段。
   */
  id: string;
  /**
   * kind 字段。
   */
  kind: string;
  /**
   * player 字段。
   */
  player: string;
  /**
   * status 字段。
   */
  status: string;
}
/**
 * MuaProfileInfo：通过 Tauri 命令在前后端之间传输的数据对象。
 */
export interface MuaProfileInfo {
  /**
   * id 字段。
   */
  id: string;
  /**
   * name 字段。
   */
  name: string;
}
/**
 * AvatarInfo：通过 Tauri 命令在前后端之间传输的数据对象。
 */
export interface AvatarInfo {
  /**
   * image 字段。
   */
  image: string;
  /**
   * label 字段。
   */
  label: string;
  /**
   * fallback 字段。
   */
  fallback: boolean;
  /**
   * account 字段。
   */
  account: string;
  /**
   * player 字段。
   */
  player: string;
}
/**
 * AnnouncementInfo：通过 Tauri 命令在前后端之间传输的数据对象。
 */
export interface AnnouncementInfo {
  /**
   * id 字段。
   */
  id: string;
  /**
   * title 字段。
   */
  title: string;
  /**
   * body 字段。
   */
  body: string;
  /**
   * tag 字段。
   */
  tag: string;
  /**
   * author 字段。
   */
  author: string;
  /**
   * publishedAt 字段。
   */
  publishedAt: string | null;
  /**
   * imageUrl 字段。
   */
  imageUrl: string;
  /**
   * markdown 字段。
   */
  markdown: string;
}
/**
 * RecommendationInfo：通过 Tauri 命令在前后端之间传输的数据对象。
 */
export interface RecommendationInfo {
  /**
   * id 字段。
   */
  id: string;
  /**
   * type 字段。
   */
  type: string;
  /**
   * name 字段。
   */
  name: string;
  /**
   * author 字段。
   */
  author: string;
  /**
   * description 字段。
   */
  description: string;
  /**
   * tags 字段。
   */
  tags: string[];
  /**
   * version 字段。
   */
  version: string;
  /**
   * size 字段。
   */
  size: string;
  /**
   * url 字段。
   */
  url: string;
}
/**
 * QuoteInfo：通过 Tauri 命令在前后端之间传输的数据对象。
 */
export interface QuoteInfo {
  /**
   * id 字段。
   */
  id: string;
  /**
   * type 字段。
   */
  type: string;
  /**
   * text 字段。
   */
  text: string;
  /**
   * author 字段。
   */
  author: string;
  /**
   * source 字段。
   */
  source: string;
  /**
   * imageUrl 字段。
   */
  imageUrl: string;
}
/**
 * CommunityData：通过 Tauri 命令在前后端之间传输的数据对象。
 */
export interface CommunityData {
  /**
   * announcements 字段。
   */
  announcements: AnnouncementInfo[];
  /**
   * recommendations 字段。
   */
  recommendations: RecommendationInfo[];
  /**
   * quotes 字段。
   */
  quotes: QuoteInfo[];
}
/**
 * ResourceVersionInfo：通过 Tauri 命令在前后端之间传输的数据对象。
 */
export interface ResourceVersionInfo {
  /**
   * id 字段。
   */
  id: string;
  /**
   * name 字段。
   */
  name: string;
  /**
   * files 字段。
   */
  files: string[];
}
/**
 * ResourceVersionsInfo：通过 Tauri 命令在前后端之间传输的数据对象。
 */
export interface ResourceVersionsInfo {
  /**
   * requestId 字段。
   */
  requestId: string;
  /**
   * token 字段。
   */
  token: string;
  /**
   * game 字段。
   */
  game: string;
  /**
   * loader 字段。
   */
  loader: string;
  /**
   * items 字段。
   */
  items: ResourceVersionInfo[];
}
/**
 * VanillaVersionInfo：通过 Tauri 命令在前后端之间传输的数据对象。
 */
export interface VanillaVersionInfo {
  /**
   * id 字段。
   */
  id: string;
  /**
   * type 字段。
   */
  type: string;
  /**
   * released 字段。
   */
  released: string;
}
/**
 * ContentItem：通过 Tauri 命令在前后端之间传输的数据对象。
 */
export interface ContentItem {
  /**
   * id 字段。
   */
  id: string;
  /**
   * category 字段。
   */
  category: string;
  /**
   * name 字段。
   */
  name: string;
  /**
   * description 字段。
   */
  description: string;
  /**
   * author 字段。
   */
  author: string;
  /**
   * icon 字段。
   */
  icon: string;
  /**
   * downloads 字段。
   */
  downloads: string;
  /**
   * tags 字段。
   */
  tags: string[];
  /**
   * gameVersions 字段。
   */
  gameVersions: string[];
  /**
   * loaders 字段。
   */
  loaders: string[];
}
/**
 * ContentSearchResult：通过 Tauri 命令在前后端之间传输的数据对象。
 */
export interface ContentSearchResult {
  /**
   * category 字段。
   */
  category: string;
  /**
   * requestId 字段。
   */
  requestId: string;
  /**
   * items 字段。
   */
  items: ContentItem[];
}
/**
 * LoaderListInfo：通过 Tauri 命令在前后端之间传输的数据对象。
 */
export interface LoaderListInfo {
  /**
   * minecraftVersion 字段。
   */
  minecraftVersion: string;
  /**
   * loader 字段。
   */
  loader: string;
  /**
   * versions 字段。
   */
  versions: string[];
}
/**
 * LaunchSettings：通过 Tauri 命令在前后端之间传输的数据对象。
 */
export interface LaunchSettings {
  /**
   * autoMemory 字段。
   */
  autoMemory: boolean;
  /**
   * fullScreen 字段。
   */
  fullScreen: boolean;
  /**
   * ramMb 字段。
   */
  ramMb: number;
  /**
   * width 字段。
   */
  width: number;
  /**
   * height 字段。
   */
  height: number;
  /**
   * java 字段。
   */
  java: string;
  /**
   * jvmArguments 字段。
   */
  jvmArguments: string;
  /**
   * windowTitle 字段。
   */
  windowTitle: string;
  /**
   * server 字段。
   */
  server: string;
  /**
   * autoChinese 字段。
   */
  autoChinese: boolean;
}
/**
 * LaunchOptionsInfo：通过 Tauri 命令在前后端之间传输的数据对象。
 */
export interface LaunchOptionsInfo {
  /**
   * requestId 字段。
   */
  requestId: string;
  /**
   * token 字段。
   */
  token: string;
  /**
   * instanceId 字段。
   */
  instanceId: string;
  /**
   * displayName 字段。
   */
  displayName: string;
  /**
   * inherited 字段。
   */
  inherited: boolean;
  settings: LaunchSettings1;
}
/**
 * LaunchSettings：通过 Tauri 命令在前后端之间传输的数据对象。
 */
export interface LaunchSettings1 {
  /**
   * autoMemory 字段。
   */
  autoMemory: boolean;
  /**
   * fullScreen 字段。
   */
  fullScreen: boolean;
  /**
   * ramMb 字段。
   */
  ramMb: number;
  /**
   * width 字段。
   */
  width: number;
  /**
   * height 字段。
   */
  height: number;
  /**
   * java 字段。
   */
  java: string;
  /**
   * jvmArguments 字段。
   */
  jvmArguments: string;
  /**
   * windowTitle 字段。
   */
  windowTitle: string;
  /**
   * server 字段。
   */
  server: string;
  /**
   * autoChinese 字段。
   */
  autoChinese: boolean;
}
/**
 * 版本选择项。
 */
export interface VersionChoice {
  /**
   * 版本名称。
   */
  name: string;
  /**
   * 版本元数据。
   */
  meta: string;
}
/**
 * 按标签聚合的版本列表。
 */
export interface VersionGroupInfo {
  /**
   * 分组标签。
   */
  label: string;
  /**
   * 版本数量。
   */
  count: number;
  /**
   * 分组中的版本。
   */
  versions: VersionChoice[];
}
/**
 * LauncherOverview：通过 Tauri 命令在前后端之间传输的数据对象。
 */
export interface LauncherOverview {
  /**
   * appVersion 字段。
   */
  appVersion: string;
  /**
   * selectedInstanceId 字段。
   */
  selectedInstanceId: string | null;
  /**
   * activeAccountId 字段。
   */
  activeAccountId: string | null;
}
/**
 * InstanceInfo：通过 Tauri 命令在前后端之间传输的数据对象。
 */
export interface InstanceInfo {
  /**
   * id 字段。
   */
  id: string;
  /**
   * name 字段。
   */
  name: string;
  /**
   * displayName 字段。
   */
  displayName: string;
  /**
   * loader 字段。
   */
  loader: string;
  /**
   * minecraft 字段。
   */
  minecraft: string;
  /**
   * mods 字段。
   */
  mods: number;
  /**
   * updates 字段。
   */
  updates: number;
  /**
   * isPlaceholder 字段。
   */
  isPlaceholder: boolean;
}
/**
 * InstanceCatalogInfo：通过 Tauri 命令在前后端之间传输的数据对象。
 */
export interface InstanceCatalogInfo {
  /**
   * items 字段。
   */
  items: InstanceInfo[];
  /**
   * versionGroups 字段。
   */
  versionGroups: VersionGroupInfo[];
}
/**
 * ModInfo：通过 Tauri 命令在前后端之间传输的数据对象。
 */
export interface ModInfo {
  /**
   * id 字段。
   */
  id: string;
  /**
   * name 字段。
   */
  name: string;
  /**
   * file 字段。
   */
  file: string;
  /**
   * version 字段。
   */
  version: string;
  /**
   * enabled 字段。
   */
  enabled: boolean;
  /**
   * path 字段。
   */
  path: string;
  /**
   * size 字段。
   */
  size: number;
}
/**
 * SavedServerInfo：通过 Tauri 命令在前后端之间传输的数据对象。
 */
export interface SavedServerInfo {
  /**
   * id 字段。
   */
  id: string;
  /**
   * name 字段。
   */
  name: string;
  /**
   * address 字段。
   */
  address: string;
  /**
   * instanceId 字段。
   */
  instanceId: string | null;
}
/**
 * FavoriteInfo：通过 Tauri 命令在前后端之间传输的数据对象。
 */
export interface FavoriteInfo {
  /**
   * id 字段。
   */
  id: string;
  /**
   * kind 字段。
   */
  kind: string;
  /**
   * name 字段。
   */
  name: string;
  /**
   * description 字段。
   */
  description: string;
}
/**
 * DataSourceSet：通过 Tauri 命令在前后端之间传输的数据对象。
 */
export interface DataSourceSet {
  /**
   * announcements 字段。
   */
  announcements: string;
  /**
   * servers 字段。
   */
  servers: string;
  /**
   * recommendations 字段。
   */
  recommendations: string;
  /**
   * quotes 字段。
   */
  quotes: string;
}
/**
 * SettingsInfo：通过 Tauri 命令在前后端之间传输的数据对象。
 */
export interface SettingsInfo {
  /**
   * root 字段。
   */
  root: string;
  /**
   * gameRoots 字段。
   */
  gameRoots: string[];
  /**
   * java 字段。
   */
  java: string;
  /**
   * width 字段。
   */
  width: number;
  /**
   * height 字段。
   */
  height: number;
  /**
   * ram 字段。
   */
  ram: number;
  /**
   * autoMemory 字段。
   */
  autoMemory: boolean;
  /**
   * anonymous 字段。
   */
  anonymous: boolean;
  /**
   * hasCommunityKey 字段。
   */
  hasCommunityKey: boolean;
  /**
   * hasAdminKey 字段。
   */
  hasAdminKey: boolean;
  /**
   * skinStation 字段。
   */
  skinStation: string;
  /**
   * cape 字段。
   */
  cape: string;
  dataSources: DataSourceSet1;
  defaultDataSources: DataSourceSet2;
  customDataSources: DataSourceSet3;
  /**
   * dataSourceMode 字段。
   */
  dataSourceMode: string;
  /**
   * autoChinese 字段。
   */
  autoChinese: boolean;
}
/**
 * DataSourceSet：通过 Tauri 命令在前后端之间传输的数据对象。
 */
export interface DataSourceSet1 {
  /**
   * announcements 字段。
   */
  announcements: string;
  /**
   * servers 字段。
   */
  servers: string;
  /**
   * recommendations 字段。
   */
  recommendations: string;
  /**
   * quotes 字段。
   */
  quotes: string;
}
/**
 * DataSourceSet：通过 Tauri 命令在前后端之间传输的数据对象。
 */
export interface DataSourceSet2 {
  /**
   * announcements 字段。
   */
  announcements: string;
  /**
   * servers 字段。
   */
  servers: string;
  /**
   * recommendations 字段。
   */
  recommendations: string;
  /**
   * quotes 字段。
   */
  quotes: string;
}
/**
 * DataSourceSet：通过 Tauri 命令在前后端之间传输的数据对象。
 */
export interface DataSourceSet3 {
  /**
   * announcements 字段。
   */
  announcements: string;
  /**
   * servers 字段。
   */
  servers: string;
  /**
   * recommendations 字段。
   */
  recommendations: string;
  /**
   * quotes 字段。
   */
  quotes: string;
}
/**
 * AppSettingsPatch：通过 Tauri 命令在前后端之间传输的数据对象。
 */
export interface AppSettingsPatch {
  /**
   * root 字段。
   */
  root?: string;
  /**
   * java 字段。
   */
  java?: string;
  /**
   * ram 字段。
   */
  ram?: number;
  /**
   * width 字段。
   */
  width?: number;
  /**
   * height 字段。
   */
  height?: number;
  /**
   * autoMemory 字段。
   */
  autoMemory?: boolean;
  /**
   * autoChinese 字段。
   */
  autoChinese?: boolean;
  /**
   * anonymous 字段。
   */
  anonymous?: boolean;
  /**
   * player 字段。
   */
  player?: string;
}
/**
 * ContentUpdateInfo：通过 Tauri 命令在前后端之间传输的数据对象。
 */
export interface ContentUpdateInfo {
  /**
   * id 字段。
   */
  id: string;
  /**
   * name 字段。
   */
  name: string;
  /**
   * before 字段。
   */
  before: string;
  /**
   * after 字段。
   */
  after: string;
  /**
   * available 字段。
   */
  available: boolean;
  /**
   * error 字段。
   */
  error: string;
}
/**
 * ContentUpdatesInfo：通过 Tauri 命令在前后端之间传输的数据对象。
 */
export interface ContentUpdatesInfo {
  /**
   * checkId 字段。
   */
  checkId: string;
  /**
   * minecraftVersion 字段。
   */
  minecraftVersion: string;
  /**
   * items 字段。
   */
  items: ContentUpdateInfo[];
}
/**
 * ContentChangeInfo：通过 Tauri 命令在前后端之间传输的数据对象。
 */
export interface ContentChangeInfo {
  /**
   * project 字段。
   */
  project: string;
  /**
   * before 字段。
   */
  before: string;
  /**
   * after 字段。
   */
  after: string;
  /**
   * path 字段。
   */
  path: string;
  /**
   * oldPath 字段。
   */
  oldPath: string;
}
/**
 * ContentUpdatePreviewInfo：通过 Tauri 命令在前后端之间传输的数据对象。
 */
export interface ContentUpdatePreviewInfo {
  /**
   * planId 字段。
   */
  planId: string;
  /**
   * targetId 字段。
   */
  targetId: string;
  /**
   * targetRevision 字段。
   */
  targetRevision: number;
  /**
   * expiresAt 字段。
   */
  expiresAt: string;
  /**
   * minecraftVersion 字段。
   */
  minecraftVersion: string;
  /**
   * bytes 字段。
   */
  bytes: number;
  /**
   * changes 字段。
   */
  changes: ContentChangeInfo[];
}
/**
 * PackPreviewInfo：通过 Tauri 命令在前后端之间传输的数据对象。
 */
export interface PackPreviewInfo {
  /**
   * planId 字段。
   */
  planId: string;
  /**
   * targetId 字段。
   */
  targetId: string;
  /**
   * targetRevision 字段。
   */
  targetRevision: number;
  /**
   * expiresAt 字段。
   */
  expiresAt: string;
  /**
   * name 字段。
   */
  name: string;
  /**
   * root 字段。
   */
  root: string;
  /**
   * game 字段。
   */
  game: string;
  /**
   * loader 字段。
   */
  loader: string;
}
/**
 * JavaInfo：通过 Tauri 命令在前后端之间传输的数据对象。
 */
export interface JavaInfo {
  /**
   * path 字段。
   */
  path: string;
  /**
   * major 字段。
   */
  major: number;
  /**
   * arch 字段。
   */
  arch: string;
  /**
   * vendor 字段。
   */
  vendor: string;
  /**
   * error 字段。
   */
  error: string;
}
/**
 * ServerPingInfo：通过 Tauri 命令在前后端之间传输的数据对象。
 */
export interface ServerPingInfo {
  /**
   * id 字段。
   */
  id: string;
  /**
   * online 字段。
   */
  online: boolean;
  /**
   * players 字段。
   */
  players: number;
  /**
   * max 字段。
   */
  max: number;
  /**
   * ping 字段。
   */
  ping: number;
  /**
   * motd 字段。
   */
  motd: string;
  /**
   * version 字段。
   */
  version: string;
  /**
   * error 字段。
   */
  error: string;
}
/**
 * SaveBackupInfo：通过 Tauri 命令在前后端之间传输的数据对象。
 */
export interface SaveBackupInfo {
  /**
   * id 字段。
   */
  id: string;
  /**
   * created 字段。
   */
  created: string;
  /**
   * worlds 字段。
   */
  worlds: string[];
}
/**
 * SystemInfoInfo：通过 Tauri 命令在前后端之间传输的数据对象。
 */
export interface SystemInfoInfo {
  /**
   * app 字段。
   */
  app: string;
  /**
   * dotnet 字段。
   */
  dotnet: string;
  /**
   * webview 字段。
   */
  webview: string;
  /**
   * os 字段。
   */
  os: string;
  /**
   * root 字段。
   */
  root: string;
  /**
   * data 字段。
   */
  data: string;
}
/**
 * KeyResultInfo：通过 Tauri 命令在前后端之间传输的数据对象。
 */
export interface KeyResultInfo {
  /**
   * role 字段。
   */
  role: string;
  /**
   * ok 字段。
   */
  ok: boolean;
  /**
   * message 字段。
   */
  message: string;
}
/**
 * TestResultInfo：通过 Tauri 命令在前后端之间传输的数据对象。
 */
export interface TestResultInfo {
  /**
   * name 字段。
   */
  name: string;
  /**
   * url 字段。
   */
  url: string;
  /**
   * ok 字段。
   */
  ok: boolean;
  /**
   * message 字段。
   */
  message: string;
  /**
   * ms 字段。
   */
  ms: number;
}
/**
 * UninstallPreviewInfo：通过 Tauri 命令在前后端之间传输的数据对象。
 */
export interface UninstallPreviewInfo {
  /**
   * planId 字段。
   */
  planId: string;
  /**
   * targetId 字段。
   */
  targetId: string;
  /**
   * targetRevision 字段。
   */
  targetRevision: number;
  /**
   * expiresAt 字段。
   */
  expiresAt: string;
  /**
   * name 字段。
   */
  name: string;
  /**
   * notice 字段。
   */
  notice: string;
  /**
   * paths 字段。
   */
  paths: string[];
  /**
   * shared 字段。
   */
  shared: boolean;
}
/**
 * TaskProgressInfo：通过 Tauri 命令在前后端之间传输的数据对象。
 */
export interface TaskProgressInfo {
  /**
   * completed 字段。
   */
  completed: number;
  /**
   * total 字段。
   */
  total: number | null;
  /**
   * unit 字段。
   */
  unit: string;
}
/**
 * TaskResultInfo：通过 Tauri 命令在前后端之间传输的数据对象。
 */
export interface TaskResultInfo {
  /**
   * changed 字段。
   */
  changed: ResourceKeyInfo[];
  /**
   * entityIds 字段。
   */
  entityIds: string[];
}
/**
 * TaskInfo：通过 Tauri 命令在前后端之间传输的数据对象。
 */
export interface TaskInfo {
  /**
   * id 字段。
   */
  id: string;
  /**
   * name 字段。
   */
  name: string;
  /**
   * kind 字段。
   */
  kind: string;
  /**
   * instanceId 字段。
   */
  instanceId: string | null;
  /**
   * status 字段。
   */
  status: string;
  /**
   * phase 字段。
   */
  phase: string;
  /**
   * progress 字段。
   */
  progress: TaskProgressInfo | null;
  /**
   * allowedActions 字段。
   */
  allowedActions: string[];
  /**
   * error 字段。
   */
  error: {
    code: string;
    message: string;
    retryable: boolean;
    fieldErrors?: {
      [k: string]: string;
    };
  } | null;
  /**
   * result 字段。
   */
  result: TaskResultInfo | null;
  /**
   * createdAt 字段。
   */
  createdAt: string;
  /**
   * updatedAt 字段。
   */
  updatedAt: string;
}
/**
 * GameSessionInfo：通过 Tauri 命令在前后端之间传输的数据对象。
 */
export interface GameSessionInfo {
  /**
   * sessionId 字段。
   */
  sessionId: string;
  /**
   * instanceId 字段。
   */
  instanceId: string;
  /**
   * accountId 字段。
   */
  accountId: string;
  /**
   * status 字段。
   */
  status: string;
  /**
   * pid 字段。
   */
  pid: number | null;
  /**
   * exitCode 字段。
   */
  exitCode: number | null;
  /**
   * error 字段。
   */
  error: {} | null;
}
/**
 * LaunchAcceptedInfo：通过 Tauri 命令在前后端之间传输的数据对象。
 */
export interface LaunchAcceptedInfo {
  /**
   * sessionId 字段。
   */
  sessionId: string;
  /**
   * taskId 字段。
   */
  taskId: string;
}
