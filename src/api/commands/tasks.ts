import { call } from "../invoke";
import type { ResourceSnapshot, SavedInfo, TaskInfo } from "../../types/launcher";

/** 列出任务队列与当前进度。 */
export const listTasks = () => call<ResourceSnapshot<TaskInfo[]>>("list_tasks");

/** 对任务执行暂停/继续/取消/删除。 */
export const taskAction = (id: string, action: string) => call<SavedInfo>("task_action", { id, action });
