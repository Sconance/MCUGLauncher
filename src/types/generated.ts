/* eslint-disable */
/**
 * GENERATED FILE — do not edit by hand.
 * Source: schemas/launcher.schema.json
 * Run: npm run dto:generate
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
  LauncherOverview?: LauncherOverview;
  InstanceInfo?: InstanceInfo;
  InstanceCatalogInfo?: InstanceCatalogInfo;
  ModInfo?: ModInfo;
  AccountInfo?: AccountInfo;
  SavedServerInfo?: SavedServerInfo;
  FavoriteInfo?: FavoriteInfo;
  DataSourceSet?: DataSourceSet;
  CommunityData?: CommunityData;
  ContentItem?: ContentItem;
  ContentSearchResult?: ContentSearchResult;
  LaunchSettings?: LaunchSettings;
  AvatarInfo?: AvatarInfo;
  MuaProfileInfo?: MuaProfileInfo;
  LaunchAcceptedInfo?: LaunchAcceptedInfo;
  SettingsInfo?: SettingsInfo;
  TaskInfo?: TaskInfo;
  GameSessionInfo?: GameSessionInfo;
  LoginSessionInfo?: LoginSessionInfo;
  LaunchOptionsInfo?: LaunchOptionsInfo;
  LoaderListInfo?: LoaderListInfo;
  UninstallPreviewInfo?: UninstallPreviewInfo;
}
export interface ApiError {
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
  message: string;
  retryable: boolean;
  fieldErrors?: {
    [k: string]: string;
  };
}
export interface ResourceKeyInfo {
  kind: string;
  instanceId: string | null;
}
export interface SavedInfo {
  message: string;
  changed: ResourceKeyInfo[];
  entityIds: string[];
}
export interface ResourceSnapshot {
  epoch: string;
  revision: number;
  data: unknown;
}
export interface LauncherOverview {
  appVersion: string;
  selectedInstanceId: string | null;
  activeAccountId: string | null;
}
export interface InstanceInfo {
  id: string;
  name: string;
  displayName: string;
  loader: string;
  minecraft: string;
  mods?: number;
  updates?: number;
  isPlaceholder?: boolean;
}
export interface InstanceCatalogInfo {
  items: InstanceInfo[];
  versionGroups: unknown[];
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
export interface AccountInfo {
  id: string;
  kind: "microsoft" | "mua" | "offline";
  player: string;
  status: "ready" | "expired" | "invalid";
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
export interface DataSourceSet {
  announcements: string;
  servers: string;
  recommendations: string;
  quotes: string;
}
export interface CommunityData {
  announcements: unknown[];
  recommendations: unknown[];
  quotes: unknown[];
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
export interface LaunchAcceptedInfo {
  sessionId: string;
  taskId: string;
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
  hasCommunityKey?: boolean;
  hasAdminKey?: boolean;
  skinStation?: string;
  cape?: string;
  dataSources: {};
  defaultDataSources?: {};
  customDataSources?: {};
  dataSourceMode: string;
  autoChinese: boolean;
}
export interface TaskInfo {
  id: string;
  name?: string;
  kind: string;
  instanceId: string | null;
  status: "queued" | "running" | "paused" | "cancelling" | "succeeded" | "failed" | "cancelled";
  phase: string;
  progress?: {} | null;
  allowedActions: string[];
  error?: ApiError | null;
  result?: {} | null;
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
  error: ApiError | null;
}
export interface LaunchOptionsInfo {
  requestId: string;
  token: string;
  instanceId: string;
  displayName: string;
  inherited: boolean;
  settings: {};
}
export interface LoaderListInfo {
  minecraftVersion: string;
  loader: string;
  versions: string[];
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
