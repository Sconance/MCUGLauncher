import { call } from "../invoke";
import type { SaveBackupInfo, SavedInfo } from "../../types/launcher";

/** 列出本机存档备份。 */
export const listSaveBackups = () => call<SaveBackupInfo[]>("list_save_backups");

/** 立即备份当前实例存档。 */
export const backupSaves = () => call<SavedInfo>("backup_saves");

/** 从指定备份恢复某个世界。 */
export const restoreSave = (id: string, world: string) => call<SavedInfo>("restore_save", { id, world });
