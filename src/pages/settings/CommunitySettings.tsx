import { useEffect, useState } from "react";
import {
  saveCustomSources as saveCustomSourcesCommand,
  saveSettings as saveSettingsCommand,
  testDataSource as testDataSourceCommand,
  useDataSources as useDataSourcesCommand,
  verifyKey as verifyKeyCommand,
} from "../../api/commands/settings";
import { AppInput } from "../../components/AppInput";
import Field from "../../components/Field";
import type { DataSourceSet } from "../../types/launcher";
import SettingsCard from "./SettingsCard";
import { useSettingsData } from "./SettingsData";
import { useSettingsStatus } from "./useSettingsStatus";

interface CommunitySettingsProps {
  active: boolean;
}

const EMPTY_SOURCES: DataSourceSet = {
  announcements: "",
  servers: "",
  recommendations: "",
  quotes: "",
};

/** 社区：数据源地址与社区身份密钥。 */
export default function CommunitySettings({ active }: CommunitySettingsProps) {
  const { settings, resourceVersions, refreshState } = useSettingsData();
  const { say, statusAt } = useSettingsStatus();
  const [sources, setSources] = useState<DataSourceSet>(EMPTY_SOURCES);
  const [anonymous, setAnonymous] = useState(false);
  const [communityKey, setCommunityKey] = useState("");
  const [adminKey, setAdminKey] = useState("");
  const [hydrated, setHydrated] = useState(false);

  useEffect(() => {
    if (hydrated) return;
    setAnonymous(settings.anonymous);
    const custom = settings.customDataSources;
    const hasCustom = Object.values(custom).some((value) => value.length > 0);
    setSources(hasCustom ? custom : settings.dataSources);
    setHydrated(true);
  }, [hydrated, settings]);

  if (!active) return null;

  const testSource = async (label: string, url: string) => {
    if (url.trim().length === 0) {
      say("community:data", `${label}地址为空。`);
      return;
    }
    say("community:data", `正在测试${label}…`);
    const result = await testDataSourceCommand(label, url.trim());
    say(
      "community:data",
      `${result.name}：${result.ok ? "可用" : "失败"} · ${result.message} · ${result.ms} ms`,
    );
  };

  const saveSources = async (use: boolean) => {
    await saveCustomSourcesCommand(sources);
    if (use) await useDataSourcesCommand("custom");
    await refreshState();
    say(
      "community:data",
      use ? "已保存并切换到自定义数据源。" : "已保存自定义数据源，可点击「使用自定义」切换。",
    );
  };

  const saveAnonymous = async () => {
    const result = await saveSettingsCommand(
      { anonymous },
      resourceVersions.settings?.revision ?? 0,
    );
    await refreshState();
    say("community:identity", result.message);
  };

  const verifyKey = async (role: string, key: string, clear?: () => void) => {
    if (key.trim().length === 0) {
      say("community:identity", "请输入密钥。");
      return;
    }
    say("community:identity", "正在验证…");
    const result = await verifyKeyCommand(role, key.trim());
    say("community:identity", result.message);
    if (result.ok && clear) clear();
  };

  return (
    <div className="settings-cards">
      <SettingsCard
        title="社区"
        description="公告、服务器、推荐、语录分别读取独立 JSON；保存自定义地址后可切换默认与自定义。"
      >
        {(
          [
            ["announcements", "公告地址"],
            ["servers", "服务器地址"],
            ["recommendations", "推荐地址"],
            ["quotes", "语录地址"],
          ] as const
        ).map(([key, label]) => (
          <Field key={key} label={label}>
            <div className="settings-actions">
              <AppInput
                value={sources[key]}
                onChange={(event) => setSources({ ...sources, [key]: event.target.value })}
              />
              <button
                type="button"
                className="btn ghost"
                onClick={() => void testSource(label.replace("地址", ""), sources[key])}
              >
                测试
              </button>
            </div>
          </Field>
        ))}
        <div className="settings-actions">
          <button type="button" className="btn primary" onClick={() => void saveSources(false)}>
            保存自定义
          </button>
          <button type="button" className="btn ghost" onClick={() => void saveSources(true)}>
            使用自定义
          </button>
          <button
            type="button"
            className="btn ghost"
            onClick={() =>
              void useDataSourcesCommand("default").then(async (result) => {
                await refreshState();
                say("community:data", result.message);
              })
            }
          >
            使用默认
          </button>
        </div>
        <p className="status">
          {statusAt("community:data") ||
            `当前使用${settings.dataSourceMode === "custom" ? "自定义" : "默认"}数据源。`}
        </p>
      </SettingsCard>

      <SettingsCard
        title="社区身份"
        description="匿名模式影响主页身份展示；密钥只保存在本机。"
      >
        <label className="checkbox">
          <input
            type="checkbox"
            checked={anonymous}
            onChange={(event) => setAnonymous(event.target.checked)}
          />
          匿名模式
        </label>
        <button
          type="button"
          className="btn ghost settings-self-start"
          onClick={() => void saveAnonymous()}
        >
          保存匿名设置
        </button>
        <Field label="社区身份密钥">
          <div className="settings-actions">
            <AppInput
              type="password"
              placeholder="向群管理员获取"
              value={communityKey}
              onChange={(event) => setCommunityKey(event.target.value)}
            />
            <button
              type="button"
              className="btn ghost"
              onClick={() => void verifyKey("community", communityKey, () => setCommunityKey(""))}
            >
              验证
            </button>
          </div>
        </Field>
        <Field label="管理员密钥">
          <div className="settings-actions">
            <AppInput
              type="password"
              placeholder="首次输入即设置本机管理员密钥"
              value={adminKey}
              onChange={(event) => setAdminKey(event.target.value)}
            />
            <button type="button" className="btn ghost" onClick={() => void verifyKey("admin", adminKey)}>
              验证
            </button>
          </div>
        </Field>
        <p className="status">
          {statusAt("community:identity") ||
            `身份密钥：${settings.hasCommunityKey ? "已设置" : "未设置"} · 管理员密钥：${
              settings.hasAdminKey ? "已设置" : "未设置"
            }`}
        </p>
      </SettingsCard>
    </div>
  );
}
