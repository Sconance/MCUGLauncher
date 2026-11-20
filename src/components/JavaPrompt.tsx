import { useEffect, useState } from "react";
import { scanJava as scanJavaCommand } from "../api/commands/settings";
import { AppInput } from "./AppInput";
import Field from "./Field";
import Modal from "./Modal";
import "./JavaPrompt.css";

interface JavaPromptProps {
  initial?: string;
  onCancel: () => void;
  onConfirm: (path: string) => void;
}

/**
 * 选择 Java 的弹层：可手填路径，也可扫描本机并直接使用扫描结果。
 * 原先独立「Java 管理」页里的扫描列表已并到这里。
 */
export default function JavaPrompt({ initial, onCancel, onConfirm }: JavaPromptProps) {
  const [value, setValue] = useState(initial ?? "");
  const [items, setItems] = useState<Awaited<ReturnType<typeof scanJavaCommand>>>([]);
  const [scanning, setScanning] = useState(false);
  const [message, setMessage] = useState("");

  const scan = async () => {
    setScanning(true);
    setMessage("正在扫描本机 Java…");
    try {
      const found = await scanJavaCommand();
      setItems(found);
      setMessage(found.length === 0 ? "没有找到可用的 Java。" : `找到 ${found.length} 个 Java。`);
    } catch (error) {
      setMessage(error instanceof Error ? error.message : "扫描失败。");
    } finally {
      setScanning(false);
    }
  };

  // 打开弹层就扫描一次，列表不会因为第一次打开而空着。
  useEffect(() => {
    void scan();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  return (
    <Modal
      title="选择 Java"
      width={560}
      onClose={onCancel}
      footer={(close) => (
        <>
          <button type="button" className="btn ghost" onClick={() => close()}>
            取消
          </button>
          <button
            type="button"
            className="btn primary"
            disabled={value.trim().length === 0}
            onClick={() => close(() => onConfirm(value.trim()))}
          >
            确定
          </button>
        </>
      )}
    >
      {(close) => (
        <div className="stack" style={{ gap: 12 }}>
          <Field label="javaw.exe 路径" hint="可手动填写，或从下方扫描结果中选择。">
            <AppInput
              placeholder="例如 C:\\Program Files\\Java\\bin\\javaw.exe"
              value={value}
              autoFocus
              onChange={(event) => setValue(event.target.value)}
              onKeyDown={(event) => {
                if (event.key === "Enter" && value.trim().length > 0) {
                  close(() => onConfirm(value.trim()));
                }
              }}
            />
          </Field>

          <div className="row" style={{ gap: 8 }}>
            <button type="button" className="btn ghost" disabled={scanning} onClick={() => void scan()}>
              {scanning ? "正在扫描…" : "扫描本机 Java"}
            </button>
            <span className="muted">{message}</span>
          </div>

          <div className="stack java-prompt-results">
            <span className="field-label">扫描结果</span>
            {items.length === 0 ? (
              <p className="muted">{scanning ? "正在查找可用的 Java…" : "还没有扫描结果。"}</p>
            ) : (
              items.map((item) => (
                <div className="java-prompt-option" key={item.path}>
                  <div className="stack" style={{ gap: 2 }}>
                    <span>{`Java ${item.major} · ${item.arch} ${item.vendor}`.trim()}</span>
                    <span className="muted java-prompt-path">{item.path}</span>
                    {item.error.length > 0 ? <span className="badge red">{item.error}</span> : null}
                  </div>
                  <span className="spacer" />
                  <button type="button" className="btn ghost" onClick={() => setValue(item.path)}>
                    使用
                  </button>
                </div>
              ))
            )}
          </div>
        </div>
      )}
    </Modal>
  );
}
