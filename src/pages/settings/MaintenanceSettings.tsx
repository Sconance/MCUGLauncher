import { useEffect, useState } from "react";
import {
  getSystemInfo as getSystemInfoCommand,
  openFolder as openFolderCommand,
  resetWindow as resetWindowCommand,
} from "../../api/commands/settings";
import {
  backupSaves as backupSavesCommand,
  listSaveBackups as listSaveBackupsCommand,
  restoreSave as restoreSaveCommand,
} from "../../api/commands/saves";
import { AppTextarea } from "../../components/AppInput";
import type { SaveBackupInfo } from "../../types/launcher";
import SettingsCard from "./SettingsCard";
import SettingHint from "./SettingHint";
import { useSettingsStatus } from "./useSettingsStatus";

interface MaintenanceSettingsProps {
  active: boolean;
}

/** 维护：存档备份、窗口/目录恢复与系统信息。 */
export default function MaintenanceSettings({ active }: MaintenanceSettingsProps) {
  const { say, statusAt } = useSettingsStatus();
  const [backups, setBackups] = useState<SaveBackupInfo[]>([]);
  const [diagnostics, setDiagnostics] = useState("");

  useEffect(() => {
    if (!active) return;
    void listSaveBackupsCommand().then(setBackups);
    void getSystemInfoCommand().then((info) => {
      setDiagnostics(
        [
          `MCUG Launcher ${info.app}`,
          `.NET ${info.dotnet}`,
          `WebView2 ${info.webview}`,
          `系统 ${info.os}`,
          `游戏目录 ${info.root}`,
          `数据目录 ${info.data}`,
        ].join("\n"),
      );
    });
  }, [active]);

  if (!active) return null;

  const backupSaves = async () => {
    say("maintenance:saves", "正在备份存档…");
    const result = await backupSavesCommand();
    setBackups(await listSaveBackupsCommand());
    say("maintenance:saves", result.message);
  };

  const restoreSave = async (id: string, world: string) => {
    say("maintenance:saves", "正在恢复存档…");
    const result = await restoreSaveCommand(id, world);
    say("maintenance:saves", result.message);
  };

  const copyDiagnostics = async () => {
    if (diagnostics.length === 0) {
      say("maintenance:system", "请先读取系统信息。");
      return;
    }
    try {
      await navigator.clipboard.writeText(diagnostics);
      say("maintenance:system", "诊断信息已复制。");
    } catch {
      say("maintenance:system", "剪贴板不可用。");
    }
  };

  const readSystemInfo = async () => {
    const info = await getSystemInfoCommand();
    setDiagnostics(
      [
        `MCUG Launcher ${info.app}`,
        `.NET ${info.dotnet}`,
        `WebView2 ${info.webview}`,
        `系统 ${info.os}`,
        `游戏目录 ${info.root}`,
        `数据目录 ${info.data}`,
      ].join("\n"),
    );
    say("maintenance:system", "系统信息已更新。");
  };

  return (
    <div className="settings-cards">
      <SettingsCard
        title={
          <>
            存档保护
            <SettingHint tip="备份保存到数据目录的 backups 文件夹" />
          </>
        }
      >
        <div className="settings-actions">
          <button type="button" className="btn primary" onClick={() => void backupSaves()}>
            立即备份
          </button>
          <button
            type="button"
            className="btn ghost"
            onClick={() =>
              void openFolderCommand("saves").then((result) =>
                say("maintenance:saves", result.message),
              )
            }
          >
            打开存档文件夹
          </button>
          <span className="status">{statusAt("maintenance:saves")}</span>
        </div>
        <div className="stack settings-list">
          {backups.length === 0 ? (
            <p className="muted">还没有存档备份。</p>
          ) : (
            backups.map((backup) => (
              <div className="stack settings-list-group" key={backup.id}>
                <span>{`${backup.created} · ${backup.worlds.length} 个存档`}</span>
                {backup.worlds.map((world) => (
                  <button
                    key={world}
                    type="button"
                    className="btn ghost settings-self-start"
                    onClick={() => void restoreSave(backup.id, world)}
                  >
                    恢复为 {world}
                  </button>
                ))}
              </div>
            ))
          )}
        </div>
      </SettingsCard>

      <SettingsCard
        title={
          <>
            任务与恢复
          </>
        }
      >
        <div className="settings-actions">
          <button
            type="button"
            className="btn ghost"
            onClick={() =>
              void resetWindowCommand().then((result) =>
                say("maintenance:recovery", result.message),
              )
            }
          >
            恢复窗口尺寸
          </button>
          <button
            type="button"
            className="btn ghost"
            onClick={() =>
              void openFolderCommand("config").then((result) =>
                say("maintenance:recovery", result.message),
              )
            }
          >
            打开配置目录
          </button>
          <button
            type="button"
            className="btn ghost"
            onClick={() =>
              void openFolderCommand("root").then((result) =>
                say("maintenance:recovery", result.message),
              )
            }
          >
            打开游戏目录
          </button>
        </div>
        <p className="status">{statusAt("maintenance:recovery")}</p>
      </SettingsCard>

      <SettingsCard
        title={
          <>
            系统信息
            <SettingHint tip="反馈问题时请附上这些运行环境信息" />
          </>
        }
      >
        <div className="settings-actions">
          <button type="button" className="btn primary" onClick={() => void readSystemInfo()}>
            读取系统信息
          </button>
          <button type="button" className="btn ghost" onClick={() => void copyDiagnostics()}>
            复制
          </button>
          <span className="status">{statusAt("maintenance:system")}</span>
        </div>
        <AppTextarea className="log-box" readOnly value={diagnostics} />
      </SettingsCard>
    </div>
  );
}
