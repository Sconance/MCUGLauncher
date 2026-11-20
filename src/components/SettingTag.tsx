import type { ReactNode } from "react";

/** 设置项标签预设：dev 黄底黑字、beta 青底白字、dep 琥珀底白字（即将弃用）。 */
export type SettingTagKind = "dev" | "beta" | "dep";

const PRESET_LABELS: Record<SettingTagKind, string> = {
  dev: "DEV",
  beta: "BETA",
  dep: "DEP",
};

/** 悬停提示：鼠标移到标签上时在标签右侧显示 */
const PRESET_TIPS: Record<SettingTagKind, string> = {
  dev: "开发中",
  beta: "测试中",
  dep: "已弃用",
};

interface SettingTagProps {
  kind: SettingTagKind;
  /** 覆盖默认提示文案 */
  tip?: string;
  /** 覆盖默认文案；不传则用预设里的 DEV / BETA / DEP */
  children?: ReactNode;
}

/** 设置项标题后面的状态标签，配色由 `data-tag` 在 CSS 里给出，提示复用全局 data-tip。 */
export default function SettingTag({ kind, tip, children }: SettingTagProps) {
  return (
    <span className="setting-tag" data-tag={kind} data-tip={tip ?? PRESET_TIPS[kind]} data-tip-side="right">
      {children ?? PRESET_LABELS[kind]}
    </span>
  );
}
