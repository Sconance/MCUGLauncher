import { call } from "../invoke";
import type {
  ContentSearchResult,
  LoaderListResult,
  PackPreviewInfo,
  ResourceVersionsResult,
  SavedInfo,
  VanillaVersionsResult,
} from "../../types/launcher";

/** 按分类与关键词搜索资源项目。 */
export const searchContent = (category: string, query: string) =>
  call<ContentSearchResult>("search_content", { category, query });

/** 读取项目的兼容版本与安装令牌；`requestId` 用于丢弃过期响应。 */
export const listContentVersions = (id: string, requestId = "") =>
  call<ResourceVersionsResult>("list_content_versions", { id, requestId });

/** 将资源版本加入下载队列；`mode` 为 instance（安装到实例）或 local（下载到目录）。 */
export const queueContentVersion = (token: string, versionId: string, mode = "instance", path = "") =>
  call<SavedInfo>("queue_content_version", { token, versionId, mode, path });

/** 收藏或取消收藏资源项目。 */
export const toggleBookmark = (id: string, name: string, description: string, kind: string) =>
  call<SavedInfo>("toggle_bookmark", { id, name, description, kind });

/** 读取 Mojang 原版版本清单。 */
export const listVanillaVersions = () => call<VanillaVersionsResult>("list_vanilla_versions");

/** 安装指定原版版本。 */
export const installVanilla = (version: string) => call<SavedInfo>("install_vanilla", { version });

/** 读取指定游戏版本可用的加载器版本。 */
export const listLoaders = (version: string, loader: string) =>
  call<LoaderListResult>("list_loaders", { version, loader });

/** 安装指定加载器版本。 */
export const installLoader = (version: string, loader: string, loaderVersion: string) =>
  call<SavedInfo>("install_loader", { version, loader, loaderVersion });

/** 读取整合包预览信息（名称、游戏版本、加载器）。 */
export const previewPack = (path: string) => call<PackPreviewInfo>("preview_pack", { path });

/** 确认导入整合包到指定目录。 */
export const confirmPack = (planId: string, name: string, directory: string) =>
  call<SavedInfo>("confirm_pack", { planId, name, directory });
