import { call } from "../invoke";
import type { LauncherState, SavedInfo } from "../../types/launcher";

export const getState = () => call<LauncherState>("get_launcher_state");

export const selectInstance = (name: string) => call<SavedInfo>("select_instance", { name });

export const selectVersion = (version: string) => call<SavedInfo>("select_version", { version });
