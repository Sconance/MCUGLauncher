import { useEffect, useState } from "react";
import { launcher } from "../api/launcher";
import Field from "../components/Field";
import PathPrompt from "../components/PathPrompt";
import type {
  ContentItem,
  LauncherState,
  PackPreviewInfo,
  ResourceVersionsInfo,
  VanillaVersionInfo,
} from "../types/launcher";
import "./DownloadPage.css";

const CATEGORIES = [
  ["mod", "模组"],
  ["modpack", "整合包"],
  ["resourcepack", "资源包"],
  ["shader", "光影"],
  ["datapack", "数据包"],
] as const;

const LOADERS = [
  ["fabric", "Fabric"],
  ["quilt", "Quilt"],
  ["forge", "Forge"],
  ["neoforge", "NeoForge"],
] as const;

const VANILLA_TYPES = [
  ["release", "正式版"],
  ["snapshot", "快照版"],
  ["old", "远古版"],
  ["all", "全部"],
] as const;

const PAGE_SIZE = 50;

/** 下载页：搜索安装、原版版本、加载器与整合包导入。 */
export default function DownloadPage() {
  const [state, setState] = useState<LauncherState | null>(null);
  const [tab, setTab] = useState<"search" | "vanilla" | "loader" | "pack">("search");
  const [status, setStatus] = useState("");

  const [category, setCategory] = useState<string>("mod");
  const [query, setQuery] = useState("");
  const [results, setResults] = useState<ContentItem[]>([]);
  const [searching, setSearching] = useState(false);
  const [selected, setSelected] = useState<ContentItem | null>(null);
  const [versions, setVersions] = useState<ResourceVersionsInfo | null>(null);
  const [versionsHint, setVersionsHint] = useState("尚未读取兼容版本。");
  const [versionId, setVersionId] = useState("");
  const [targetMode, setTargetMode] = useState("instance");

  const [vanilla, setVanilla] = useState<VanillaVersionInfo[]>([]);
  const [vanillaHint, setVanillaHint] = useState("正在读取 Mojang 版本列表…");
  const [vanillaType, setVanillaType] = useState<string>("release");
  const [vanillaQuery, setVanillaQuery] = useState("");
  const [vanillaPage, setVanillaPage] = useState(0);
  const [vanillaSelected, setVanillaSelected] = useState("");

  const [loaderGame, setLoaderGame] = useState("");
  const [loaderType, setLoaderType] = useState<string>("fabric");
  const [loaderVersions, setLoaderVersions] = useState<string[]>([]);
  const [loaderVersion, setLoaderVersion] = useState("");
  const [loaderHint, setLoaderHint] = useState("");

  const [pack, setPack] = useState<PackPreviewInfo | null>(null);
  const [packName, setPackName] = useState("");
  const [packRoot, setPackRoot] = useState("");
  const [packHint, setPackHint] = useState("");

  const [prompt, setPrompt] = useState<"packFile" | "localImport" | "packRoot" | "download" | null>(null);

  useEffect(() => {
    let alive = true;
    void Promise.all([launcher.getState(), launcher.listVanillaVersions()]).then(([snapshot, vanillaResult]) => {
      if (!alive) return;
      setState(snapshot);
      if (vanillaResult.ok) {
        setVanilla(vanillaResult.items);
        setVanillaHint(`已读取 ${vanillaResult.items.length} 个正式版、快照和远古版本。`);
        const instanceGame =
          snapshot.instances.find((item) => item.name === snapshot.selectedInstance)?.minecraft ?? "";
        setLoaderGame(instanceGame || vanillaResult.items[0]?.id || "");
      } else {
        setVanillaHint(vanillaResult.error);
      }
    });
    return () => {
      alive = false;
    };
  }, []);

  const filteredVanilla = vanilla.filter((item) => {
    const matchesType =
      vanillaType === "all" ||
      (vanillaType === "old" ? item.type === "old_alpha" || item.type === "old_beta" : item.type === vanillaType);
    return matchesType && item.id.toLowerCase().includes(vanillaQuery.trim().toLowerCase());
  });
  const pageCount = Math.max(1, Math.ceil(filteredVanilla.length / PAGE_SIZE));
  const page = Math.min(vanillaPage, pageCount - 1);
  const visibleVanilla = filteredVanilla.slice(page * PAGE_SIZE, page * PAGE_SIZE + PAGE_SIZE);

  useEffect(() => {
    if (visibleVanilla.length > 0 && !visibleVanilla.some((item) => item.id === vanillaSelected)) {
      setVanillaSelected(visibleVanilla[0].id);
    }
  }, [visibleVanilla, vanillaSelected]);

  const currentInstance = state?.instances.find((item) => item.name === state.selectedInstance);
  const instanceHint = currentInstance
    ? `当前实例：${currentInstance.displayName} · ${currentInstance.loader} · ${currentInstance.minecraft}`
    : "尚未选择实例";

  const search = async () => {
    setSelected(null);
    setVersions(null);
    setSearching(true);
    setResults([]);
    setStatus("正在搜索 Modrinth…");
    const result = await launcher.searchContent(category, query.trim());
    setSearching(false);
    if (result.error.length > 0) {
      setStatus(`搜索失败：${result.error}`);
      return;
    }
    setResults(result.items);
    setStatus(`搜索完成，共 ${result.items.length} 项。`);
  };

  const showDetail = (item: ContentItem) => {
    setSelected(item);
    setVersions(null);
    setVersionsHint("尚未读取兼容版本。");
    setVersionId("");
    setTargetMode("instance");
  };

  const loadVersions = async (item: ContentItem) => {
    setVersionsHint("正在读取兼容版本…");
    const result = await launcher.listContentVersions(item.id);
    if (result.ok && result.result) {
      setVersions(result.result);
      setVersionId(result.result.items[0]?.id ?? "");
      setVersionsHint(`${result.result.game} · ${result.result.loader} · ${result.result.items.length} 个兼容版本`);
    } else {
      setVersionsHint(`读取版本失败：${result.error}`);
    }
  };

  const queueDownload = async () => {
    if (!versions || versionId.length === 0) {
      setStatus("请选择资源版本。");
      return;
    }
    const localOnly = category === "modpack" || category === "datapack";
    if (localOnly || targetMode === "local") {
      setPrompt("download");
      return;
    }
    await launcher.queueContentVersion(versions.token, versionId, "instance");
    setStatus("下载请求已提交，请查看任务列表。");
  };

  const toggleBookmark = async (item: ContentItem) => {
    const result = await launcher.toggleBookmark(item.id, item.name, item.description, item.author, category);
    setState(await launcher.getState());
    setStatus(result.message);
  };

  const installVanilla = async () => {
    if (vanillaSelected.length === 0) {
      setVanillaHint("请先读取并选择一个原版版本。");
      return;
    }
    setVanillaHint("正在提交原版安装…");
    await launcher.installVanilla(vanillaSelected);
    setVanillaHint(`原版安装请求已提交：${vanillaSelected}`);
  };

  const loadLoaderVersions = async () => {
    if (loaderGame.length === 0) {
      setLoaderHint("请先选择游戏版本。");
      return;
    }
    setLoaderVersions([]);
    setLoaderHint(`正在读取 ${LOADERS.find(([value]) => value === loaderType)?.[1] ?? ""} 版本…`);
    const result = await launcher.listLoaders(loaderGame, loaderType);
    if (result.ok && result.result) {
      setLoaderVersions(result.result.versions);
      setLoaderVersion(result.result.versions[0] ?? "");
      setLoaderHint(
        result.result.versions.length === 0
          ? "没有读取到加载器版本。"
          : `已读取 ${result.result.versions.length} 个 ${
              LOADERS.find(([value]) => value === loaderType)?.[1] ?? ""
            } 版本。`,
      );
    } else {
      setLoaderHint(`读取加载器版本失败：${result.error}`);
    }
  };

  const installLoader = async () => {
    if (loaderGame.length === 0 || loaderVersion.length === 0) {
      setLoaderHint("请先选择游戏版本和加载器版本。");
      return;
    }
    await launcher.installLoader(loaderGame, loaderType, loaderVersion);
    setLoaderHint(
      `安装请求已提交：${loaderGame} / ${LOADERS.find(([value]) => value === loaderType)?.[1] ?? ""} ${loaderVersion}`,
    );
  };

  const previewPack = async (path: string) => {
    setPackHint("正在读取整合包…");
    const preview = await launcher.previewPack(path);
    setPack(preview);
    setPackName(preview.name);
    setPackRoot(preview.root);
    setPackHint("已读取整合包信息，确认后加入导入队列。");
  };

  const confirmPack = async () => {
    if (!pack) return;
    if (packName.trim().length === 0 || packRoot.trim().length === 0) {
      setPackHint("请填写实例名称并选择已存在的安装目录。");
      return;
    }
    setPackHint("正在提交导入…");
    await launcher.confirmPack(pack.token, packName.trim(), packRoot.trim());
    setPackHint("整合包导入请求已提交，请查看任务列表。");
  };

  const loaderGames = Array.from(
    new Set(
      [vanilla.map((item) => item.id), currentInstance?.minecraft ?? ""]
        .flat()
        .filter((value) => value.length > 0),
    ),
  );

  return (
    <div className="page">
      <div className="page-head">
        <h1 className="page-title">下载</h1>
        <span className="spacer" />
        <span className="muted">{instanceHint}</span>
      </div>

      <div className="tabs" style={{ marginTop: 12 }}>
        {(
          [
            ["search", "搜索下载"],
            ["vanilla", "原版游戏"],
            ["loader", "加载器"],
            ["pack", "整合包导入"],
          ] as const
        ).map(([key, label]) => (
          <button key={key} type="button" className={`tab ${tab === key ? "active" : ""}`} onClick={() => setTab(key)}>
            {label}
          </button>
        ))}
      </div>

      <div className="download-body">
        {tab === "search" ? (
          <div className="stack" style={{ gap: 12 }}>
            <div className="row" style={{ gap: 8 }}>
              <select
                className="select"
                style={{ width: 140 }}
                value={category}
                onChange={(event) => setCategory(event.target.value)}
              >
                {CATEGORIES.map(([value, label]) => (
                  <option key={value} value={value}>
                    {label}
                  </option>
                ))}
              </select>
              <input
                className="input"
                placeholder="搜索关键词"
                value={query}
                onChange={(event) => setQuery(event.target.value)}
                onKeyDown={(event) => {
                  if (event.key === "Enter") void search();
                }}
              />
              <button type="button" className="btn primary" style={{ marginRight: 10 }} onClick={() => void search()}>
                搜索
              </button>
            </div>

            {selected ? (
              <section className="card stack" style={{ gap: 8 }}>
                <h2 className="h2">{selected.name}</h2>
                <p>{selected.description}</p>
                <p className="muted">
                  {[
                    `项目 ID ${selected.id}`,
                    selected.author.length > 0 ? `作者 ${selected.author}` : "",
                    selected.downloads.length > 0 ? `${selected.downloads} 次下载` : "",
                    selected.tags.join(" / "),
                  ]
                    .filter((value) => value.length > 0)
                    .join(" · ")}
                </p>
                <p className="muted">{versionsHint}</p>
                {!versions ? (
                  <button
                    type="button"
                    className="btn primary"
                    style={{ alignSelf: "flex-start" }}
                    onClick={() => void loadVersions(selected)}
                  >
                    选择版本并安装
                  </button>
                ) : null}
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
                      value={category === "modpack" || category === "datapack" ? "local" : targetMode}
                      onChange={(event) => setTargetMode(event.target.value)}
                    >
                      {category === "modpack" || category === "datapack" ? (
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
                    onClick={() => void queueDownload()}
                  >
                    开始下载
                  </button>
                  <button type="button" className="btn ghost" onClick={() => setSelected(null)}>
                    返回搜索结果
                  </button>
                </div>
              </section>
            ) : (
              <div className="download-results">
                {searching ? <p className="muted">正在搜索…</p> : null}
                {!searching && results.length === 0 ? (
                  <p className="muted">没有搜索结果。输入关键词后搜索 Modrinth。</p>
                ) : null}
                {results.map((item) => {
                  const bookmarked = state?.favorites.some((favorite) => favorite.id === item.id) ?? false;
                  return (
                    <article className="card download-card" key={item.id}>
                      <span className="download-name">{item.name}</span>
                      <span className="muted">{item.description}</span>
                      <span className="muted">
                        {[item.author.length > 0 ? `作者 ${item.author}` : "", item.downloads.length > 0 ? `${item.downloads} 次下载` : ""]
                          .filter((value) => value.length > 0)
                          .join(" · ")}
                      </span>
                      <div className="row" style={{ gap: 6, marginTop: 10 }}>
                        <button type="button" className="btn ghost" onClick={() => showDetail(item)}>
                          详情
                        </button>
                        <button
                          type="button"
                          className="btn ghost"
                          onClick={() => {
                            showDetail(item);
                            void loadVersions(item);
                          }}
                        >
                          选择版本
                        </button>
                        <button type="button" className="btn ghost" onClick={() => void toggleBookmark(item)}>
                          {bookmarked ? "取消收藏" : "收藏"}
                        </button>
                      </div>
                    </article>
                  );
                })}
              </div>
            )}
          </div>
        ) : null}

        {tab === "vanilla" ? (
          <div className="stack" style={{ gap: 10 }}>
            <p className="muted">搜索并浏览完整 Mojang 版本清单，包括正式版、快照和远古版本。</p>
            <div className="row" style={{ gap: 8 }}>
              <input
                className="input"
                placeholder="搜索 Minecraft 版本"
                value={vanillaQuery}
                onChange={(event) => {
                  setVanillaQuery(event.target.value);
                  setVanillaPage(0);
                }}
              />
              <select
                className="select"
                style={{ width: 130 }}
                value={vanillaType}
                onChange={(event) => {
                  setVanillaType(event.target.value);
                  setVanillaPage(0);
                }}
              >
                {VANILLA_TYPES.map(([value, label]) => (
                  <option key={value} value={value}>
                    {label}
                  </option>
                ))}
              </select>
              <button
                type="button"
                className="btn ghost"
                onClick={() =>
                  void launcher.listVanillaVersions().then((result) => {
                    if (!result.ok) {
                      setVanillaHint(result.error);
                      return;
                    }
                    setVanilla(result.items);
                    setVanillaPage(0);
                    setVanillaHint(`已读取 ${result.items.length} 个正式版、快照和远古版本。`);
                  })
                }
              >
                刷新列表
              </button>
            </div>
            <div className="row" style={{ gap: 8 }}>
              <button type="button" className="btn primary" onClick={() => void installVanilla()}>
                安装所选原版
              </button>
            </div>
            <div className="version-list tall">
              {visibleVanilla.map((item) => (
                <button
                  key={item.id}
                  type="button"
                  className={`list-item ${item.id === vanillaSelected ? "selected" : ""}`}
                  onClick={() => setVanillaSelected(item.id)}
                >
                  {item.id} · {item.released}
                </button>
              ))}
            </div>
            <div className="pager">
              <button
                type="button"
                className="btn ghost"
                disabled={page === 0}
                onClick={() => setVanillaPage((previous) => Math.max(0, previous - 1))}
              >
                上一页
              </button>
              <span className="muted">
                {page + 1} / {pageCount} 页 · {filteredVanilla.length} 个版本
              </span>
              <button
                type="button"
                className="btn ghost"
                disabled={page + 1 >= pageCount}
                onClick={() => setVanillaPage((previous) => Math.min(pageCount - 1, previous + 1))}
              >
                下一页
              </button>
            </div>
            <p className="muted">{vanillaHint}</p>
          </div>
        ) : null}

        {tab === "loader" ? (
          <div className="stack" style={{ gap: 10 }}>
            <p className="muted">为指定 Minecraft 版本安装 Fabric、Quilt、Forge 或 NeoForge。</p>
            <div className="row" style={{ gap: 10 }}>
              <span>游戏版本</span>
              <select className="select" value={loaderGame} onChange={(event) => setLoaderGame(event.target.value)}>
                {loaderGames.map((game) => (
                  <option key={game} value={game}>
                    {game}
                  </option>
                ))}
              </select>
              <button type="button" className="btn ghost" onClick={() => setLoaderHint("版本列表已是最新。")}>
                读取版本
              </button>
            </div>
            <div className="row" style={{ gap: 10 }}>
              <span>加载器</span>
              <select className="select" value={loaderType} onChange={(event) => setLoaderType(event.target.value)}>
                {LOADERS.map(([value, label]) => (
                  <option key={value} value={value}>
                    {label}
                  </option>
                ))}
              </select>
              <button type="button" className="btn ghost" onClick={() => void loadLoaderVersions()}>
                读取加载器版本
              </button>
            </div>
            <div className="row" style={{ gap: 10 }}>
              <span>加载器版本</span>
              <select className="select" value={loaderVersion} onChange={(event) => setLoaderVersion(event.target.value)}>
                {loaderVersions.length === 0 ? <option value="">尚未读取</option> : null}
                {loaderVersions.map((version) => (
                  <option key={version} value={version}>
                    {version}
                  </option>
                ))}
              </select>
            </div>
            <button type="button" className="btn primary" style={{ alignSelf: "flex-start" }} onClick={() => void installLoader()}>
              安装加载器
            </button>
            <p className="muted">{loaderHint}</p>
          </div>
        ) : null}

        {tab === "pack" ? (
          <div className="stack" style={{ gap: 10 }}>
            <p className="muted">选择 .mrpack 或 .zip 整合包，先预览再确认导入。</p>
            <button type="button" className="btn ghost" style={{ alignSelf: "flex-start" }} onClick={() => setPrompt("packFile")}>
              选择整合包文件
            </button>
            <p className="muted">也可导入模组、资源包、光影或世界存档；拖入启动器窗口即可检查。</p>
            <button type="button" className="btn ghost" style={{ alignSelf: "flex-start" }} onClick={() => setPrompt("localImport")}>
              选择本地文件
            </button>

            {pack ? (
              <section className="card stack" style={{ gap: 10 }}>
                <h2 className="h2">{pack.name}</h2>
                <p className="muted">
                  {pack.game} · {pack.loader} · 来源目录 {pack.root}
                </p>
                <Field label="实例名称">
                  <input
                    className="input"
                    placeholder="1–100 个字符"
                    value={packName}
                    onChange={(event) => setPackName(event.target.value)}
                  />
                </Field>
                <Field label="安装目录">
                  <div className="row" style={{ gap: 8 }}>
                    <input
                      className="input"
                      placeholder="已存在的游戏目录"
                      value={packRoot}
                      onChange={(event) => setPackRoot(event.target.value)}
                    />
                    <button type="button" className="btn ghost" onClick={() => setPrompt("packRoot")}>
                      选择
                    </button>
                  </div>
                </Field>
                <button type="button" className="btn primary" style={{ alignSelf: "flex-start" }} onClick={() => void confirmPack()}>
                  确认导入
                </button>
              </section>
            ) : null}
            <p className="muted">{packHint}</p>
          </div>
        ) : null}
      </div>

      <p className="status" style={{ marginTop: 8 }}>
        {status}
      </p>

      {prompt === "packFile" ? (
        <PathPrompt
          title="选择整合包"
          label="整合包文件路径"
          initial="D:/Downloads/example.mrpack"
          hint="支持 .mrpack 与 .zip。"
          onCancel={() => setPrompt(null)}
          onConfirm={(path) => {
            setPrompt(null);
            void previewPack(path);
          }}
        />
      ) : null}

      {prompt === "localImport" ? (
        <PathPrompt
          title="选择本地文件"
          label="文件路径"
          initial="D:/Downloads/example.jar"
          hint="支持模组、资源包、光影、存档与整合包。"
          onCancel={() => setPrompt(null)}
          onConfirm={(path) => {
            setPrompt(null);
            setStatus(`已选择本地文件：${path}（导入对话框待接入）`);
          }}
        />
      ) : null}

      {prompt === "packRoot" ? (
        <PathPrompt
          title="选择安装目录"
          label="游戏目录"
          initial={packRoot.length > 0 ? packRoot : (state?.root ?? "")}
          hint="必须是已存在的游戏目录。"
          onCancel={() => setPrompt(null)}
          onConfirm={(path) => {
            setPrompt(null);
            setPackRoot(path);
          }}
        />
      ) : null}

      {prompt === "download" ? (
        <PathPrompt
          title="选择下载目录"
          label="保存位置"
          initial="D:/Downloads"
          hint="资源将下载到该目录。"
          onCancel={() => setPrompt(null)}
          onConfirm={(path) => {
            setPrompt(null);
            if (!versions || versionId.length === 0) return;
            void launcher.queueContentVersion(versions.token, versionId, "local", path);
            setStatus("下载请求已提交，请查看任务列表。");
          }}
        />
      ) : null}
    </div>
  );
}
