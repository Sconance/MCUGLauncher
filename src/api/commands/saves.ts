import { call } from "../invoke";
import type { SaveBackupInfo, SavedInfo } from "../../types/launcher";

export const listSaveBackups = () => call<SaveBackupInfo[]>("list_save_backups");

export const backupSaves = () => call<SavedInfo>("backup_saves");

export const restoreSave = (id: string, world: string) => call<SavedInfo>("restore_save", { id, world });
