import { useEffect, useState } from "react";
import {
  installFrontend,
  listFrontends,
  readFrontendIcon,
  reloadFrontend,
  removeFrontend,
  useFrontend,
  type FrontendSummary,
} from "../../api/commands/frontend";
import { pickFile } from "../../api/commands/pathPicker";
import { openFolder as openFolderCommand, readLog as readLogCommand } from "../../api/commands/settings";
import { AppTextarea } from "../../components/AppInput";
import Modal from "../../components/Modal";
import SvgIcon from "../../components/SvgIcon";
import cubeIcon from "../../assets/icons/ui/cube.svg";
import { setTopStatusForced, useTopStatusForced } from "../../shell/topStatusPreference";
import SettingRow from "./SettingRow";
import SettingsCard from "./SettingsCard";
import { useSettingsData } from "./SettingsData";
import { useSettingsStatus } from "./useSettingsStatus";

interface DeveloperSettingsProps {
  active: boolean;
}

type PendingAction =
  | { kind: "install"; path: string }
  | { kind: "remove"; item: FrontendSummary }
  | null;

function fileName(path: string): string {
  const parts = path.split(/[\\/]/);
  return parts[parts.length - 1] || path;
}

/** 开发者选项：状态栏按钮、第三方前端与运行日志。 */
export default function DeveloperSettings({ active }: DeveloperSettingsProps) {
  const forceGames = useTopStatusForced("games");
  const forceTasks = useTopStatusForced("tasks");
  const { say, statusAt } = useSettingsStatus();
  const { resourceVersions, refreshSettingsResources } = useSettingsData();
  const [log, setLog] = useState("");
  const [frontends, setFrontends] = useState<FrontendSummary[] | null>(null);
  const [icons, setIcons] = useState<Record<string, string | null>>({});
  const [activeFrontend, setActiveFrontend] = useState<string | null>(null);
  const [pending, setPending] = useState<PendingAction>(null);
  const [busy, setBusy] = useState(false);

  const revision = resourceVersions.settings?.revision ?? 0;

  const readLog = async () => {
    setLog(await readLogCommand());
    say("developer:logs", "");
  };

  const refreshFrontends = async () => {
    try {
      const list = await listFrontends();
      setFrontends(list.items);
      setActiveFrontend(list.active);
    } catch (error) {
      setFrontends([]);
      say("developer:frontends", error instanceof Error ? error.message : "读取已安装前端失败。");
    }
  };

  useEffect(() => {
    if (!active) return;
    void readLogCommand().then(setLog);
    void refreshFrontends();
  }, [active]);

  // 图标单独取：列表只负责“有没有”，真正显示时再按需读成 data URL。
  useEffect(() => {
    const wanted = (frontends ?? []).filter((item) => item.hasIcon);
    if (wanted.length === 0) {
      setIcons({});
      return;
    }
    let cancelled = false;
    void Promise.all(
      wanted.map(async (item) => {
        const key = `${item.identifier}@${item.version}`;
        try {
          return [key, await readFrontendIcon(item.identifier, item.version)] as const;
        } catch {
          return [key, null] as const;
        }
      }),
    ).then((entries) => {
      if (!cancelled) setIcons(Object.fromEntries(entries));
    });
    return () => {
      cancelled = true;
    };
  }, [frontends]);

  if (!active) return null;

  const importFrontend = async () => {
    const path = await pickFile({ title: "选择前端包（.mcuglf）" });
    if (path) setPending({ kind: "install", path });
  };

  const switchFrontend = async (identifier: string | null) => {
    setBusy(true);
    say("developer:frontends", "正在切换…");
    try {
      const result = await useFrontend(identifier, revision);
      await refreshSettingsResources(result.changed);
      await refreshFrontends();
      say("developer:frontends", result.message);
    } catch (error) {
      say("developer:frontends", error instanceof Error ? error.message : "切换前端失败。");
    } finally {
      setBusy(false);
    }
  };

  const reload = async () => {
    setBusy(true);
    say("developer:frontends", "正在重启前端…");
    try {
      const result = await reloadFrontend();
      // 正常情况下这个 Promise 还没走完，窗口就已经被新窗口替换了。
      say("developer:frontends", result.message);
    } catch (error) {
      say("developer:frontends", error instanceof Error ? error.message : "重启前端失败。");
    } finally {
      setBusy(false);
    }
  };

  const removeFrontendVersion = async (item: FrontendSummary) => {
    setBusy(true);
    say("developer:frontends", "正在卸载…");
    try {
      const result = await removeFrontend(item.identifier, item.version, revision);
      await refreshSettingsResources(result.changed);
      await refreshFrontends();
      say("developer:frontends", result.message);
    } catch (error) {
      say("developer:frontends", error instanceof Error ? error.message : "卸载前端失败。");
    } finally {
      setBusy(false);
    }
  };

  const confirmPending = async () => {
    if (!pending) return;
    const action = pending;
    setPending(null);
    if (action.kind === "install") {
      setBusy(true);
      say("developer:frontends", "正在导入…");
      try {
        const item = await installFrontend(action.path);
        await refreshFrontends();
        say(
          "developer:frontends",
          `已导入 ${item.name} ${item.version}。点「使用」选中，再点「重启前端」生效。`,
        );
      } catch (error) {
        say("developer:frontends", error instanceof Error ? error.message : "导入前端失败。");
      } finally {
        setBusy(false);
      }
      return;
    }
    await removeFrontendVersion(action.item);
  };

  const frontendStatus = statusAt("developer:frontends");

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
            前端（第三方 UI）
          </>
        }
      >
        <span className="muted">
          导入 `.mcuglf` 包即可用别人的界面替换本启动器界面。第三方前端通过
          mcugl.kernel/v1 访问与官方界面相同的命令，因此拥有完整的数据读写权限，只导入你信任的文件。
          切换后点「重启前端」立即生效；如果第三方界面白屏或卡死，按 Ctrl+Alt+C
          可强制切回官方前端，启动器不会退出。
        </span>
        <div className="settings-actions">
          <button
            type="button"
            className="btn primary"
            disabled={busy}
            onClick={() => void importFrontend()}
          >
            导入前端包…
          </button>
          {activeFrontend ? (
            <button
              type="button"
              className="btn ghost"
              disabled={busy}
              onClick={() => void switchFrontend(null)}
            >
              切回官方前端
            </button>
          ) : (
            <span className="status">当前使用：官方前端</span>
          )}
          <button
            type="button"
            className="btn ghost"
            disabled={busy}
            onClick={() => void reload()}
          >
            重启前端
          </button>
          <span className="status">{frontendStatus}</span>
        </div>

        {frontends === null ? (
          <span className="muted">正在读取已安装前端…</span>
        ) : frontends.length === 0 ? (
          <span className="muted">
            还没有安装第三方前端。可以用 npm run frontend:pack 把示例目录打成 `.mcuglf` 再导入。
          </span>
        ) : (
          frontends.map((item) => {
            const key = `${item.identifier}@${item.version}`;
            const icon = icons[key];
            return (
            <div className="setting-row" key={key}>
              <div className="frontend-info">
                {icon ? (
                  <img className="frontend-icon" src={icon} alt={`${item.name} 图标`} />
                ) : (
                  <span className="frontend-icon fallback" aria-hidden="true">
                    <SvgIcon src={cubeIcon} className="frontend-icon-glyph" />
                  </span>
                )}
                <div className="setting-copy">
                  <span className="setting-title">
                    {item.name} <span className="muted">{item.version}</span>
                    {item.active ? <span className="status"> · 使用中</span> : null}
                  </span>
                  <span className="muted">
                    {item.identifier}
                    {item.author ? ` · ${item.author}` : ""} · 内核 {item.kernelApi}
                  </span>
                  {item.description ? <span className="muted">{item.description}</span> : null}
                </div>
              </div>
              <div className="settings-actions">
                {item.active ? null : (
                  <button
                    type="button"
                    className="btn ghost"
                    disabled={busy}
                    onClick={() => void switchFrontend(item.identifier)}
                  >
                    使用
                  </button>
                )}
                <button
                  type="button"
                  className="btn ghost"
                  disabled={busy}
                  onClick={() => setPending({ kind: "remove", item })}
                >
                  卸载
                </button>
              </div>
            </div>
            );
          })
        )}
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

      {pending ? (
        <Modal
          title={pending.kind === "install" ? "导入第三方前端" : "卸载前端"}
          onClose={() => setPending(null)}
          footer={(close) => (
            <>
              <button
                type="button"
                className="btn ghost"
                onClick={() => close()}
              >
                取消
              </button>
              <button
                type="button"
                className="btn primary"
                onClick={() => close(() => void confirmPending())}
              >
                {pending.kind === "install" ? "确认导入" : "确认卸载"}
              </button>
            </>
          )}
        >
          {pending.kind === "install" ? (
            <div className="stack">
              <span>
                即将导入 <strong>{fileName(pending.path)}</strong>。
              </span>
              <span className="muted">
                第三方前端会获得与官方界面相同的权限（读取实例、账号、设置，调用内核命令）。只导入你信任来源的文件。
              </span>
            </div>
          ) : (
            <div className="stack">
              <span>
                卸载 <strong>{pending.item.name}</strong> {pending.item.version}？
              </span>
              <span className="muted">
                如果这是当前使用的前端，卸载后会切回官方前端；改动在重启后完全生效。
              </span>
            </div>
          )}
        </Modal>
      ) : null}
    </div>
  );
}
