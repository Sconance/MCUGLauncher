import type { ReactNode } from "react";

/** 设置项标签预设：dev 黄底黑字、beta 青底白字、err 红底白字。 */
export type SettingTagKind = "dev" | "beta" | "err";

const PRESET_LABELS: Record<SettingTagKind, string> = {
  dev: "DEV",
  beta: "BETA",
  err: "ERR",
};

interface SettingTagProps {
  kind: SettingTagKind;
  /** 覆盖默认文案；不传则用预设里的 DEV / BETA / ERR */
  children?: ReactNode;
}

/** 设置项标题后面的状态标签，配色由 `data-tag` 在 CSS 里给出。 */
export default function SettingTag({ kind, children }: SettingTagProps) {
  return (
    <span className="setting-tag" data-tag={kind}>
      {children ?? PRESET_LABELS[kind]}
    </span>
  );
}
