import { useEffect, useState } from "react";
import { launcher } from "../api/launcher";
import Markdown from "../components/Markdown";
import Modal from "../components/Modal";
import type { AnnouncementInfo, CommunityData, LauncherState } from "../types/launcher";
import "./CommunityPage.css";

/** 社区管理：管理员身份、当前公告与推荐列表维护。 */
export default function CommunityPage() {
  const [state, setState] = useState<LauncherState | null>(null);
  const [community, setCommunity] = useState<CommunityData | null>(null);
  const [adminKey, setAdminKey] = useState("");
  const [adminStatus, setAdminStatus] = useState("");
  const [manageStatus, setManageStatus] = useState("");
  const [canManage, setCanManage] = useState(false);
  const [announcement, setAnnouncement] = useState<AnnouncementInfo | null>(null);

  useEffect(() => {
    let alive = true;
    void Promise.all([launcher.getState(), launcher.getCommunity()]).then(([snapshot, data]) => {
      if (!alive) return;
      setState(snapshot);
      setCommunity(data);
      setCanManage(snapshot.hasAdminKey);
    });
    return () => {
      alive = false;
    };
  }, []);

  const verifyAdmin = async () => {
    if (adminKey.trim().length === 0) {
      setAdminStatus("请输入管理员密钥。");
      return;
    }
    setAdminStatus("正在验证…");
    const result = await launcher.verifyKey("admin", adminKey.trim());
    setAdminStatus(result.message);
    if (!result.ok) return;
    setAdminKey("");
    setCanManage(true);
    setState(await launcher.getState());
  };

  const removeRecommendation = async (index: number) => {
    if (!canManage || !community || index < 0 || index >= community.recommendations.length) return;
    setManageStatus("正在保存推荐列表…");
    const remaining = community.recommendations.filter((_, position) => position !== index);
    const result = await launcher.saveRecommendations(remaining);
    setCommunity(await launcher.getCommunity());
    setManageStatus(result.message);
  };

  if (!state || !community) {
    return (
      <div className="subpage">
        <div className="empty">正在读取社区内容…</div>
      </div>
    );
  }

  return (
    <div className="subpage">
      <div className="stack" style={{ gap: 6 }}>
        <h1 className="page-title">社区管理</h1>
        <p className="muted">公告由社区数据源提供并在此展示；管理员密钥仅用于管理推荐内容。</p>
      </div>

      <div className="page-scroll" style={{ marginTop: 10 }}>
        <div className="stack" style={{ gap: 14 }}>
          <section className="card stack" style={{ gap: 8 }}>
            <h2 className="h2">管理员身份</h2>
            <p className="muted">
              {canManage
                ? "本机已设置管理员密钥。密钥只保存在本机，不会上传。"
                : "尚未设置管理员密钥。首次输入任意 8 位以上密钥即设置本机管理员密钥。"}
            </p>
            <div className="row" style={{ gap: 8 }}>
              <input
                className="input"
                type="password"
                placeholder="首次输入即设置本机管理员密钥"
                value={adminKey}
                onChange={(event) => setAdminKey(event.target.value)}
              />
              <button type="button" className="btn ghost" onClick={() => void verifyAdmin()}>
                验证
              </button>
            </div>
            <p className="status">{adminStatus}</p>
          </section>

          <section className="card stack" style={{ gap: 8 }}>
            <h2 className="h2">当前公告</h2>
            {community.announcements.length === 0 ? (
              <p className="muted">暂无公告。</p>
            ) : (
              community.announcements.slice(0, 10).map((item) => (
                <div className="community-item" key={item.id}>
                  <span className="community-title">
                    {item.tag.length > 0 ? `[${item.tag}] ${item.title}` : item.title}
                  </span>
                  <span className="muted">{item.body}</span>
                  <span className="muted">
                    {[item.author, item.publishedAt?.slice(0, 10) ?? "", item.imageUrl.length > 0 ? "含配图" : ""]
                      .filter((value) => value.length > 0)
                      .join(" · ")}
                  </span>
                  {item.markdown.length > 0 ? (
                    <button
                      type="button"
                      className="btn link"
                      style={{ alignSelf: "flex-start" }}
                      onClick={() => setAnnouncement(item)}
                    >
                      查看完整公告
                    </button>
                  ) : null}
                </div>
              ))
            )}
          </section>

          <section className="card stack" style={{ gap: 8 }}>
            <h2 className="h2">管理推荐</h2>
            <p className="muted">删除会重写本机推荐列表，保留未删除项及其原始字段；需要管理员密钥。</p>
            {community.recommendations.length === 0 ? (
              <p className="muted">没有可管理的推荐。</p>
            ) : (
              community.recommendations.map((item, index) => (
                <div className="community-row" key={item.id}>
                  <div className="stack" style={{ gap: 2 }}>
                    <span className="community-title">{item.name}</span>
                    <span className="muted">
                      {item.type} · {item.author}
                    </span>
                  </div>
                  <button
                    type="button"
                    className="btn ghost"
                    disabled={!canManage}
                    onClick={() => void removeRecommendation(index)}
                  >
                    删除
                  </button>
                </div>
              ))
            )}
            <p className="status">{manageStatus}</p>
          </section>
        </div>
      </div>

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
          {announcement.imageUrl.length > 0 ? (
            <img className="announcement-image" src={announcement.imageUrl} alt="" />
          ) : null}
          <Markdown text={announcement.markdown.length > 0 ? announcement.markdown : announcement.body} />
        </Modal>
      ) : null}
    </div>
  );
}
