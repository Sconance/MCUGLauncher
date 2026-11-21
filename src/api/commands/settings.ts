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

/** 按补丁更新启动设置；未提供的字段保持不变。 */
export const saveSettings = (patch: AppSettingsPatch, expectedRevision: number) =>
  call<SavedInfo>("save_settings", { patch, expectedRevision });

/** 扫描本机可用的 Java 运行时。 */
export const scanJava = () => call<JavaInfo[]>("scan_java");

/** 保存自定义数据源地址，可在同一次写入中启用。 */
export const saveCustomSources = (sources: DataSourceSet, expectedRevision: number, activate = false) =>
  call<SavedInfo>("save_custom_sources", { sources, expectedRevision, activate });

/** 切换默认或自定义数据源。 */
export const useDataSources = (mode: "default" | "custom", expectedRevision: number) =>
  call<SavedInfo>("use_data_sources", { mode, expectedRevision });

/** 测试数据源地址是否可访问。 */
export const testDataSource = (name: string, url: string) =>
  call<TestResultInfo>("test_data_source", { name, url });

/** 恢复默认窗口尺寸。 */
export const resetWindow = () => call<SavedInfo>("reset_window");

/** 打开指定目录（root / config / saves / version）。 */
export const openFolder = (kind: string) => call<SavedInfo>("open_folder", { kind });

/** 读取启动器运行日志。 */
export const readLog = () => call<string>("read_log");

/** 读取运行环境信息。 */
export const getSystemInfo = () => call<SystemInfo>("get_system_info");

/** 校验社区身份或管理员密钥。 */
export const verifyKey = (role: string, key: string) => call<KeyResultInfo>("verify_key", { role, key });
