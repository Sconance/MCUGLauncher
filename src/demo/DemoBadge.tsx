/**
 * 演示模式角标：只在 `npm run dev:demo` 出现，
 * 用来提醒「这里的数据都是假的」，避免把演示值当成真实状态。
 */
export default function DemoBadge() {
  return (
    <div
      role="status"
      title="演示模式：数据来自 src/demo，不会读写真实游戏目录"
      style={{
        position: "fixed",
        left: 18,
        top: 18,
        zIndex: 90,
        display: "flex",
        alignItems: "center",
        gap: 8,
        padding: "6px 12px",
        borderRadius: 999,
        background: "rgba(18, 26, 38, 0.82)",
        color: "#dbe7f5",
        fontSize: 11.5,
        letterSpacing: 0.2,
        boxShadow: "0 10px 26px rgba(0,0,0,0.28)",
        backdropFilter: "blur(14px)",
        pointerEvents: "none",
      }}
    >
      <span
        style={{
          width: 7,
          height: 7,
          borderRadius: "50%",
          background: "#f0b429",
          boxShadow: "0 0 8px #f0b429",
        }}
      />
      演示模式 · 假数据
    </div>
  );
}
