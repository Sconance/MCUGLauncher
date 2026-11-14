import { useEffect, useState } from "react";
import { launcher } from "../api/launcher";
import type {
  AnnouncementInfo,
  AvatarInfo,
  CommunityData,
  LauncherState,
  RecommendationInfo,
  SavedServerInfo,
  ServerPingInfo,
} from "../types/launcher";
import Field from "../components/Field";
import Modal from "../components/Modal";
import { useNavigation } from "../shell/navigation";
import "./HomePage.css";

interface ServerDraft {
  name: string;
  address: string;
  instance: string;
}

interface DialogState {
  editing: SavedServerInfo | null;
  draft: ServerDraft;
  status: string;
}

interface MenuState {
  item: RecommendationInfo;
  x: number;
  y: number;
}

const EMPTY_DRAFT: ServerDraft = { name: "", address: "", instance: "" };

function pingText(ping: ServerPingInfo | undefined): string {
  if (!ping) return "尚未查询";
  if (!ping.online) return "离线";
  return `${ping.players}/${ping.max} 人 · ${ping.ping} ms`;
}

function Markdown({ text }: { text: string }) {
  return (
    <div className="markdown">
      {text.split("\n").map((line, index) => {
        const key = `${index}-${line.length}`;
        if (line.startsWith("### ")) return <h4 key={key}>{line.slice(4)}</h4>;
        if (line.startsWith("## ")) return <h3 key={key}>{line.slice(3)}</h3>;
        if (line.startsWith("# ")) return <h2 key={key}>{line.slice(2)}</h2>;
        if (line.startsWith("> ")) return <blockquote key={key}>{line.slice(2)}</blockquote>;
        if (line.startsWith("- ")) return <li key={key}>{line.slice(2)}</li>;
        if (line.trim().length === 0) return <div key={key} className="md-gap" />;
        return <p key={key}>{line}</p>;
      })}
    </div>
  );
}

