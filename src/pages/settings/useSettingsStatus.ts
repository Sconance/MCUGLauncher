import { useCallback, useState } from "react";

/**
 * 设置分区里的轻量状态行。
 *
 * 一个分区可能有多张卡片（存档、恢复、系统信息），用 key 区分各卡片的提示，
 * 避免一张卡片的操作结果覆盖另一张卡片的状态。
 */
export function useSettingsStatus() {
  const [statuses, setStatuses] = useState<Record<string, string>>({});

  const say = useCallback((key: string, message: string) => {
    setStatuses((previous) => ({ ...previous, [key]: message }));
  }, []);

  const statusAt = useCallback((key: string) => statuses[key] ?? "", [statuses]);

  return { say, statusAt };
}
