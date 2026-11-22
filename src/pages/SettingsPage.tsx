import { useState } from "react";
import SplitPage from "../components/SplitPage";
import { useLauncher } from "../state/launcher";
import type { NavItem } from "../components/SectionNav";
import AboutSettings from "./settings/AboutSettings";
import AppearanceSettings from "./settings/AppearanceSettings";
import CommunityManagementSettings from "./settings/CommunityManagementSettings";
import CommunitySettings from "./settings/CommunitySettings";
import DeveloperSettings from "./settings/DeveloperSettings";
import LaunchSettings from "./settings/LaunchSettings";
import MaintenanceSettings from "./settings/MaintenanceSettings";
import { SettingsDataProvider } from "./settings/SettingsData";
import navAbout from "../assets/icons/nav/nav-about.svg";
import navAnnouncement from "../assets/icons/nav/nav-announcement.svg";
import navAppearance from "../assets/icons/nav/nav-appearance.svg";
import navCommunity from "../assets/icons/nav/nav-community.svg";
import navDeveloper from "../assets/icons/nav/nav-developer.svg";
import navLaunch from "../assets/icons/nav/nav-launch.svg";
import navRecovery from "../assets/icons/nav/nav-recovery.svg";
import "./SettingsPage.css";

type SectionKey =
  | "launch"
  | "community"
  | "communityManage"
  | "appearance"
  | "maintenance"
  | "developer"
  | "about";

const NAV_GROUPS: NavItem[][] = [
  [
    { key: "appearance", label: "外观", icon: navAppearance },
    { key: "launch", label: "启动", icon: navLaunch },
  ],
  [
    { key: "community", label: "社区", icon: navCommunity },
    { key: "communityManage", label: "社区管理", icon: navAnnouncement },
  ],
  [
    { key: "maintenance", label: "维护", icon: navRecovery },
    { key: "developer", label: "开发者选项", icon: navDeveloper },
  ],
  [{ key: "about", label: "关于与许可", icon: navAbout }],
];

/**
 * 设置页只负责左侧导航与分区切换。
 *
 * 每个分区的状态、请求和界面都在 `./settings/*Settings.tsx` 内；
 * 七个分区常驻挂载、用 active 控制显隐，切换分区时未保存的输入不会丢。
 */
export default function SettingsPage() {
  const { overview, settings, accounts, resourceVersions, refreshSettingsResources } = useLauncher();
  const [section, setSection] = useState<SectionKey>("launch");

  if (!overview || !settings || !accounts) {
    return (
      <div className="page">
        <div className="empty">正在加载设置…</div>
      </div>
    );
  }

  return (
    <SettingsDataProvider
      value={{ overview, settings, accounts, resourceVersions, refreshSettingsResources }}
    >
      <SplitPage
        title="设置"
        navLabel="设置分区"
        groups={NAV_GROUPS}
        active={section}
        onSelect={(key) => setSection(key as SectionKey)}
        contentClassName="settings-scroll"
      >
        {(shown) => (
          <>
            <LaunchSettings active={shown === "launch"} />
            <CommunitySettings active={shown === "community"} />
            <CommunityManagementSettings active={shown === "communityManage"} />
            <AppearanceSettings active={shown === "appearance"} />
            <MaintenanceSettings active={shown === "maintenance"} />
            <DeveloperSettings active={shown === "developer"} />
            <AboutSettings active={shown === "about"} />
          </>
        )}
      </SplitPage>
    </SettingsDataProvider>
  );
}
