import { useRef, useState } from "react";
import { resetWindow as resetWindowCommand } from "../../api/commands/settings";
import { AppSelect } from "../../components/AppInput";
import SettingTag from "../../components/SettingTag";
import { useI18n } from "../../i18n";
import { setHomeAlignment, useHomeAlignment } from "../../shell/homeAlignment";
import { isLiquidGlass, setLiquidGlass } from "../../shell/liquid";
import SettingRow from "./SettingRow";
import SettingsCard from "./SettingsCard";

interface AppearanceSettingsProps {
  active: boolean;
}

/** 外观：语言、液态玻璃、主页对齐与主题信息。 */
export default function AppearanceSettings({ active }: AppearanceSettingsProps) {
  const { locale, setLocale, t, packs, labelOf, builtinCodes, importPack, removePack } = useI18n();
  const [languageStatus, setLanguageStatus] = useState("");
  const [windowStatus, setWindowStatus] = useState("");
  const [liquidGlass, setLiquidGlassState] = useState(isLiquidGlass);
  const homeAlignment = useHomeAlignment();
  const languageInput = useRef<HTMLInputElement>(null);

  if (!active) return null;

  const importLanguageFile = async (files: FileList | null) => {
    const file = files?.[0];
    if (!file) return;
    try {
      const pack = importPack(await file.text());
      setLanguageStatus(
        t("settings.languageImported", {
          name: pack.nativeName ?? pack.name ?? pack.code,
          code: pack.code,
        }),
      );
    } catch (error) {
      setLanguageStatus(error instanceof Error ? error.message : "导入失败。");
    } finally {
      if (languageInput.current) languageInput.current.value = "";
    }
  };

  const toggleLiquidGlass = (next: boolean) => {
    setLiquidGlassState(next);
    setLiquidGlass(next);
  };

  return (
    <SettingsCard title="外观" description="固定蓝白主题；可恢复默认窗口尺寸。">
      <SettingRow
        title={t("settings.language")}
        control={
          <div className="settings-actions">
            <AppSelect
              style={{ width: 260 }}
              value={locale}
              onChange={(event) => {
                setLocale(event.target.value);
                setLanguageStatus("");
              }}
            >
              {packs.map((pack) => (
                <option key={pack.code} value={pack.code}>
                  {labelOf(pack)}
                </option>
              ))}
            </AppSelect>
            <button type="button" className="btn ghost" onClick={() => languageInput.current?.click()}>
              {t("settings.languageImport")}
            </button>
            {builtinCodes.includes(locale) ? null : (
              <button
                type="button"
                className="btn ghost"
                onClick={() => {
                  removePack(locale);
                  setLanguageStatus("");
                }}
              >
                {t("settings.languageRemove")}
              </button>
            )}
          </div>
        }
      >
        <span className="muted">{t("settings.languageHint")}</span>
        <span className="muted">{t("settings.languagePackHint")}</span>
        {languageStatus.length > 0 ? <span className="muted">{languageStatus}</span> : null}
      </SettingRow>
      <input
        ref={languageInput}
        type="file"
        accept="application/json,.json"
        hidden
        onChange={(event) => void importLanguageFile(event.target.files)}
      />

      <SettingRow
        title={
          <>
            液态玻璃效果
            <SettingTag kind="dev" />
          </>
        }
        control={
          <button
            type="button"
            role="switch"
            aria-checked={liquidGlass}
            aria-label="液态玻璃效果"
            className={`switch${liquidGlass ? " on" : ""}`}
            onClick={() => toggleLiquidGlass(!liquidGlass)}
          >
            <span className="switch-thumb" />
          </button>
        }
      >
        <span className="muted">胶囊栏与内容栏改用折射玻璃；关闭时保持当前的磨砂玻璃。</span>
        <span className="muted">手势与折射都走 GPU，低配设备按住导航按钮时可能出现掉帧。</span>
      </SettingRow>

      <SettingRow
        title="主页按钮对齐"
        control={
          <div className="segmented" role="radiogroup" aria-label="主页按钮对齐">
            {(
              [
                ["right", "向右对齐"],
                ["center", "居中对齐"],
              ] as const
            ).map(([value, label]) => (
              <button
                key={value}
                type="button"
                role="radio"
                aria-checked={homeAlignment === value}
                className={`segmented-option${homeAlignment === value ? " is-selected" : ""}`}
                onClick={() => setHomeAlignment(value)}
              >
                {label}
              </button>
            ))}
          </div>
        }
      >
        <span className="muted">角色、实例与启动按钮在主页底部的位置。</span>
        <span className="muted">居中对齐时启动按钮位于正中，启动水波与进度提示也会随之调整。</span>
      </SettingRow>

      <div className="theme-preview">
        <span className="theme-name">蓝白 · 默认</span>
        <span className="muted">#326EA7 强调色 / #F3F7F6 页面底色</span>
      </div>
      <div className="settings-actions">
        <button
          type="button"
          className="btn ghost"
          onClick={() =>
            void resetWindowCommand().then((result) => setWindowStatus(result.message))
          }
        >
          恢复默认窗口 1200 × 780
        </button>
        <span className="status">{windowStatus}</span>
      </div>
    </SettingsCard>
  );
}
