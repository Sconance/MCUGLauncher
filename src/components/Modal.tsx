import { useEffect, type ReactNode } from "react";

interface ModalProps {
  title?: string;
  children: ReactNode;
  footer?: ReactNode;
  onClose: () => void;
  width?: number;
}

/** 居中弹层：Esc 关闭，遮罩拦截点击，对应原 Avalonia 的覆盖页。 */
export default function Modal({ title, children, footer, onClose, width }: ModalProps) {
  useEffect(() => {
    const onKey = (event: KeyboardEvent) => {
      if (event.key === "Escape") onClose();
    };
    window.addEventListener("keydown", onKey);
    return () => window.removeEventListener("keydown", onKey);
  }, [onClose]);

  return (
    <div className="modal-overlay" role="dialog" aria-modal="true">
      <div className="modal-card" style={width ? { width } : undefined}>
        {title ? <h2 className="modal-title">{title}</h2> : null}
        {children}
        {footer ? <div className="modal-actions">{footer}</div> : null}
      </div>
    </div>
  );
}
