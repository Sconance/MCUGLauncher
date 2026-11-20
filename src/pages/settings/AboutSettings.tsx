import SettingsCard from "./SettingsCard";
import { useSettingsData } from "./SettingsData";

interface AboutSettingsProps {
  active: boolean;
}

/** 关于与许可。 */
export default function AboutSettings({ active }: AboutSettingsProps) {
  const { overview } = useSettingsData();
  if (!active) return null;

  return (
    <SettingsCard title="关于与许可">
      <span className="about-version">MCUG Launcher {overview.appVersion}</span>
      <p className="muted">社区数据仓库：https://github.com/waibibabu1239/MCUGL-storage</p>
      <p className="muted">许可：沿用 MCUG Launcher 原仓库策略。</p>
    </SettingsCard>
  );
}
