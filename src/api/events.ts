import { listen, type UnlistenFn } from "@tauri-apps/api/event";

/** 后端推送事件。所有全局状态以事件为唯一数据源。 */
export const EVENTS = {
  state: "launcher://state",
  tasks: "launcher://tasks",
  games: "launcher://games",
  community: "launcher://community",
  avatar: "launcher://avatar",
  toast: "launcher://toast",
  error: "launcher://error",
} as const;

export type EventName = (typeof EVENTS)[keyof typeof EVENTS];

export function on<T>(event: EventName, handler: (payload: T) => void): Promise<UnlistenFn> {
  return listen<T>(event, ({ payload }) => handler(payload));
}
