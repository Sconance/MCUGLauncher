import { useEffect, useState } from "react";

/** 开发用：分别强制显示右上角的运行实例 / 下载任务按钮。 */
export type TopStatusTarget = "games" | "tasks";

const KEYS: Record<TopStatusTarget, string> = {
  games: "mcugl.topStatusForceGames",
  tasks: "mcugl.topStatusForceTasks",
};

/** 旧版是一个总开关；首次加载时迁移成两个独立开关。 */
const LEGACY_KEY = "mcugl.topStatusForce";

function migrateLegacyKey(): void {
  if (localStorage.getItem(LEGACY_KEY) !== "1") return;
  if (localStorage.getItem(KEYS.games) === null) localStorage.setItem(KEYS.games, "1");
  if (localStorage.getItem(KEYS.tasks) === null) localStorage.setItem(KEYS.tasks, "1");
  localStorage.removeItem(LEGACY_KEY);
}

migrateLegacyKey();

const listeners = new Set<() => void>();

export function isTopStatusForced(target: TopStatusTarget): boolean {
  return localStorage.getItem(KEYS[target]) === "1";
}

export function setTopStatusForced(target: TopStatusTarget, value: boolean): void {
  if (value) localStorage.setItem(KEYS[target], "1");
  else localStorage.removeItem(KEYS[target]);
  listeners.forEach((listener) => listener());
}

/** 订阅某个按钮的强制显示开关；设置页改动后，右上角按钮同帧跟进。 */
export function useTopStatusForced(target: TopStatusTarget): boolean {
  const [value, setValue] = useState(() => isTopStatusForced(target));

  useEffect(() => {
    const listener = () => setValue(isTopStatusForced(target));
    listeners.add(listener);
    return () => {
      listeners.delete(listener);
    };
  }, [target]);

  return value;
}
