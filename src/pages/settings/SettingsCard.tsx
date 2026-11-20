import type { ReactNode } from "react";

interface SettingsCardProps {
  /** 卡片标题，可带 SettingTag / SettingHint 等行内元素。 */
  title: ReactNode;
  children: ReactNode;
  className?: string;
}

/** 设置页的标准内容卡片：标题 + 内容，间距与旧版内联结构一致。 */
export default function SettingsCard({ title, children, className }: SettingsCardProps) {
  return (
    <section className={`card stack settings-card${className ? ` ${className}` : ""}`}>
      <h2 className="h2">{title}</h2>
      {children}
    </section>
  );
}
