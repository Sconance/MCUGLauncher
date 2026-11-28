import { useEffect, useState } from "react";
import { openFolder as openFolderCommand, readLog as readLogCommand } from "../../api/commands/settings";
import { AppTextarea } from "../../components/AppInput";
import { setTopStatusForced, useTopStatusForced } from "../../shell/topStatusPreference";
import {
  setVanillaReleaseTimeShown,
  useVanillaReleaseTimeShown,
} from "../../shell/releaseTimePreference";
import { setJavaArchShown, useJavaArchShown } from "../../shell/javaDisplayPreference";
import SettingRow from "./SettingRow";
import SettingsCard from "./SettingsCard";
import { useSettingsStatus } from "./useSettingsStatus";

interface DeveloperSettingsProps {
  active: boolean;
}

/** 开发者选项：状态栏按钮、调试显示偏好与运行日志。 */
export default function DeveloperSettings({ active }: DeveloperSettingsProps) {
  const forceGames = useTopStatusForced("games");
  const forceTasks = useTopStatusForced("tasks");
  const showReleaseTime = useVanillaReleaseTimeShown();
  const showJavaArch = useJavaArchShown();
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
        <SettingRow
          title={
            <>
              原版列表显示发布时分秒
            </>
          }
          control={
            <button
              type="button"
              role="switch"
              aria-checked={showReleaseTime}
              aria-label="原版列表显示发布时分秒"
              className={`switch${showReleaseTime ? " on" : ""}`}
              onClick={() => setVanillaReleaseTimeShown(!showReleaseTime)}
            >
              <span className="switch-thumb" />
            </button>
          }
        />
        <SettingRow
          title={
            <>
              显示本机Java架构
            </>
          }
          control={
            <button
              type="button"
              role="switch"
              aria-checked={showJavaArch}
              aria-label="显示本机Java架构"
              className={`switch${showJavaArch ? " on" : ""}`}
              onClick={() => setJavaArchShown(!showJavaArch)}
            >
              <span className="switch-thumb" />
            </button>
          }
        />
      </SettingsCard>

      <SettingsCard title="运行日志">
        <SettingRow
          title="启动器日志"
          control={
            <div className="setting-control">
              <button type="button" className="btn primary" onClick={() => void readLog()}>
                刷新
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
                打开
              </button>
            </div>
          }
        >
          <span className="setting-subtitle">
            {statusAt("developer:logs") || "读取最新运行日志或打开日志所在目录"}
          </span>
        </SettingRow>
        <AppTextarea className="log-box" readOnly value={log} />
      </SettingsCard>
    </div>
  );
}