/** 主页：账号卡、实例启动、服务器收藏、公告、推荐与语录。 */
export default function HomePage() {
  const { openAccount } = useNavigation();
  const [state, setState] = useState<LauncherState | null>(null);
  const [avatar, setAvatar] = useState<AvatarInfo | null>(null);
  const [community, setCommunity] = useState<CommunityData | null>(null);
  const [pings, setPings] = useState<Record<string, ServerPingInfo>>({});
  const [quoteIndex, setQuoteIndex] = useState(0);
  const [status, setStatus] = useState("");
  const [dialog, setDialog] = useState<DialogState | null>(null);
  const [announcement, setAnnouncement] = useState<AnnouncementInfo | null>(null);
  const [menu, setMenu] = useState<MenuState | null>(null);

  useEffect(() => {
    let alive = true;
    void (async () => {
      const [next, avatarInfo, communityData] = await Promise.all([
        launcher.getState(),
        launcher.readAvatar(),
        launcher.getCommunity(),
      ]);
      if (!alive) return;
      setState(next);
      setAvatar(avatarInfo);
      setCommunity(communityData);
    })();
    return () => {
      alive = false;
    };
  }, []);

  useEffect(() => {
    if (!state) return;
    let alive = true;
    for (const server of state.servers) {
      void launcher.pingServer(server.address).then((info) => {
        if (alive) setPings((previous) => ({ ...previous, [info.id]: info }));
      });
    }
    return () => {
      alive = false;
    };
  }, [state?.servers]);

  useEffect(() => {
    if (!menu) return;
    const close = () => setMenu(null);
    window.addEventListener("click", close);
    return () => window.removeEventListener("click", close);
  }, [menu]);

  if (!state || !community) {
    return (
      <div className="page">
        <div className="empty">正在加载启动器状态…</div>
      </div>
    );
  }

  const instances = state.instances.filter((item) => item.name.length > 0 && !item.isPlaceholder);
  const current =
    instances.find((item) => item.name === state.selectedInstance) ??
    instances.find((item) => item.name === state.selectedVersion);
  const launchTarget = current ? `${current.displayName} · ${current.minecraft} · ${current.loader}` : "请选择实例";
  const launchSubtitle = current
    ? `${current.name} · ${state.autoMemory ? "自动内存" : `${state.ram} MB`}`
    : "未选择实例";
  const firstAnnouncement = community.announcements[0];
  const quote = community.quotes[quoteIndex];

  const refresh = async () => {
    const next = await launcher.requestState();
    setState(next);
    setStatus("状态已刷新。");
  };

  const selectInstance = async (name: string) => {
    const result = await launcher.selectInstance(name);
    setState((previous) =>
      previous ? { ...previous, selectedInstance: name, selectedVersion: name } : previous,
    );
    setStatus(result.message);
  };

  const launch = async () => {
    if (!current) {
      setStatus("请先安装或选择实例。");
      return;
    }
    setStatus("正在准备启动…");
    const result = await launcher.launch(current.name);
    setStatus(result.message);
  };

  const openServerDialog = (server?: SavedServerInfo, message?: string) => {
    setDialog({
      editing: server ?? null,
      draft: server ? { name: server.name, address: server.address, instance: server.instance } : { ...EMPTY_DRAFT },
      status: message ?? "",
    });
  };

  const saveServer = async () => {
    if (!dialog) return;
    const address = dialog.draft.address.trim();
    if (address.length === 0) {
      setDialog({ ...dialog, status: "请填写服务器网页。" });
      return;
    }
    setDialog({ ...dialog, status: "正在保存…" });
    const result = await launcher.saveServer(
      dialog.draft.name.trim(),
      address,
      dialog.editing?.address ?? "",
      dialog.draft.instance,
    );
    if (!result.ok) {
      setDialog({ ...dialog, status: result.message });
      return;
    }
    setDialog(null);
    await refresh();
    setStatus(result.message);
  };

  const deleteServer = async (server: SavedServerInfo) => {
    const result = await launcher.deleteServer(server.address);
    setState((previous) =>
      previous ? { ...previous, servers: previous.servers.filter((item) => item.address !== server.address) } : previous,
    );
    setPings((previous) => {
      const next = { ...previous };
      delete next[server.address];
      return next;
    });
    setStatus(result.message);
  };

  const queryServer = async (server: SavedServerInfo) => {
    const info = await launcher.pingServer(server.address);
    setPings((previous) => ({ ...previous, [info.id]: info }));
    setStatus("服务器状态已刷新。");
  };

  const openServer = async (server: SavedServerInfo) => {
    if (server.instance.length === 0) {
      openServerDialog(server, "先给这台服务器绑定实例，保存后就能一键进服。");
      return;
    }
    setStatus(`正在用 ${server.instance} 打开 ${server.address}…`);
    const result = await launcher.launchToServer(server.instance, server.address);
    setStatus(result.message);
  };

  const favorite = async (item: RecommendationInfo) => {
    setMenu(null);
    const address = item.url.trim();
    if (address.length === 0) {
      setStatus("这条推荐没有附带服务器地址，无法收藏。");
      return;
    }
    if (state.servers.some((server) => server.address.toLowerCase() === address.toLowerCase())) {
      setStatus(`「${item.name.length > 0 ? item.name : address}」已经在收藏里了。`);
      return;
    }
    const result = await launcher.saveServer(item.name.trim(), address, "");
    await refresh();
    setStatus(result.message);
  };

  return (
    <>
      <div className={`home ${dialog ? "blurred" : ""}`}>
        <aside className="card home-account">
          <div className="stack" style={{ gap: 8 }}>
            <h2 className="h2">账号</h2>
            <div className="avatar-wrap">
              <span className="avatar-shadow" />
              <button
                type="button"
                className="btn avatar"
                title="查看或更换头像"
                aria-label="查看或更换头像"
                onClick={openAccount}
              >
                <img className="avatar-image" src={avatar?.image} alt={state.player} />
              </button>
            </div>
            <p className="account-name">{state.player.length > 0 ? state.player : "未登录"}</p>
            <p className="muted" style={{ textAlign: "center" }}>
              {avatar?.label ?? ""}
            </p>
            <button type="button" className="btn soft" onClick={openAccount}>
              账号与皮肤
            </button>

            <span className="field-label" style={{ marginTop: 6 }}>
              当前实例
            </span>
            <select
              className="select"
              value={current?.name ?? ""}
              onChange={(event) => void selectInstance(event.target.value)}
            >
              {instances.length === 0 ? <option value="">暂无实例</option> : null}
              {instances.map((item) => (
                <option key={item.name} value={item.name}>
                  {item.displayName} · {item.loader} · {item.minecraft}
                </option>
              ))}
            </select>
            <p className="muted">{launchTarget}</p>
            <button type="button" className="btn link" style={{ alignSelf: "center" }} onClick={() => void refresh()}>
              刷新状态
            </button>
          </div>

          <button type="button" className="btn launch" onClick={() => void launch()} disabled={!current}>
            <span className="stack" style={{ gap: 2, alignItems: "flex-start" }}>
              <span className="launch-title">▶ 启动游戏</span>
              <span className="launch-subtitle">{launchSubtitle}</span>
            </span>
          </button>
        </aside>

        <div className="home-feed">
          <section className="card stack" style={{ gap: 8 }}>
            <div className="row" style={{ gap: 8 }}>
              <h2 className="h2">正在开设的服务器</h2>
              <span className="spacer" />
              <span className="muted">
                {state.servers.length > 0
                  ? `${state.servers.filter((server) => pings[server.address]?.online).length} 在线 · ${
                      state.servers.filter((server) => !pings[server.address]?.online).length
                    } 离线`
                  : ""}
              </span>
              <button type="button" className="btn link" onClick={() => openServerDialog()}>
                添加服务器
              </button>
            </div>

            {state.servers.length === 0 ? (
              <div className="empty">
                {"还没有收藏服务器。\n点右上角「添加服务器」，填好网页与实例后就能在这里一键进服。"}
              </div>
            ) : (
              <div className="server-grid">
                {state.servers.map((server) => {
                  const ping = pings[server.address];
                  return (
                    <article className="server-card" key={server.address}>
                      <div className="server-head">
                        <span className={`server-icon ${ping?.online ? "online" : ""}`}>▣</span>
                        <div className="stack" style={{ gap: 2 }}>
                          <span className="server-name">{server.name.length > 0 ? server.name : server.address}</span>
                          <span className="muted">{pingText(ping)}</span>
                        </div>
                      </div>
                      <span className="muted">{server.address}</span>
                      <span className="muted">{server.instance.length > 0 ? `实例 · ${server.instance}` : "未绑定实例"}</span>
                      <span className="muted">{ping ? (ping.online ? ping.motd : "无法连接") : "等待查询"}</span>
                      <div className="server-actions">
                        <button type="button" className="btn link" onClick={() => void openServer(server)}>
                          打开
                        </button>
                        <button type="button" className="btn link" onClick={() => void queryServer(server)}>
                          查询
                        </button>
                        <button type="button" className="btn link" onClick={() => openServerDialog(server)}>
                          编辑
                        </button>
                        <button type="button" className="btn link" onClick={() => void deleteServer(server)}>
                          删除
                        </button>
                      </div>
                    </article>
                  );
                })}
              </div>
            )}
          </section>

          <section className="card stack" style={{ gap: 8 }}>
            <h2 className="h2">群公告</h2>
            {firstAnnouncement ? (
              <>
                <p className="announcement-title">
                  {firstAnnouncement.tag.length > 0
                    ? `[${firstAnnouncement.tag}] ${firstAnnouncement.title}`
                    : firstAnnouncement.title}
                </p>
                <p>{firstAnnouncement.body}</p>
                {firstAnnouncement.imageUrl.length > 0 ? (
                  <img className="announcement-image" src={firstAnnouncement.imageUrl} alt="" />
                ) : null}
                <p className="muted">
                  {[firstAnnouncement.author, firstAnnouncement.publishedAt?.slice(0, 10) ?? "", firstAnnouncement.imageUrl ? "含配图" : ""]
                    .filter((value) => value.length > 0)
                    .join(" · ")}
                </p>
                {firstAnnouncement.markdown.length > 0 ? (
                  <button
                    type="button"
                    className="btn link"
                    style={{ alignSelf: "flex-start" }}
                    onClick={() => setAnnouncement(firstAnnouncement)}
                  >
                    查看完整公告
                  </button>
                ) : null}
              </>
            ) : (
              <>
                <p className="announcement-title">暂无公告</p>
                <p>新的群公告会出现在这里。</p>
              </>
            )}
          </section>

          <section className="card stack" style={{ gap: 8 }}>
            <div className="row" style={{ gap: 8 }}>
              <h2 className="h2">群友推荐</h2>
              <span className="spacer" />
              <button
                type="button"
                className="btn link"
                onClick={() => void launcher.refreshCommunity().then(setCommunity)}
              >
                刷新社区内容
              </button>
            </div>
            {community.recommendations.length === 0 ? (
              <p className="muted">暂无群友推荐。</p>
            ) : (
              community.recommendations.slice(0, 5).map((item) => (
                <div
                  className="rec-row"
                  key={item.id}
                  onContextMenu={(event) => {
                    if (item.type !== "server") return;
                    event.preventDefault();
                    setMenu({ item, x: event.clientX, y: event.clientY });
                  }}
                >
                  <span className={`rec-icon ${item.type}`}>
                    {item.type === "material" ? "◆" : item.type === "shader" ? "✦" : "▣"}
                  </span>
                  <div className="stack" style={{ gap: 2 }}>
                    <span className="rec-name">{item.name}</span>
                    <span className="muted">
                      {item.type === "material" ? "材质包" : item.type === "shader" ? "光影" : "服务器"} · 由 {item.author} 推荐
                      {item.tags.length > 0 ? ` · ${item.tags.join(" / ")}` : ""}
                    </span>
                    {item.description.length > 0 ? <span className="muted">{item.description}</span> : null}
                  </div>
                </div>
              ))
            )}
          </section>

          <section className="card quote-card">
            <span className="quote-mark">“</span>
            <div className="stack" style={{ gap: 4 }}>
              <p className="quote-text">{quote ? (quote.type === "image" ? `[图片语录] ${quote.text}` : quote.text) : "暂无语录。"}</p>
              <p className="muted">{[quote?.author ?? "", quote?.source ?? ""].filter((value) => value.length > 0).join(" · ")}</p>
            </div>
            <button
              type="button"
              className="btn link"
              disabled={community.quotes.length === 0}
              onClick={() => setQuoteIndex((previous) => (previous + 1) % Math.max(1, community.quotes.length))}
            >
              换一条
            </button>
          </section>

          <p className="status">{status}</p>
        </div>
      </div>

      {dialog ? (
        <Modal
          title={dialog.editing ? "编辑服务器" : "添加服务器"}
          onClose={() => setDialog(null)}
          footer={
            <>
              <button type="button" className="btn ghost" onClick={() => setDialog(null)}>
                取消
              </button>
              <button type="button" className="btn primary" onClick={() => void saveServer()}>
                保存
              </button>
            </>
          }
        >
          <p className="muted">保存后会作为一张卡片出现在「正在开设的服务器」里。</p>
          <Field label="服务器名称" hint="可留空，默认显示地址">
            <input
              className="input"
              maxLength={60}
              placeholder="例如：MCUG 生存服"
              value={dialog.draft.name}
              onChange={(event) => setDialog({ ...dialog, draft: { ...dialog.draft, name: event.target.value } })}
              onKeyDown={(event) => {
                if (event.key === "Enter") void saveServer();
              }}
            />
          </Field>
          <Field label="网页" hint="填域名或 host:port 才能查询人数并直接进服；带 / 的网页链接只能作记录。">
            <input
              className="input"
              placeholder="play.example.com:25565"
              value={dialog.draft.address}
              onChange={(event) => setDialog({ ...dialog, draft: { ...dialog.draft, address: event.target.value } })}
              onKeyDown={(event) => {
                if (event.key === "Enter") void saveServer();
              }}
            />
          </Field>
          <Field label="打开所用的实例" hint="绑定后用卡片上的「打开」直连服务器；不绑定只能手动进入。">
            <select
              className="select"
              value={dialog.draft.instance}
              onChange={(event) => setDialog({ ...dialog, draft: { ...dialog.draft, instance: event.target.value } })}
            >
              <option value="">不绑定实例</option>
              {instances.map((item) => (
                <option key={item.name} value={item.name}>
                  {item.displayName} · {item.loader} · {item.minecraft}
                </option>
              ))}
              {dialog.draft.instance.length > 0 && !instances.some((item) => item.name === dialog.draft.instance) ? (
                <option value={dialog.draft.instance}>{dialog.draft.instance}（当前未安装）</option>
              ) : null}
            </select>
          </Field>
          {dialog.status.length > 0 ? <p className="status">{dialog.status}</p> : null}
        </Modal>
      ) : null}

      {announcement ? (
        <Modal
          title={announcement.title}
          width={640}
          onClose={() => setAnnouncement(null)}
          footer={
            <button type="button" className="btn primary" onClick={() => setAnnouncement(null)}>
              关闭
            </button>
          }
        >
          <p className="muted">
            {[announcement.tag, announcement.author, announcement.publishedAt?.slice(0, 10) ?? ""]
              .filter((value) => value.length > 0)
              .join(" · ")}
          </p>
          {announcement.imageUrl.length > 0 ? <img className="announcement-image" src={announcement.imageUrl} alt="" /> : null}
          <Markdown text={announcement.markdown.length > 0 ? announcement.markdown : announcement.body} />
        </Modal>
      ) : null}

      {menu ? (
        <div className="context-menu" style={{ left: menu.x, top: menu.y }}>
          <button type="button" onClick={() => void favorite(menu.item)}>
            收藏
          </button>
        </div>
      ) : null}
    </>
  );
}
