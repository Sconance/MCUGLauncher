import { call } from "../invoke";
import type {
  AppSettingsPatch,
  DataSourceSet,
  JavaInfo,
  KeyResultInfo,
  SavedInfo,
  SystemInfo,
  TestResultInfo,
} from "../../types/launcher";

export const saveSettings = (patch: AppSettingsPatch) => call<SavedInfo>("save_settings", { patch });

export const scanJava = () => call<JavaInfo[]>("scan_java");

export const saveCustomSources = (sources: DataSourceSet) =>
  call<SavedInfo>("save_custom_sources", { sources });

export const useDataSources = (mode: string) => call<SavedInfo>("use_data_sources", { mode });

export const testDataSource = (name: string, url: string) =>
  call<TestResultInfo>("test_data_source", { name, url });

export const resetWindow = () => call<SavedInfo>("reset_window");

export const openFolder = (kind: string) => call<SavedInfo>("open_folder", { kind });

export const readLog = () => call<string>("read_log");

export const getSystemInfo = () => call<SystemInfo>("get_system_info");

export const verifyKey = (role: string, key: string) => call<KeyResultInfo>("verify_key", { role, key });
