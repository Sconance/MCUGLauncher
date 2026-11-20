interface SettingHintProps {
  /** 悬停/聚焦时显示在问号右侧的说明。 */
  tip: string;
}

/** 设置标题后的圆形问号标签：把必要说明收进悬浮提示，避免正文堆描述。 */
export default function SettingHint({ tip }: SettingHintProps) {
  return (
    <span
      className="setting-hint"
      data-tip={tip}
      data-tip-side="right"
      role="img"
      aria-label={tip}
      tabIndex={0}
    >
      <span aria-hidden="true">?</span>
    </span>
  );
}
