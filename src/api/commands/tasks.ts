import { call } from "../invoke";
import type { SavedInfo, TaskInfo } from "../../types/launcher";

export const listTasks = () => call<TaskInfo[]>("list_tasks");

export const taskAction = (id: string, action: string) => call<SavedInfo>("task_action", { id, action });
