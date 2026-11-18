import "./SectionNav.css";

export interface NavItem {
  key: string;
  label: string;
  /** 图标资源路径；不传则只显示文字（例如下载页的分区） */
  icon?: string;
}

interface SectionNavProps {
  /** 按分类分组；每组渲染为一个低不透明度的圆角矩形 */
  groups: NavItem[][];
  active: string;
  onSelect: (key: string) => void;
  title?: string;
  label: string;
}

/** 左侧分区导航：分类圆角块 + 图标条目，对应手机设置页的分组列表。 */
export default function SectionNav({ groups, active, onSelect, title, label }: SectionNavProps) {
  return (
    <nav className="section-nav" aria-label={label}>
      {title ? <h2 className="section-title">{title}</h2> : null}
      {groups.map((group, index) => (
        <div className="section-group" key={index}>
          {group.map((item) => (
            <button
              key={item.key}
              type="button"
              className={`section-item${item.key === active ? " selected" : ""}`}
              aria-current={item.key === active ? "true" : undefined}
              onClick={() => onSelect(item.key)}
            >
              {item.icon ? <img className="section-icon" src={item.icon} alt="" width={16} height={16} /> : null}
              <span className="section-label">{item.label}</span>
            </button>
          ))}
        </div>
      ))}
    </nav>
  );
}
