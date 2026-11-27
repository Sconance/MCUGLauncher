import type { ReactNode } from "react";

interface SettingsCardProps {
  /** 卡片标题，可带 SettingTag / SettingHint 等行内元素。 */
  title: ReactNode;
  children: ReactNode;
  className?: string;
}

/** 设置页的标准分组：卡片外标题 + 圆角内容卡片。 */
export default function SettingsCard({ title, children, className }: SettingsCardProps) {
  return (
    <section className="settings-section">
      <h2 className="h2 settings-section-title">{title}</h2>
      <div className={`card stack settings-card${className ? ` ${className}` : ""}`}>
        {children}
      </div>
    </section>
  );
}
