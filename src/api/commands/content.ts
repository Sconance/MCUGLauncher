import { call } from "../invoke";
import type {
  ContentSearchResult,
  LoaderListInfo,
  PackPreviewInfo,
  ResourceVersionsInfo,
  SavedInfo,
  VanillaVersionInfo,
} from "../../types/launcher";

/** 按分类与关键词搜索资源项目；`game` / `loader` 用于按实例的版本与加载器过滤，留空表示不筛。 */
export const searchContent = (category: string, query: string, game = "", loader = "") =>
  call<ContentSearchResult>("search_content", { category, query, game, loader });

/** 读取项目的兼容版本与安装令牌；`requestId` 用于丢弃过期响应。 */
export const listContentVersions = (id: string, requestId = "") =>
  call<ResourceVersionsInfo>("list_content_versions", { id, requestId });

/** 将资源版本加入下载队列；`mode` 为 instance（安装到实例）或 local（下载到目录）。 */
export const queueContentVersion = (token: string, versionId: string, mode = "instance", path = "") =>
  call<SavedInfo>("queue_content_version", { token, versionId, mode, path });

/** 收藏或取消收藏资源项目。 */
export const toggleBookmark = (id: string, name: string, description: string, kind: string) =>
  call<SavedInfo>("toggle_bookmark", { id, name, description, kind });

/** 读取 Mojang 原版版本清单。 */
export const listVanillaVersions = () => call<VanillaVersionInfo[]>("list_vanilla_versions");

/** 安装指定原版版本。 */
export const installVanilla = (minecraftVersion: string) =>
  call<SavedInfo>("install_vanilla", { minecraftVersion });

/** 读取指定游戏版本可用的加载器版本。 */
export const listLoaders = (minecraftVersion: string, loader: string) =>
  call<LoaderListInfo>("list_loaders", { minecraftVersion, loader });

/** 安装指定加载器版本。 */
export const installLoader = (minecraftVersion: string, loader: string, loaderVersion: string) =>
  call<SavedInfo>("install_loader", { minecraftVersion, loader, loaderVersion });

/** 读取整合包预览信息（名称、游戏版本、加载器）。 */
export const previewPack = (path: string) => call<PackPreviewInfo>("preview_pack", { path });

/** 确认导入整合包到指定目录。 */
export const confirmPack = (planId: string, name: string, directory: string) =>
  call<SavedInfo>("confirm_pack", { planId, name, directory });
