import { call } from "../invoke";
import type {
  ContentUpdatePreviewInfo,
  ContentUpdatesInfo,
  LaunchOptionsInfo,
  LaunchSettings,
  SavedInfo,
  UninstallPreviewInfo,
} from "../../types/launcher";

export const previewUninstall = (version: string) =>
  call<UninstallPreviewInfo>("preview_uninstall", { version });

export const applyUninstall = (token: string) => call<SavedInfo>("apply_uninstall", { token });

export const cloneInstance = (version: string, name: string, saves = false, servers = false) =>
  call<SavedInfo>("clone_instance", { version, name, saves, servers });

export const exportInstance = (version: string, path: string, servers = false, saves = false) =>
  call<SavedInfo>("export_instance", { version, path, servers, saves });

export const checkUpdates = (version: string) => call<ContentUpdatesInfo>("check_updates", { version });

export const previewUpdates = (token: string, projects: string[] = []) =>
  call<ContentUpdatePreviewInfo>("preview_updates", { token, projects });

export const applyUpdates = (token: string) => call<SavedInfo>("apply_updates", { token });

export const toggleMod = (file: string, enabled: boolean) =>
  call<SavedInfo>("toggle_mod", { file, enabled });

export const deleteMod = (file: string) => call<SavedInfo>("delete_mod", { file });

export const readLaunchOptions = (version: string, requestId = "") =>
  call<LaunchOptionsInfo>("read_launch_options", { version, requestId });

export const saveLaunchOptions = (
  token: string,
  version: string,
  name: string,
  settings: LaunchSettings,
  inherit: boolean,
) => call<SavedInfo>("save_launch_options", { token, version, name, settings, inherit });
