import type { ReactNode } from "react";

interface SettingsCardProps {
  title: ReactNode;
  /** 卡片标题下的一行说明，统一使用 .muted 字号。 */
  description?: ReactNode;
  children: ReactNode;
  className?: string;
}

/** 设置页的标准内容卡片：标题 + 说明 + 内容，间距与旧版内联结构一致。 */
export default function SettingsCard({ title, description, children, className }: SettingsCardProps) {
  return (
    <section className={`card stack settings-card${className ? ` ${className}` : ""}`}>
      <h2 className="h2">{title}</h2>
      {description ? <p className="muted">{description}</p> : null}
      {children}
    </section>
  );
}
