import type { ReactNode } from "react";
import navAccount from "../assets/nav-account.svg";
import navBookmarks from "../assets/nav-bookmarks.svg";
import navCommunity from "../assets/nav-community.svg";
import navGames from "../assets/nav-games.svg";
import navInstances from "../assets/nav-instances.svg";
import navTasks from "../assets/nav-tasks.svg";
import SectionNav, { type NavItem } from "../components/SectionNav";
import AccountPage from "./AccountPage";
import BookmarksPage from "./BookmarksPage";
import CommunityPage from "./CommunityPage";
import GamesPage from "./GamesPage";
import InstancesPage from "./InstancesPage";
import TasksPage from "./TasksPage";
import { useFadeSwap } from "../shell/fade";

interface Section {
  key: string;
  label: string;
  icon: string;
  page: ReactNode;
}

const SECTIONS: Section[] = [
  { key: "instances", label: "实例", icon: navInstances, page: <InstancesPage /> },
  { key: "games", label: "运行中的游戏", icon: navGames, page: <GamesPage /> },
  { key: "tasks", label: "下载任务", icon: navTasks, page: <TasksPage /> },
  { key: "bookmarks", label: "我的收藏", icon: navBookmarks, page: <BookmarksPage /> },
  { key: "account", label: "账号与皮肤", icon: navAccount, page: <AccountPage /> },
  { key: "community", label: "社区管理", icon: navCommunity, page: <CommunityPage /> },
];

const NAV_GROUPS: NavItem[][] = [
  [
    { key: "instances", label: "实例", icon: navInstances },
    { key: "games", label: "运行中的游戏", icon: navGames },
  ],
  [
    { key: "tasks", label: "下载任务", icon: navTasks },
    { key: "bookmarks", label: "我的收藏", icon: navBookmarks },
  ],
  [
    { key: "account", label: "账号与皮肤", icon: navAccount },
    { key: "community", label: "社区管理", icon: navCommunity },
  ],
];

interface ExtrasPageProps {
  active: string;
  onChange: (key: string) => void;
}

/** 对应原 ExtrasView：左侧分区导航，右侧承载六个子页面。 */
export default function ExtrasPage({ active, onChange }: ExtrasPageProps) {
  const { shown, out } = useFadeSwap(active);

  return (
    <div className="extras">
      <aside className="extras-nav">
        <SectionNav groups={NAV_GROUPS} active={active} onSelect={onChange} title="更多" label="更多页面" />
      </aside>
      <section className={`extras-body fade-swap${out ? " is-out" : ""}`}>
        {SECTIONS.map((section) => (
          <div key={section.key} className="extras-panel" hidden={section.key !== shown}>
            {section.page}
          </div>
        ))}
      </section>
    </div>
  );
}
