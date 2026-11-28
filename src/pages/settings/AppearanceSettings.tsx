import { useRef, useState } from "react";
import { resetWindow as resetWindowCommand } from "../../api/commands/settings";
import { AppSelect } from "../../components/AppInput";
import { useI18n } from "../../i18n";
import { setBackgroundStyle, useBackgroundStyle } from "../../shell/backgroundStyle";
import { setHomeAlignment, useHomeAlignment } from "../../shell/homeAlignment";
import SettingRow from "./SettingRow";
import SettingsCard from "./SettingsCard";
import WallpaperSettings from "./WallpaperSettings";

interface AppearanceSettingsProps {
  active: boolean;
}

/** 外观：个性化（背景样式等）、语言、主页对齐与窗口尺寸。 */
export default function AppearanceSettings({ active }: AppearanceSettingsProps) {
  const { locale, setLocale, t, packs, labelOf, builtinCodes, importPack, removePack } = useI18n();
  const [languageStatus, setLanguageStatus] = useState("");
  const [windowStatus, setWindowStatus] = useState("");
  const backgroundStyle = useBackgroundStyle();
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

  return (
    <div className="settings-cards">
      <SettingsCard title="个性化">
        <SettingRow
          title="背景样式"
          control={
            <div className="segmented" role="radiogroup" aria-label="背景样式">
              {(
                [
                  ["frosted", "磨砂"],
                  ["opaque", "不透明"],
                ] as const
              ).map(([value, label]) => (
                <button
                  key={value}
                  type="button"
                  role="radio"
                  aria-checked={backgroundStyle === value}
                  className={`segmented-option${backgroundStyle === value ? " is-selected" : ""}`}
                  onClick={() => setBackgroundStyle(value)}
                >
                  {label}
                </button>
              ))}
            </div>
          }
        />
      </SettingsCard>

      <WallpaperSettings />

      <SettingsCard title="外观">
        <SettingRow
          title={
            <>
              {t("settings.language")}
            </>
          }
          control={
            <div className="setting-control">
              <AppSelect
                className="setting-control-select"
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
          {languageStatus.length > 0 ? <span className="setting-subtitle">{languageStatus}</span> : null}
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
              主页按钮对齐
            </>
          }
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
        />
        <SettingRow
          title="恢复默认窗口尺寸"
          control={
            <button
              type="button"
              className="btn ghost"
              onClick={() =>
                void resetWindowCommand().then((result) => setWindowStatus(result.message))
              }
            >
              恢复默认
            </button>
          }
        >
          <span className="setting-subtitle">
            {windowStatus || "将启动器窗口重置为 1200 × 780"}
          </span>
        </SettingRow>
      </SettingsCard>
    </div>
  );
}
