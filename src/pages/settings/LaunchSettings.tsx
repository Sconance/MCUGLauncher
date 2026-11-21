import { useEffect, useState } from "react";
import { saveSettings as saveSettingsCommand } from "../../api/commands/settings";
import { AppInput, AppSelect } from "../../components/AppInput";
import Field from "../../components/Field";
import JavaPrompt from "../../components/JavaPrompt";
import PathPrompt from "../../components/PathPrompt";
import { LIMITS } from "../../constants";
import type { AppSettingsPatch } from "../../types/launcher";
import SettingsCard from "./SettingsCard";
import { useSettingsData } from "./SettingsData";
import { useSettingsStatus } from "./useSettingsStatus";

interface LaunchSettingsProps {
  active: boolean;
}

/** 启动：游戏目录、Java、内存、窗口与玩家名。 */
export default function LaunchSettings({ active }: LaunchSettingsProps) {
  const { overview, settings, accounts, resourceVersions, refreshSettingsResources } = useSettingsData();
  const { say, statusAt } = useSettingsStatus();
  const [root, setRoot] = useState("");
  const [java, setJava] = useState("");
  const [ram, setRam] = useState("4096");
  const [width, setWidth] = useState("1280");
  const [height, setHeight] = useState("720");
  const [player, setPlayer] = useState("");
  const [autoMemory, setAutoMemory] = useState(true);
  const [autoChinese, setAutoChinese] = useState(true);
  const [prompt, setPrompt] = useState<null | "root" | "java">(null);
  const [hydrated, setHydrated] = useState(false);

  useEffect(() => {
    if (hydrated) return;
    setRoot(settings.root);
    setJava(settings.java);
    setRam(String(settings.ram));
    setWidth(String(settings.width));
    setHeight(String(settings.height));
    setPlayer(accounts.find((account) => account.id === overview.activeAccountId)?.player ?? "");
    setAutoMemory(settings.autoMemory);
    setAutoChinese(settings.autoChinese);
    setHydrated(true);
  }, [hydrated, settings, accounts, overview]);

  if (!active) return null;

  const roots = Array.from(new Set([...settings.gameRoots, settings.root, root])).filter(
    (value) => value.length > 0,
  );
  const offline = accounts.find((account) => account.id === overview.activeAccountId)?.kind === "offline";

  const persistLaunchSettings = async (patch: AppSettingsPatch, pendingMessage: string) => {
    say("launch:settings", pendingMessage);
    try {
      const result = await saveSettingsCommand(patch, resourceVersions.settings?.revision ?? 0);
      await refreshSettingsResources(result.changed);
      say("launch:settings", result.message);
      return true;
    } catch (error) {
      say("launch:settings", error instanceof Error ? error.message : "保存启动设置失败。");
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
    await persistLaunchSettings(
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

  const switchRoot = async (value: string) => {
    if (await persistLaunchSettings({ root: value }, "正在切换游戏目录…")) setRoot(value);
  };

  return (
    <>
      <SettingsCard
        title="启动"
      >
        <Field label="游戏目录">
          <div className="settings-actions">
            <AppInput
              placeholder="游戏根目录"
              value={root}
              onChange={(event) => setRoot(event.target.value)}
            />
            <button type="button" className="btn ghost" onClick={() => setPrompt("root")}>
              选择
            </button>
          </div>
        </Field>
        <Field label="已保存游戏目录">
          <div className="settings-actions">
            <AppSelect value={root} onChange={(event) => void switchRoot(event.target.value)}>
              {roots.map((value) => (
                <option key={value} value={value}>
                  {value}
                </option>
              ))}
            </AppSelect>
            <button type="button" className="btn ghost" onClick={() => setPrompt("root")}>
              添加并切换
            </button>
          </div>
        </Field>
        <Field label="Java 路径（留空自动选择）">
          <div className="settings-actions">
            <AppInput
              placeholder="留空时由启动核心选择"
              value={java}
              onChange={(event) => setJava(event.target.value)}
            />
            <button type="button" className="btn ghost" onClick={() => setPrompt("java")}>
              选择
            </button>
          </div>
        </Field>
        <div className="launch-numbers">
          <label className="checkbox">
            <input
              type="checkbox"
              checked={autoMemory}
              onChange={(event) => setAutoMemory(event.target.checked)}
            />
            自动分配内存
          </label>
          <Field label="最大内存 MB">
            <AppInput value={ram} onChange={(event) => setRam(event.target.value)} />
          </Field>
          <Field label="游戏窗口宽">
            <AppInput value={width} onChange={(event) => setWidth(event.target.value)} />
          </Field>
          <Field label="游戏窗口高">
            <AppInput value={height} onChange={(event) => setHeight(event.target.value)} />
          </Field>
        </div>
        <label className="checkbox">
          <input
            type="checkbox"
            checked={autoChinese}
            onChange={(event) => setAutoChinese(event.target.checked)}
          />
          启动时自动设置简体中文（仅缺少语言选项时）
        </label>
        <Field label={offline ? "离线玩家名" : "玩家名（在线账号不可修改）"}>
          <AppInput
            placeholder={offline ? "仅离线账号可修改" : ""}
            value={player}
            disabled={!offline}
            onChange={(event) => setPlayer(event.target.value)}
          />
        </Field>
        <div className="settings-actions">
          <button type="button" className="btn primary" onClick={() => void saveLaunch()}>
            保存设置
          </button>
          <span className="status">{statusAt("launch:settings")}</span>
        </div>
      </SettingsCard>

      {prompt === "root" ? (
        <PathPrompt
          title="选择游戏目录"
          label="游戏根目录"
          initial={root}
          hint="目录必须已存在。"
          onCancel={() => setPrompt(null)}
          onConfirm={(path) => {
            setPrompt(null);
            void switchRoot(path);
          }}
        />
      ) : null}

      {prompt === "java" ? (
        <JavaPrompt
          initial={java}
          onCancel={() => setPrompt(null)}
          onConfirm={(path) => {
            setPrompt(null);
            setJava(path);
            say("launch:settings", "已选择 Java 路径，保存后生效。");
          }}
        />
      ) : null}
    </>
  );
}
