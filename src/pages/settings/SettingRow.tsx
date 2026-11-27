import type { ReactNode } from "react";

interface SettingRowProps {
  /** 左侧标题，可带 SettingTag 等行内元素。 */
  title: ReactNode;
  /** 标题下的说明，一行一个 span/p 均可。 */
  children?: ReactNode;
  /** 右侧控件（开关、分段选择器、下拉框等）。 */
  control: ReactNode;
}

/** 一行设置：左侧是标题与可选说明，右侧是控件。 */
export default function SettingRow({ title, children, control }: SettingRowProps) {
  const hasSubtitle = children !== undefined && children !== null && children !== false;
  return (
    <div className={`setting-row${hasSubtitle ? " has-subtitle" : ""}`}>
      <div className="setting-copy">
        <span className="setting-title">{title}</span>
        {hasSubtitle ? children : null}
      </div>
      {control}
    </div>
  );
}
