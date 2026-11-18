import { useLayoutEffect, useRef, type ReactNode } from "react";
import SectionNav, { type NavItem } from "./SectionNav";
import { useFadeSwap } from "../shell/fade";
import "./SplitPage.css";

interface SplitPageProps {
  /** 左栏标题（设置 / 下载 / 发现 / 更多） */
  title?: string;
  /** 左栏导航的无障碍名称 */
  navLabel: string;
  /** 左栏分组，原样交给 SectionNav */
  groups: NavItem[][];
  /** 当前选中的分区：点了立刻变，不等动画 */
  active: string;
  onSelect: (key: string) => void;
  /**
   * 淡出结束、内容真正换到新分区时调用。
   * 数据请求放这里，旧内容才不会在淡出的 130ms 里被清空。
   */
  onShownChange?: (key: string) => void;
  /** 内容下方常驻、不参与滚动的一块（状态行、群友名言等） */
  footer?: ReactNode;
  /** 内容层附加类名（滚动容器等） */
  contentClassName?: string;
  /** 渲染当前应当显示的分区；参数是淡出结束后真正生效的 key */
  children: (shown: string) => ReactNode;
}

/**
 * 左右栏页面骨架：左侧分区导航 + 右侧内容。
 *
 * 切换动画、滚动复位都收在这一层，页面只负责往 children 里填内容，
 * 这样设置、发现、下载、更多四个页面的切换表现不会各写一套。
 */
export default function SplitPage({
  title,
  navLabel,
  groups,
  active,
  onSelect,
  onShownChange,
  footer,
  contentClassName,
  children,
}: SplitPageProps) {
  const { shown, out } = useFadeSwap(active);
  const contentRef = useRef<HTMLDivElement>(null);

  // 换分区后回到顶部；onShownChange 用当前这次渲染的闭包，保证拿到最新状态
  useLayoutEffect(() => {
    contentRef.current?.scrollTo({ top: 0 });
    onShownChange?.(shown);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [shown]);

  return (
    <div className="extras split-page">
      <aside className="extras-nav">
        <SectionNav groups={groups} active={active} onSelect={onSelect} title={title} label={navLabel} />
      </aside>

      <div className="split-body">
        <div
          ref={contentRef}
          className={`split-content${out ? " is-out" : ""}${contentClassName ? ` ${contentClassName}` : ""}`}
        >
          {children(shown)}
        </div>
        {footer}
      </div>
    </div>
  );
}
