import type { CSSProperties } from "react";
import "./SvgIcon.css";

interface SvgIconProps {
  /** 由 Vite 导入的 SVG 资源地址。 */
  src: string;
  /** 尺寸、颜色等交给调用方已有的类控制。 */
  className?: string;
  /** 可访问名称；省略时按纯装饰图标处理。 */
  label?: string;
}

/**
 * 统一的 SVG 图标出口：图标本体放在 src/assets/icons 下，
 * 组件只负责用 mask 着色，不再在 TSX 里写 svg/path。
 */
export default function SvgIcon({ src, className, label }: SvgIconProps) {
  const style = { "--svg-icon": `url("${src}")` } as CSSProperties;
  return (
    <span
      className={`svg-icon${className ? ` ${className}` : ""}`}
      style={style}
      role={label ? "img" : undefined}
      aria-label={label}
      aria-hidden={label ? undefined : true}
    />
  );
}
