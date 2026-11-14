import { useEffect, useState } from "react";
import { launcher } from "../api/launcher";
import PathPrompt from "../components/PathPrompt";
import type { FavoriteInfo, LauncherState, ResourceVersionsInfo } from "../types/launcher";
import "./BookmarksPage.css";

function kindLabel(kind: string): string {
  switch (kind) {
    case "mod":
      return "模组";
    case "modpack":
      return "整合包";
    case "resourcepack":
      return "资源包";
    case "shader":
      return "光影";
    case "datapack":
      return "数据包";
    default:
      return kind;
  }
}

/** 我的收藏：收藏列表、选择版本安装与取消收藏。 */
export default function BookmarksPage() {
  const [state, setState] = useState<LauncherState | null>(null);
  const [status, setStatus] = useState("");
  const [selected, setSelected] = useState<FavoriteInfo | null>(null);
  const [versions, setVersions] = useState<ResourceVersionsInfo | null>(null);
  const [versionId, setVersionId] = useState("");
  const [mode, setMode] = useState("instance");
  const [hint, setHint] = useState("");
  const [folderPrompt, setFolderPrompt] = useState(false);

  useEffect(() => {
    let alive = true;
    void launcher.getState().then((snapshot) => {
      if (alive) setState(snapshot);
    });
    return () => {
      alive = false;
    };
  }, []);

  const refresh = async () => setState(await launcher.getState());

  const openInstall = async (favorite: FavoriteInfo) => {
    setSelected(favorite);
    setVersions(null);
    setVersionId("");
    setMode("instance");
    setHint("正在读取兼容版本…");
    const result = await launcher.listContentVersions(favorite.id);
    if (!result.ok || !result.result) {
      setHint(`读取版本失败：${result.error}`);
      return;
    }
    setVersions(result.result);
    setVersionId(result.result.items[0]?.id ?? "");
    setHint(
      result.result.items.length === 0
        ? `${result.result.game} · ${result.result.loader} · 当前实例没有兼容版本，请先在启动页切换实例。`
        : `${result.result.game} · ${result.result.loader} · ${result.result.items.length} 个兼容版本`,
    );
  };

  const startInstall = async () => {
    if (!selected || !versions || versionId.length === 0) {
      setStatus("请选择要安装的版本。");
      return;
    }
    const localOnly = selected.kind === "modpack" || selected.kind === "datapack";
    if (localOnly || mode === "local") {
      setFolderPrompt(true);
      return;
    }
    await launcher.queueContentVersion(versions.token, versionId, "instance");
    setStatus("下载请求已提交，请查看任务列表。");
  };

  const removeFavorite = async (favorite: FavoriteInfo) => {
    if (selected?.id === favorite.id) closeInstall();
    const result = await launcher.toggleBookmark(favorite.id, favorite.name, favorite.description, "", favorite.kind);
    await refresh();
    setStatus(`${result.message} ${favorite.name}`);
  };

  const closeInstall = () => {
    setSelected(null);
    setVersions(null);
    setVersionId("");
    setHint("");
  };

  if (!state) {
    return (
      <div className="subpage">
        <div className="empty">正在读取收藏…</div>
      </div>
    );
  }

  return (
    <div className="subpage">
      <div className="stack" style={{ gap: 6 }}>
        <h1 className="page-title">我的收藏</h1>
        <p className="muted">收藏保存在本机；下载时仍会按当前实例重新检查兼容版本。</p>
      </div>

      <div className="page-scroll" style={{ marginTop: 10 }}>
        <div className="stack" style={{ gap: 8 }}>
          {state.favorites.length === 0 ? (
            <p className="muted">没有收藏的资源。在下载页搜索并收藏后会出现在这里。</p>
          ) : (
            state.favorites.map((favorite) => (
              <article className="card favorite-card" key={favorite.id}>
                <div className="stack" style={{ gap: 2 }}>
                  <span className="favorite-name">{favorite.name}</span>
                  <span className="muted">
                    {kindLabel(favorite.kind)} · 项目 ID {favorite.id}
                  </span>
                  {favorite.description.length > 0 ? <span className="muted">{favorite.description}</span> : null}
                </div>
                <div className="favorite-actions">
                  <button type="button" className="btn primary" onClick={() => void openInstall(favorite)}>
                    选择版本安装
                  </button>
                  <button type="button" className="btn ghost" onClick={() => void removeFavorite(favorite)}>
                    取消收藏
                  </button>
                </div>
              </article>
            ))
          )}

          {selected ? (
            <section className="card stack" style={{ gap: 8 }}>
              <h2 className="h2">安装 {selected.name}</h2>
              <p className="muted">{hint}</p>
              {versions && versions.items.length > 0 ? (
                <>
                  <div className="version-list">
                    {versions.items.map((item) => (
                      <button
                        key={item.id}
                        type="button"
                        className={`list-item ${item.id === versionId ? "selected" : ""}`}
                        onClick={() => setVersionId(item.id)}
                      >
                        {item.name} · {item.files.join(", ")}
                      </button>
                    ))}
                  </div>
                  <select
                    className="select"
                    style={{ width: 200 }}
                    value={selected.kind === "modpack" || selected.kind === "datapack" ? "local" : mode}
                    onChange={(event) => setMode(event.target.value)}
                  >
                    {selected.kind === "modpack" || selected.kind === "datapack" ? (
                      <option value="local">下载到目录</option>
                    ) : (
                      <>
                        <option value="instance">安装到当前实例</option>
                        <option value="local">下载到目录</option>
                      </>
                    )}
                  </select>
                </>
              ) : null}
              <div className="row" style={{ gap: 8 }}>
                <button
                  type="button"
                  className="btn primary"
                  disabled={!versions || versions.items.length === 0}
                  onClick={() => void startInstall()}
                >
                  开始下载
                </button>
                <button type="button" className="btn ghost" onClick={closeInstall}>
                  取消
                </button>
              </div>
            </section>
          ) : null}

          <span className="status">{status}</span>
        </div>
      </div>

      {folderPrompt ? (
        <PathPrompt
          title="选择下载目录"
          label="保存位置"
          initial="D:/Downloads"
          hint="资源将下载到该目录。"
          onCancel={() => setFolderPrompt(false)}
          onConfirm={(path) => {
            setFolderPrompt(false);
            if (!versions || versionId.length === 0) return;
            void launcher.queueContentVersion(versions.token, versionId, "local", path);
            setStatus("下载请求已提交，请查看任务列表。");
          }}
        />
      ) : null}
    </div>
  );
}
