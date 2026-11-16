/**
 * 前端契约类型统一从 JSON Schema 生成文件导出。
 * 这里只保留前端专用的泛型快照辅助类型和展示模型。
 */
import type * as Generated from "./generated";

export type ResourceSnapshot<T> = Omit<Generated.ResourceSnapshot, "data"> & { data: T };

export type {
  AccountInfo, AnnouncementInfo, AppSettingsPatch, AvatarInfo, CommunityData,
  ContentChangeInfo, ContentItem, ContentSearchResult, ContentUpdateInfo,
  ContentUpdatePreviewInfo, ContentUpdatesInfo, DataSourceSet, FavoriteInfo,
  GameSessionInfo, InstanceCatalogInfo, InstanceInfo, JavaInfo, KeyResultInfo,
  LauncherOverview,
  LaunchAcceptedInfo, LaunchOptionsInfo, LaunchSettings, LoaderListInfo,
  LoginSessionInfo, ModInfo, MuaProfileInfo, PackPreviewInfo, QuoteInfo,
  RecommendationInfo, ResourceKeyInfo, ResourceVersionInfo, ResourceVersionsInfo,
  SavedInfo, SavedServerInfo, SaveBackupInfo, ServerPingInfo, SettingsInfo,
  TaskInfo, TestResultInfo, UninstallPreviewInfo, VanillaVersionInfo,
  VersionChoice, VersionGroupInfo,
} from "./generated";

export type SystemInfo = Generated.SystemInfoInfo;
