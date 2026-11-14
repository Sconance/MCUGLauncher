import { useState } from "react";
import Field from "./Field";
import Modal from "./Modal";

interface PathPromptProps {
  title: string;
  label: string;
  initial?: string;
  hint?: string;
  onCancel: () => void;
  onConfirm: (path: string) => void;
}

/** 路径输入弹层：浏览器预览无法调用原生文件对话框时的替代方案。 */
export default function PathPrompt({ title, label, initial, hint, onCancel, onConfirm }: PathPromptProps) {
  const [value, setValue] = useState(initial ?? "");

  return (
    <Modal
      title={title}
      onClose={onCancel}
      footer={
        <>
          <button type="button" className="btn ghost" onClick={onCancel}>
            取消
          </button>
          <button
            type="button"
            className="btn primary"
            disabled={value.trim().length === 0}
            onClick={() => onConfirm(value.trim())}
          >
            确定
          </button>
        </>
      }
    >
      <Field label={label} hint={hint}>
        <input
          className="input"
          value={value}
          autoFocus
          onChange={(event) => setValue(event.target.value)}
          onKeyDown={(event) => {
            if (event.key === "Enter" && value.trim().length > 0) onConfirm(value.trim());
          }}
        />
      </Field>
    </Modal>
  );
}
