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
import Modal from "../../components/Modal";
import SvgIcon from "../../components/SvgIcon";
import cubeIcon from "../../assets/icons/ui/cube.svg";
import SettingRow from "./SettingRow";
import SettingsCard from "./SettingsCard";
import { useSettingsData } from "./SettingsData";
import { useSettingsStatus } from "./useSettingsStatus";

interface ExtensionsSettingsProps {
  active: boolean;
}

type PendingAction =
  | { kind: "install"; path: string }
  | { kind: "remove"; item: FrontendSummary }
  | null;

interface OfficialFrontendManifest {
  identifier: string;
  name: string;
  version: string;
  kernelApi: string;
  author?: string;
  description?: string;
  icon?: string;
}

const DEFAULT_OFFICIAL_MANIFEST: OfficialFrontendManifest = {
  identifier: "cn.mcug.official",
  name: "MCUG Launcher 官方界面",
  version: "0.1.0",
  kernelApi: "^1.0.0",
  author: "MCUG Launcher",
  description: "MCUG Launcher 默认官方前端包（内置）",
  icon: "MCUGLogo.svg",
};

function fileName(path: string): string {
  const parts = path.split(/[\\/]/);
  return parts[parts.length - 1] || path;
}

/** 扩展：第三方前端包（UI）管理与切换。 */
export default function ExtensionsSettings({ active }: ExtensionsSettingsProps) {
  const { say, statusAt } = useSettingsStatus();
  const { resourceVersions, refreshSettingsResources } = useSettingsData();
  const [official, setOfficial] = useState<OfficialFrontendManifest>(DEFAULT_OFFICIAL_MANIFEST);
  const [frontends, setFrontends] = useState<FrontendSummary[] | null>(null);
  const [icons, setIcons] = useState<Record<string, string | null>>({});
  const [activeFrontend, setActiveFrontend] = useState<string | null>(null);
  const [pending, setPending] = useState<PendingAction>(null);
  const [busy, setBusy] = useState(false);

  const revision = resourceVersions.settings?.revision ?? 0;

  const refreshFrontends = async () => {
    try {
      const list = await listFrontends();
      setFrontends(list.items);
      setActiveFrontend(list.active);
    } catch (error) {
      setFrontends([]);
      say("extensions:frontends", error instanceof Error ? error.message : "读取已安装前端包失败。");
    }
  };

  useEffect(() => {
    if (!active) return;
    void refreshFrontends();
    let cancelled = false;
    void fetch("./frontend.json")
      .then((res) => (res.ok ? res.json() : null))
      .then((data: Partial<OfficialFrontendManifest> | null) => {
        if (!cancelled && data && typeof data.name === "string" && typeof data.identifier === "string") {
          setOfficial({
            identifier: data.identifier,
            name: data.name,
            version: typeof data.version === "string" ? data.version : DEFAULT_OFFICIAL_MANIFEST.version,
            kernelApi: typeof data.kernelApi === "string" ? data.kernelApi : DEFAULT_OFFICIAL_MANIFEST.kernelApi,
            author: typeof data.author === "string" ? data.author : undefined,
            description: typeof data.description === "string" ? data.description : undefined,
            icon: typeof data.icon === "string" ? data.icon : undefined,
          });
        }
      })
      .catch(() => {
        /* 保持内置默认值 */
      });
    return () => {
      cancelled = true;
    };
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
    say("extensions:frontends", "正在切换…");
    try {
      const result = await useFrontend(identifier, revision);
      await refreshSettingsResources(result.changed);
      await refreshFrontends();
      say("extensions:frontends", result.message);
    } catch (error) {
      say("extensions:frontends", error instanceof Error ? error.message : "切换前端包失败。");
    } finally {
      setBusy(false);
    }
  };

  const reload = async () => {
    setBusy(true);
    say("extensions:frontends", "正在重启前端包…");
    try {
      const result = await reloadFrontend();
      // 正常情况下这个 Promise 还没走完，窗口就已经被新窗口替换了。
      say("extensions:frontends", result.message);
    } catch (error) {
      say("extensions:frontends", error instanceof Error ? error.message : "重启前端包失败。");
    } finally {
      setBusy(false);
    }
  };

  const removeFrontendVersion = async (item: FrontendSummary) => {
    setBusy(true);
    say("extensions:frontends", "正在卸载…");
    try {
      const result = await removeFrontend(item.identifier, item.version, revision);
      await refreshSettingsResources(result.changed);
      await refreshFrontends();
      say("extensions:frontends", result.message);
    } catch (error) {
      say("extensions:frontends", error instanceof Error ? error.message : "卸载前端包失败。");
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
      say("extensions:frontends", "正在导入…");
      try {
        const item = await installFrontend(action.path);
        await refreshFrontends();
        say(
          "extensions:frontends",
          `已导入 ${item.name} ${item.version}。点「使用」选中，再点「重启前端包」生效。`,
        );
      } catch (error) {
        say("extensions:frontends", error instanceof Error ? error.message : "导入前端包失败。");
      } finally {
        setBusy(false);
      }
      return;
    }
    await removeFrontendVersion(action.item);
  };

  const frontendStatus = statusAt("extensions:frontends");

  return (
    <div className="settings-cards">
      <SettingsCard title="前端包（第三方 UI）">
        <SettingRow
          title="第三方前端包管理"
          control={
            <div className="setting-control">
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
                  切回官方前端包
                </button>
              ) : null}
              <button
                type="button"
                className="btn ghost"
                disabled={busy}
                onClick={() => void reload()}
              >
                重启前端包
              </button>
            </div>
          }
        >
          <span className="setting-subtitle">
            {frontendStatus ||
              (activeFrontend
                ? `当前使用：${activeFrontend}（按 Ctrl+Alt+C 可强制切回官方前端包）`
                : "当前使用：官方前端包；导入 .mcuglf 包可替换界面（Ctrl+Alt+C 强制切回）")}
          </span>
        </SettingRow>

        <div className="setting-row" key="__official__">
          <div className="frontend-info">
            {official.icon ? (
              <img className="frontend-icon" src={`./${official.icon}`} alt={`${official.name} 图标`} />
            ) : (
              <span className="frontend-icon fallback" aria-hidden="true">
                <SvgIcon src={cubeIcon} className="frontend-icon-glyph" />
              </span>
            )}
            <div className="setting-copy">
              <span className="setting-title">
                {official.name} <span className="muted">{official.version}</span>
                {activeFrontend === null ? <span className="status"> · 使用中</span> : null}
              </span>
              <span className="muted">
                {official.identifier}
                {official.author ? ` · ${official.author}` : ""} · 内核 {official.kernelApi}
              </span>
              {official.description ? <span className="muted">{official.description}</span> : null}
            </div>
          </div>
          <div className="settings-actions">
            {activeFrontend === null ? null : (
              <button
                type="button"
                className="btn ghost"
                disabled={busy}
                onClick={() => void switchFrontend(null)}
              >
                使用
              </button>
            )}
            <button
              type="button"
              className="btn ghost"
              disabled
              title="官方内置前端包不可卸载"
            >
              卸载
            </button>
          </div>
        </div>

        {frontends === null ? (
          <span className="muted">正在读取已安装前端包…</span>
        ) : frontends.length === 0 ? (
          <span className="muted">
            还没有安装第三方前端包。可以用 npm run frontend:pack 把示例目录打成 `.mcuglf` 再导入。
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

      {pending ? (
        <Modal
          title={pending.kind === "install" ? "导入第三方前端包" : "卸载前端包"}
          onClose={() => setPending(null)}
          footer={(close) => (
            <>
              <button type="button" className="btn ghost" onClick={() => close()}>
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
                第三方前端包会获得与官方界面相同的权限（读取实例、账号、设置，调用内核命令）。只导入你信任来源的文件。
              </span>
            </div>
          ) : (
            <div className="stack">
              <span>
                卸载 <strong>{pending.item.name}</strong> {pending.item.version}？
              </span>
              <span className="muted">
                如果这是当前使用的前端包，卸载后会切回官方前端包；改动在重启后完全生效。
              </span>
            </div>
          )}
        </Modal>
      ) : null}
    </div>
  );
}
