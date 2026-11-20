import { useCallback, useEffect, useRef, useState, type ReactNode } from "react";

/** 淡出时长：和 components.css 里 .modal-overlay.is-leaving 的动画保持一致 */
export const MODAL_EXIT_MS = 160;

/**
 * 弹层内部的「关闭」：先播淡出，动画结束再真正 onClose。
 * 卡片里的按钮 / 选项用它，才能和遮罩一起淡出而不是瞬间消失。
 */
type ModalClose = (after?: () => void) => void;

/** 卡片内容 / footer 可以传函数，拿到 close 后再触发真正的关闭 */
type ModalRender = (close: ModalClose) => ReactNode;

interface ModalProps {
  title?: string;
  children: ReactNode | ModalRender;
  footer?: ReactNode | ModalRender;
  onClose: () => void;
  /** 点遮罩关闭：默认不关，只有 Esc / 按钮能关 */
  dismissOnOverlay?: boolean;
  width?: number;
}

/** 居中弹层：Esc 关闭，遮罩拦截点击，对应原 Avalonia 的覆盖页；进出都带淡入淡出。 */
export default function Modal({ title, children, footer, onClose, dismissOnOverlay = false, width }: ModalProps) {
  const [leaving, setLeaving] = useState(false);
  const timer = useRef<number | null>(null);
  /** onClose 常是内联箭头函数，用 ref 兜住，保证 requestClose 稳定 */
  const closeRef = useRef(onClose);
  closeRef.current = onClose;

  /** 先淡出再交给父组件卸载；重复调用只认第一次 */
  const requestClose = useCallback<ModalClose>((after) => {
    if (timer.current !== null) return;
    setLeaving(true);
    timer.current = window.setTimeout(() => {
      after?.();
      closeRef.current();
    }, MODAL_EXIT_MS);
  }, []);

  useEffect(() => {
    const onKey = (event: KeyboardEvent) => {
      if (event.key === "Escape") requestClose();
    };
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [requestClose]);

  // 只在卸载时清计时器：不能跟着 requestClose 清理，否则父组件重渲染会打断淡出
  useEffect(
    () => () => {
      if (timer.current !== null) window.clearTimeout(timer.current);
    },
    [],
  );

  return (
    <div
      className={`modal-overlay${leaving ? " is-leaving" : ""}`}
      role="dialog"
      aria-modal="true"
      onClick={dismissOnOverlay ? () => requestClose() : undefined}
    >
      <div className="modal-card" style={width ? { width } : undefined} onClick={(event) => event.stopPropagation()}>
        {title ? <h2 className="modal-title">{title}</h2> : null}
        {typeof children === "function" ? (children as ModalRender)(requestClose) : children}
        {footer ? (
          <div className="modal-actions">
            {typeof footer === "function" ? (footer as ModalRender)(requestClose) : footer}
          </div>
        ) : null}
      </div>
    </div>
  );
}
