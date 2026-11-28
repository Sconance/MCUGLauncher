import { useEffect, useState } from "react";
import { openPathInExplorer, pickFile, pickFolder } from "../../api/commands/pathPicker";
import {
  saveSettings as saveSettingsCommand,
  scanJava as scanJavaCommand,
} from "../../api/commands/settings";
import { AppInput, AppSelect } from "../../components/AppInput";
import PathPrompt from "../../components/PathPrompt";
import { LIMITS } from "../../constants";
import { cleanJavaPath, formatJavaLabel, useJavaArchShown } from "../../shell/javaDisplayPreference";
import type { AppSettingsPatch } from "../../types/launcher";
import SettingRow from "./SettingRow";
import SettingsCard from "./SettingsCard";
import { useSettingsData } from "./SettingsData";
import { useSettingsStatus } from "./useSettingsStatus";

interface LaunchSettingsProps {
  active: boolean;
}

/** 启动：游戏目录、内存、窗口与玩家名，以及独立的 Java 路径与本机扫描卡片。 */
export default function LaunchSettings({ active }: LaunchSettingsProps) {
  const { overview, settings, accounts, resourceVersions, refreshSettingsResources } = useSettingsData();
  const { say, statusAt } = useSettingsStatus();
  const showJavaArch = useJavaArchShown();
  const [root, setRoot] = useState("");
  const [java, setJava] = useState("");
  const [ram, setRam] = useState("4096");
  const [width, setWidth] = useState("1280");
  const [height, setHeight] = useState("720");
  const [player, setPlayer] = useState("");
  const [autoMemory, setAutoMemory] = useState(true);
  const [autoChinese, setAutoChinese] = useState(true);
  const [prompt, setPrompt] = useState<null | "root">(null);
  const [hydrated, setHydrated] = useState(false);

  const [javaItems, setJavaItems] = useState<Awaited<ReturnType<typeof scanJavaCommand>>>([]);
  const [javaScanning, setJavaScanning] = useState(false);
  const [javaScanMessage, setJavaScanMessage] = useState("");
  const [javaScannedOnce, setJavaScannedOnce] = useState(false);

  useEffect(() => {
    if (hydrated) return;
    setRoot(settings.root);
    setJava(cleanJavaPath(settings.java));
    setRam(String(settings.ram));
    setWidth(String(settings.width));
    setHeight(String(settings.height));
    setPlayer(accounts.find((account) => account.id === overview.activeAccountId)?.player ?? "");
    setAutoMemory(settings.autoMemory);
    setAutoChinese(settings.autoChinese);
    setHydrated(true);
  }, [hydrated, settings, accounts, overview]);

  const scanJava = async () => {
    setJavaScanning(true);
    setJavaScanMessage("正在扫描本机 Java…");
    try {
      const found = await scanJavaCommand();
      setJavaItems(found);
      setJavaScanMessage(found.length === 0 ? "没有找到可用的 Java。" : `找到 ${found.length} 个 Java。`);
    } catch (error) {
      setJavaScanMessage(error instanceof Error ? error.message : "扫描失败。");
    } finally {
      setJavaScanning(false);
    }
  };

  useEffect(() => {
    if (!active || javaScannedOnce) return;
    setJavaScannedOnce(true);
    void scanJava();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [active, javaScannedOnce]);

  if (!active) return null;

  const roots = Array.from(new Set([...settings.gameRoots, settings.root, root])).filter(
    (value) => value.length > 0,
  );
  const offline = accounts.find((account) => account.id === overview.activeAccountId)?.kind === "offline";

  const persistSettings = async (
    channel: "launch:settings" | "launch:java",
    patch: AppSettingsPatch,
    pendingMessage: string,
  ) => {
    say(channel, pendingMessage);
    try {
      const result = await saveSettingsCommand(patch, resourceVersions.settings?.revision ?? 0);
      await refreshSettingsResources(result.changed);
      say(channel, result.message);
      return true;
    } catch (error) {
      say(channel, error instanceof Error ? error.message : "保存启动设置失败。");
      return false;
    }
  };

  const saveLaunch = async () => {
    if (ram.length === 0 || root.trim().length === 0) {
      say("launch:settings", "请选择已有游戏目录。");
      return;
    }
    const ramValue = Number.parseInt(ram, 10);
    if (!Number.isFinite(ramValue) || ramValue < LIMITS.ramMin || ramValue > LIMITS.ramMax) {
      say("launch:settings", `最大内存需在 ${LIMITS.ramMin}–${LIMITS.ramMax} MB 之间。`);
      return;
    }
    const widthValue = Number.parseInt(width, 10);
    if (!Number.isFinite(widthValue) || widthValue < LIMITS.windowWidthMin || widthValue > LIMITS.windowWidthMax) {
      say("launch:settings", `窗口宽需在 ${LIMITS.windowWidthMin}–${LIMITS.windowWidthMax} 之间。`);
      return;
    }
    const heightValue = Number.parseInt(height, 10);
    if (!Number.isFinite(heightValue) || heightValue < LIMITS.windowHeightMin || heightValue > LIMITS.windowHeightMax) {
      say("launch:settings", `窗口高需在 ${LIMITS.windowHeightMin}–${LIMITS.windowHeightMax} 之间。`);
      return;
    }
    if (offline && player.trim().length === 0) {
      say("launch:settings", "离线账号需要填写玩家名。");
      return;
    }
    await persistSettings(
      "launch:settings",
      {
        root: root.trim(),
        java: java.trim(),
        ram: ramValue,
        width: widthValue,
        height: heightValue,
        autoMemory,
        autoChinese,
        ...(offline ? { player: player.trim() } : {}),
      },
      "正在保存…",
    );
  };

  const saveJava = async () => {
    await persistSettings("launch:java", { java: java.trim() }, "正在保存 Java 路径…");
  };

  const switchRoot = async (value: string) => {
    if (await persistSettings("launch:settings", { root: value }, "正在切换游戏目录…")) setRoot(value);
  };

  const browseRoot = async () => {
    try {
      const picked = await pickFolder({
        title: "选择游戏目录",
        defaultDirectory: root.trim() || undefined,
      });
      if (!picked) return;
      await switchRoot(cleanJavaPath(picked));
    } catch {
      setPrompt("root");
    }
  };

  const openRoot = async () => {
    const message = await openPathInExplorer(root, "root");
    say("launch:settings", message);
  };

  const browseJava = async () => {
    try {
      const picked = await pickFile({ title: "选择 Java 可执行文件（javaw.exe / java.exe）" });
      if (!picked) return;
      setJava(cleanJavaPath(picked));
      say("launch:java", "已选择 Java 路径，保存后生效。");
    } catch (error) {
      say("launch:java", error instanceof Error ? error.message : "选择 Java 路径失败。");
    }
  };

  const openJava = async () => {
    const message = await openPathInExplorer(java);
    say("launch:java", message);
  };

  return (
    <>
      <div className="settings-cards">
        <SettingsCard title="启动">
          <SettingRow
            title="游戏目录"
            control={
              <div className="setting-control">
                <button type="button" className="btn ghost" onClick={() => void browseRoot()}>
                  修改
                </button>
                <button
                  type="button"
                  className="btn ghost"
                  disabled={root.trim().length === 0}
                  onClick={() => void openRoot()}
                >
                  打开
                </button>
              </div>
            }
          >
            <span className="setting-subtitle">{root.trim() || "尚未设置游戏目录"}</span>
          </SettingRow>

          <SettingRow
            title="已保存游戏目录"
            control={
              <div className="setting-control">
                <AppSelect
                  className="setting-control-select"
                  value={root}
                  onChange={(event) => void switchRoot(event.target.value)}
                >
                  {roots.map((value) => (
                    <option key={value} value={value}>
                      {value}
                    </option>
                  ))}
                </AppSelect>
                <button type="button" className="btn ghost" onClick={() => void browseRoot()}>
                  添加
                </button>
              </div>
            }
          >
            <span className="setting-subtitle">快速切换已记录的游戏根目录</span>
          </SettingRow>

          <SettingRow
            title="自动分配内存"
            control={
              <button
                type="button"
                role="switch"
                aria-checked={autoMemory}
                aria-label="自动分配内存"
                className={`switch${autoMemory ? " on" : ""}`}
                onClick={() => setAutoMemory((current) => !current)}
              >
                <span className="switch-thumb" />
              </button>
            }
          >
            <span className="setting-subtitle">根据系统可用内存自动计算分配大小</span>
          </SettingRow>

          <SettingRow
            title="最大内存"
            control={
              <div className="setting-control">
                <AppInput
                  className="setting-control-num"
                  value={ram}
                  onChange={(event) => setRam(event.target.value)}
                />
                <span className="muted">MB</span>
              </div>
            }
          >
            <span className="setting-subtitle">
              允许范围 {LIMITS.ramMin}–{LIMITS.ramMax} MB
            </span>
          </SettingRow>

          <SettingRow
            title="游戏分辨率"
            control={
              <div className="setting-control">
                <AppInput
                  className="setting-control-num"
                  aria-label="游戏窗口宽"
                  value={width}
                  onChange={(event) => setWidth(event.target.value)}
                />
                <span className="setting-control-x">×</span>
                <AppInput
                  className="setting-control-num"
                  aria-label="游戏窗口高"
                  value={height}
                  onChange={(event) => setHeight(event.target.value)}
                />
              </div>
            }
          >
            <span className="setting-subtitle">启动游戏时的默认窗口宽高</span>
          </SettingRow>

          <SettingRow
            title="启动时自动设置简体中文"
            control={
              <button
                type="button"
                role="switch"
                aria-checked={autoChinese}
                aria-label="启动时自动设置简体中文"
                className={`switch${autoChinese ? " on" : ""}`}
                onClick={() => setAutoChinese((current) => !current)}
              >
                <span className="switch-thumb" />
              </button>
            }
          >
            <span className="setting-subtitle">仅在实例缺少语言选项时生效</span>
          </SettingRow>

          <SettingRow
            title={offline ? "离线玩家名" : "玩家名"}
            control={
              <AppInput
                className="setting-control-input"
                placeholder={offline ? "输入离线玩家名" : ""}
                value={player}
                disabled={!offline}
                onChange={(event) => setPlayer(event.target.value)}
              />
            }
          >
            <span className="setting-subtitle">
              {offline ? "仅离线账号可修改" : "在线账号不可修改"}
            </span>
          </SettingRow>

          <div className="settings-actions">
            <button type="button" className="btn primary" onClick={() => void saveLaunch()}>
              保存设置
            </button>
            <span className="status">{statusAt("launch:settings")}</span>
          </div>
        </SettingsCard>

        <SettingsCard title="Java 路径">
          <SettingRow
            title="Java 路径"
            control={
              <div className="setting-control">
                <button type="button" className="btn ghost" onClick={() => void browseJava()}>
                  修改
                </button>
                <button
                  type="button"
                  className="btn ghost"
                  disabled={java.trim().length === 0}
                  onClick={() => void openJava()}
                >
                  打开
                </button>
                {java.trim().length > 0 ? (
                  <button
                    type="button"
                    className="btn ghost"
                    onClick={() => {
                      setJava("");
                      say("launch:java", "已清空 Java 路径，保存后将自动选择。");
                    }}
                  >
                    清空
                  </button>
                ) : null}
              </div>
            }
          >
            <span className="setting-subtitle">
              {java.trim() || "留空时由启动核心自动选择"}
            </span>
          </SettingRow>

          <div className="java-inline-picker">
            <div className="settings-actions">
              <button
                type="button"
                className="btn ghost"
                disabled={javaScanning}
                onClick={() => void scanJava()}
              >
                {javaScanning ? "正在扫描…" : "扫描本机 Java"}
              </button>
              <span className="muted">{javaScanMessage}</span>
            </div>

            <div className="stack java-inline-results">
              <span className="field-label">扫描结果</span>
              {javaItems.length === 0 ? (
                <p className="muted">
                  {javaScanning ? "正在查找可用的 Java…" : "还没有扫描结果。"}
                </p>
              ) : (
                <div className="section-group java-list-group">
                  {javaItems.map((item) => {
                    const displayPath = cleanJavaPath(item.path);
                    const selected = cleanJavaPath(java) === displayPath;
                    return (
                      <button
                        type="button"
                        key={item.path}
                        className={`section-item java-list-item${selected ? " selected" : ""}`}
                        onClick={() => {
                          setJava(displayPath);
                          say("launch:java", "已选择 Java 路径，保存后生效。");
                        }}
                      >
                        <div className="java-list-copy">
                          <span className="java-list-title">{formatJavaLabel(item, showJavaArch)}</span>
                          <span className="muted java-list-path">{displayPath}</span>
                          {item.error.length > 0 ? <span className="badge red">{item.error}</span> : null}
                        </div>
                      </button>
                    );
                  })}
                </div>
              )}
            </div>
          </div>

          <div className="settings-actions">
            <button type="button" className="btn primary" onClick={() => void saveJava()}>
              保存 Java 路径
            </button>
            <span className="status">{statusAt("launch:java")}</span>
          </div>
        </SettingsCard>
      </div>

      {prompt === "root" ? (
        <PathPrompt
          title="选择游戏目录"
          label="游戏根目录"
          initial={root}
          hint="目录必须已存在。"
          onCancel={() => setPrompt(null)}
          onConfirm={(path) => {
            setPrompt(null);
            void switchRoot(cleanJavaPath(path));
          }}
        />
      ) : null}
    </>
  );
}
