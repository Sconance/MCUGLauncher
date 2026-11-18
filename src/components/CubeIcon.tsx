interface CubeIconProps {
  className?: string;
}

/**
 * 正交投影的正方体：顶面最亮、左面中等、右面最暗，用来占位版本图标。
 * 目前所有版本共用这一个图标，后续可按版本主题替换成各自的美术资源。
 */
export default function CubeIcon({ className }: CubeIconProps) {
  return (
    <svg className={className} viewBox="0 0 16 16" aria-hidden="true" focusable="false">
      <path d="M8 1.4 14 4.9v6.2L8 14.6 2 11.1V4.9z" fill="currentColor" opacity="0.16" />
      <path d="M8 1.4 14 4.9 8 8.4 2 4.9z" fill="currentColor" opacity="0.92" />
      <path d="M2 4.9 8 8.4v6.2L2 11.1z" fill="currentColor" opacity="0.55" />
      <path d="M14 4.9 8 8.4v6.2l6-3.5z" fill="currentColor" opacity="0.32" />
    </svg>
  );
}
