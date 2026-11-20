import type { ReactNode } from "react";
import navAccount from "../assets/icons/nav/nav-account.svg";
import navBookmarks from "../assets/icons/nav/nav-bookmarks.svg";
import navCommunity from "../assets/icons/nav/nav-community.svg";
import type { NavItem } from "../components/SectionNav";
import SplitPage from "../components/SplitPage";
import AccountPage from "./AccountPage";
import BookmarksPage from "./BookmarksPage";
import CommunityPage from "./CommunityPage";

interface Section {
  key: string;
  label: string;
  icon: string;
  page: ReactNode;
}

const SECTIONS: Section[] = [
  { key: "bookmarks", label: "我的收藏", icon: navBookmarks, page: <BookmarksPage /> },
  { key: "account", label: "账号与皮肤", icon: navAccount, page: <AccountPage /> },
  { key: "community", label: "社区管理", icon: navCommunity, page: <CommunityPage /> },
];

const NAV_GROUPS: NavItem[][] = [
  [{ key: "bookmarks", label: "我的收藏", icon: navBookmarks }],
  [
    { key: "account", label: "账号与皮肤", icon: navAccount },
    { key: "community", label: "社区管理", icon: navCommunity },
  ],
];

interface ExtrasPageProps {
  active: string;
  onChange: (key: string) => void;
}

/** 对应原 ExtrasView：左侧分区导航，右侧承载收藏、账号与社区三个子页面。 */
export default function ExtrasPage({ active, onChange }: ExtrasPageProps) {
  return (
    <SplitPage
      title="更多"
      navLabel="更多页面"
      groups={NAV_GROUPS}
      active={active}
      onSelect={onChange}
      contentClassName="extras-content"
    >
      {(shown) =>
        SECTIONS.map((section) => (
          <div key={section.key} className="extras-panel" hidden={section.key !== shown}>
            {section.page}
          </div>
        ))
      }
    </SplitPage>
  );
}
