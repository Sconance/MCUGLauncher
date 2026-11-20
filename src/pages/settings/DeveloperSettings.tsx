import { useEffect, useState } from "react";
import { openFolder as openFolderCommand, readLog as readLogCommand } from "../../api/commands/settings";
import { AppTextarea } from "../../components/AppInput";
import { setTopStatusForced, useTopStatusForced } from "../../shell/topStatusPreference";
import SettingRow from "./SettingRow";
import SettingsCard from "./SettingsCard";
import { useSettingsStatus } from "./useSettingsStatus";

interface DeveloperSettingsProps {
  active: boolean;
}

/** 开发者选项：强制显示开关与运行日志。 */
export default function DeveloperSettings({ active }: DeveloperSettingsProps) {
  const forceGames = useTopStatusForced("games");
  const forceTasks = useTopStatusForced("tasks");
  const { say, statusAt } = useSettingsStatus();
  const [log, setLog] = useState("");

  const readLog = async () => {
    setLog(await readLogCommand());
    say("developer:logs", "");
  };

  useEffect(() => {
    if (!active) return;
    void readLogCommand().then(setLog);
  }, [active]);

  if (!active) return null;

  return (
    <div className="settings-cards">
      <SettingsCard
        title={
          <>
            开发者选项
          </>
        }
      >
        <SettingRow
          title={
            <>
              强制显示运行实例按钮
            </>
          }
          control={
            <button
              type="button"
              role="switch"
              aria-checked={forceGames}
              aria-label="强制显示运行实例按钮"
              className={`switch${forceGames ? " on" : ""}`}
              onClick={() => setTopStatusForced("games", !forceGames)}
            >
              <span className="switch-thumb" />
            </button>
          }
        />
        <SettingRow
          title={
            <>
              强制显示下载任务按钮
            </>
          }
          control={
            <button
              type="button"
              role="switch"
              aria-checked={forceTasks}
              aria-label="强制显示下载任务按钮"
              className={`switch${forceTasks ? " on" : ""}`}
              onClick={() => setTopStatusForced("tasks", !forceTasks)}
            >
              <span className="switch-thumb" />
            </button>
          }
        />
      </SettingsCard>

      <SettingsCard
        title={
          <>
            运行日志
          </>
        }
      >
        <div className="settings-actions">
          <button type="button" className="btn primary" onClick={() => void readLog()}>
            刷新日志
          </button>
          <button
            type="button"
            className="btn ghost"
            onClick={() =>
              void openFolderCommand("config").then((result) =>
                say("developer:logs", result.message),
              )
            }
          >
            打开日志目录
          </button>
          <span className="status">{statusAt("developer:logs")}</span>
        </div>
        <AppTextarea className="log-box" readOnly value={log} />
      </SettingsCard>
    </div>
  );
}
