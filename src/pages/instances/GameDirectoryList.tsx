import { useState } from "react";
import PathPrompt from "../../components/PathPrompt";

interface GameDirectoryListProps {
  roots: readonly string[];
  activeRoot: string;
  pending: boolean;
  onOpen: (path: string) => void;
}

export default function GameDirectoryList({ roots, activeRoot, pending, onOpen }: GameDirectoryListProps) {
  const [adding, setAdding] = useState(false);

  return (
    <div className="im-all">
      <div className="im-directory-header">
        <h1 className="page-title">游戏目录</h1>
        <button type="button" className="btn primary" disabled={pending} onClick={() => setAdding(true)}>
          添加游戏目录
        </button>
      </div>
      {roots.length === 0 ? <p className="muted">尚未添加游戏目录。</p> : null}
      <div className="im-all-list">
        {roots.map((path) => (
          <div key={path} className={`im-all-item${path === activeRoot ? " is-current" : ""}`}>
            <button
              type="button"
              className="im-all-main"
              disabled={pending}
              aria-current={path === activeRoot}
              onClick={() => onOpen(path)}
            >
              <span className="im-all-text">
                <span className="im-directory-path">{path}</span>
                {path === activeRoot ? <span className="im-all-sub">当前游戏目录</span> : null}
              </span>
            </button>
          </div>
        ))}
      </div>
      {adding ? (
        <PathPrompt
          title="添加游戏目录"
          label="游戏根目录"
          hint="填写已存在的绝对路径，例如你的 .minecraft 目录。"
          onCancel={() => setAdding(false)}
          onConfirm={(path) => {
            setAdding(false);
            onOpen(path);
          }}
        />
      ) : null}
    </div>
  );
}
