import type { ReactNode } from "react";
import AccountPage from "./AccountPage";
import BookmarksPage from "./BookmarksPage";
import CommunityPage from "./CommunityPage";
import GamesPage from "./GamesPage";
import InstancesPage from "./InstancesPage";
import TasksPage from "./TasksPage";

interface Section {
  key: string;
  label: string;
  page: ReactNode;
}

const SECTIONS: Section[] = [
  { key: "instances", label: "实例", page: <InstancesPage /> },
  { key: "account", label: "账号与皮肤", page: <AccountPage /> },
  { key: "tasks", label: "下载任务", page: <TasksPage /> },
  { key: "games", label: "运行中的游戏", page: <GamesPage /> },
  { key: "bookmarks", label: "我的收藏", page: <BookmarksPage /> },
  { key: "community", label: "社区管理", page: <CommunityPage /> },
];

interface ExtrasPageProps {
  active: string;
  onChange: (key: string) => void;
}

/** 对应原 ExtrasView：左侧分区导航，右侧承载六个子页面。 */
export default function ExtrasPage({ active, onChange }: ExtrasPageProps) {
  return (
    <div className="extras">
      <aside className="extras-nav">
        <h2 className="extras-title">更多</h2>
        <nav className="list" aria-label="更多页面">
          {SECTIONS.map((section) => (
            <button
              key={section.key}
              type="button"
              className={`list-item ${section.key === active ? "selected" : ""}`}
              onClick={() => onChange(section.key)}
            >
              {section.label}
            </button>
          ))}
        </nav>
      </aside>
      <section className="extras-body">
        {SECTIONS.map((section) => (
          <div key={section.key} className="extras-panel" hidden={section.key !== active}>
            {section.page}
          </div>
        ))}
      </section>
    </div>
  );
}
