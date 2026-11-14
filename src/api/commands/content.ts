import { call } from "../invoke";
import type {
  ContentSearchResult,
  LoaderListResult,
  PackPreviewInfo,
  ResourceVersionsResult,
  SavedInfo,
  VanillaVersionsResult,
} from "../../types/launcher";

export const searchContent = (category: string, query: string) =>
  call<ContentSearchResult>("search_content", { category, query });

export const listContentVersions = (id: string, requestId = "") =>
  call<ResourceVersionsResult>("list_content_versions", { id, requestId });

export const queueContentVersion = (token: string, versionId: string, mode = "instance", path = "") =>
  call<SavedInfo>("queue_content_version", { token, versionId, mode, path });

export const toggleBookmark = (id: string, name: string, description: string, kind: string) =>
  call<SavedInfo>("toggle_bookmark", { id, name, description, kind });

export const listVanillaVersions = () => call<VanillaVersionsResult>("list_vanilla_versions");

export const installVanilla = (version: string) => call<SavedInfo>("install_vanilla", { version });

export const listLoaders = (version: string, loader: string) =>
  call<LoaderListResult>("list_loaders", { version, loader });

export const installLoader = (version: string, loader: string, loaderVersion: string) =>
  call<SavedInfo>("install_loader", { version, loader, loaderVersion });

export const previewPack = (path: string) => call<PackPreviewInfo>("preview_pack", { path });

export const confirmPack = (token: string, name: string, directory: string) =>
  call<SavedInfo>("confirm_pack", { token, name, directory });
