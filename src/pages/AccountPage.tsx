import { useState } from "react";
import navAccount from "../assets/icons/nav/nav-account.svg";
import navMicrosoft from "../assets/icons/nav/nav-microsoft.svg";
import navMua from "../assets/icons/nav/nav-mua.svg";
import navOffline from "../assets/icons/nav/nav-offline.svg";
import * as accounts from "../api/commands/accounts";
import { AppInput } from "../components/AppInput";
import Modal from "../components/Modal";
import PathPrompt from "../components/PathPrompt";
import SplitPage from "../components/SplitPage";
import type { NavItem } from "../components/SectionNav";
import { useLauncher } from "../state/launcher";
import type { AccountInfo } from "../types/launcher";
import "./AccountPage.css";

type AccountSource = "all" | "microsoft" | "offline" | "mua";
type AccountView = "grid" | "list";

const SOURCES: { key: AccountSource; label: string; icon: string }[] = [
  { key: "all", label: "全部来源", icon: navAccount },
  { key: "offline", label: "离线模式", icon: navOffline },
  { key: "microsoft", label: "微软账户", icon: navMicrosoft },
  { key: "mua", label: "MUA 账户", icon: navMua },
];

const NAV_GROUPS: NavItem[][] = [
  SOURCES.map(({ key, label, icon }) => ({ key, label, icon })),
];

function isAccountSource(value: string): value is AccountSource {
  return SOURCES.some((source) => source.key === value);
}

function sourceLabel(source: AccountSource): string {
  return SOURCES.find((item) => item.key === source)?.label ?? "全部来源";
}

function modeLabel(mode: string): string {
  if (mode === "microsoft") return "正版账号 · 在线验证";
  if (mode === "mua") return "MUA 账号 · 外置登录";
  return "离线账号 · 本地启动";
}

function errorText(error: unknown): string {
  return error instanceof Error ? error.message : String(error);
}

/** 账户页：左侧按来源筛选，右侧以网格/列表管理本机保存的账号。 */
export default function AccountPage() {
  const { overview, accounts: accountList, avatar, refreshState, refreshAvatar } = useLauncher();
  const [source, setSource] = useState<AccountSource>("all");
  const [view, setView] = useState<AccountView>("grid");
  const [status, setStatus] = useState("");
  const [addOpen, setAddOpen] = useState(false);
  const [muaAccount, setMuaAccount] = useState("");
  const [muaPassword, setMuaPassword] = useState("");
  const [skinPrompt, setSkinPrompt] = useState(false);

  if (!overview || !accountList) {
    return (
      <div className="subpage">
        <div className="empty">正在读取账号…</div>
      </div>
    );
  }

  const activeAccount = accountList.find((account) => account.id === overview.activeAccountId) ?? null;
  const offline = activeAccount?.kind === "offline";
  const skinHint = !offline
    ? "在线账号的皮肤来自认证站，不能在此选择本地皮肤。"
    : avatar?.fallback
      ? "正在使用默认头像；离线模式可选择本地皮肤。"
      : "本地皮肤仅对离线账号生效";

  const openAdd = () => {
    setStatus("");
    setAddOpen(true);
  };

  const selectAccount = async (account: AccountInfo): Promise<boolean> => {
    setStatus(`正在切换到 ${account.player || "该账号"}…`);
    try {
      const result = await accounts.selectAccount(account.id);
      await Promise.all([refreshState(), refreshAvatar()]);
      setStatus(result.message);
      return true;
    } catch (error) {
      setStatus(errorText(error));
      return false;
    }
  };

  const forget = async (account: AccountInfo) => {
    setStatus(`正在移除 ${account.player || "该账号"}…`);
    try {
      const result = await accounts.forgetAccount(account.id);
      await Promise.all([refreshState(), refreshAvatar()]);
      setStatus(result.message);
    } catch (error) {
      setStatus(errorText(error));
    }
  };

  const loginMicrosoft = async (close: () => void) => {
    setStatus("正在打开微软登录页…");
    try {
      await accounts.startLogin("microsoft");
      setStatus("登录会话已创建。");
      setSource("microsoft");
      close();
    } catch (error) {
      setStatus(errorText(error));
    }
  };

  const loginMua = async (close: () => void) => {
    if (muaAccount.trim().length === 0 || muaPassword.length === 0) {
      setStatus("请填写 MUA 账号和密码。");
      return;
    }
    setStatus("正在登录 MUA…");
    try {
      await accounts.startLogin("mua", muaAccount.trim(), muaPassword);
      setStatus("登录会话已创建。");
      setSource("mua");
      close();
    } catch (error) {
      setStatus(errorText(error));
    }
  };

  const switchOffline = async (close: () => void) => {
    const offlineAccount = accountList.find((account) => account.kind === "offline");
    if (!offlineAccount) {
      setStatus("离线账号不可用。");
      return;
    }
    if (await selectAccount(offlineAccount)) {
      setSource("offline");
      close();
    }
  };

  const applySkin = async (path: string) => {
    setStatus("正在应用本地皮肤…");
    try {
      await accounts.setOfflineSkin(path);
      await refreshAvatar();
      setStatus(path.length === 0 ? "已清除本地皮肤。" : "已应用本地皮肤。");
    } catch (error) {
      setStatus(errorText(error));
    }
  };

  const currentSource = source;

  return (
    <>
      <SplitPage
        title="账户"
        navLabel="账户来源"
        groups={NAV_GROUPS}
        active={source}
        onSelect={(key) => {
          if (isAccountSource(key)) setSource(key);
        }}
        contentClassName="account-scroll"
      >
        {(shown) => {
          const shownSource = isAccountSource(shown) ? shown : currentSource;
          const list = accountList.filter((account) => shownSource === "all" || account.kind === shownSource);
          const empty = shownSource === "all" ? "还没有保存的账号。" : "这个分类下还没有账号。";
          return (
            <div className="account-page">
              <header className="account-head">
                <div className="account-head-copy">
                  <h2 className="h2">{sourceLabel(shownSource)}</h2>
                  <span className="muted">{list.length} 个账号</span>
                </div>
                <div className="account-head-actions">
                  <div className="account-view-toggle" role="group" aria-label="显示方式">
                    <button
                      type="button"
                      className={view === "grid" ? "is-active" : ""}
                      aria-pressed={view === "grid"}
                      onClick={() => setView("grid")}
                    >
                      网格
                    </button>
                    <button
                      type="button"
                      className={view === "list" ? "is-active" : ""}
                      aria-pressed={view === "list"}
                      onClick={() => setView("list")}
                    >
                      列表
                    </button>
                  </div>
                  <button type="button" className="btn primary" onClick={openAdd}>
                    + 添加账号
                  </button>
                </div>
              </header>

              {status ? <p className="status account-status">{status}</p> : null}

              {list.length === 0 ? (
                <div className="empty account-empty">
                  <span>{empty}</span>
                  <button type="button" className="btn ghost" onClick={openAdd}>
                    添加账号
                  </button>
                </div>
              ) : (
                <div className={`account-list is-${view}`}>
                  {list.map((account) => {
                    const isActive = account.id === activeAccount?.id;
                    return (
                      <article key={account.id} className={`card account-card${isActive ? " is-active" : ""}`}>
                        <div className="account-card-top">
                          <img
                            className="account-card-avatar"
                            src={isActive ? (avatar?.image ?? "/MCUGLogo.svg") : "/MCUGLogo.svg"}
                            alt=""
                          />
                          {isActive ? <span className="account-card-current">当前</span> : null}
                        </div>
                        <div className="account-card-info">
                          <span className="account-card-name">{account.player || "未命名"}</span>
                          <span className="muted">{modeLabel(account.kind)}</span>
                        </div>
                        <div className="account-card-actions">
                          {!isActive ? (
                            <button type="button" className="btn ghost" onClick={() => void selectAccount(account)}>
                              使用
                            </button>
                          ) : null}
                          {account.kind !== "offline" ? (
                            <button type="button" className="btn ghost" onClick={() => void forget(account)}>
                              忘记
                            </button>
                          ) : null}
                          {isActive && account.kind === "offline" ? (
                            <>
                              <button type="button" className="btn ghost" onClick={() => setSkinPrompt(true)}>
                                选择皮肤
                              </button>
                              <button type="button" className="btn ghost" onClick={() => void applySkin("")}>
                                清除皮肤
                              </button>
                            </>
                          ) : null}
                        </div>
                      </article>
                    );
                  })}
                </div>
              )}
            </div>
          );
        }}
      </SplitPage>

      {addOpen ? (
        <Modal
          title="添加账号"
          width={480}
          onClose={() => setAddOpen(false)}
          footer={(close) => (
            <button type="button" className="btn ghost" onClick={() => close()}>
              关闭
            </button>
          )}
        >
          {(close) => (
            <div className="account-add-form">
              <section className="account-add-section">
                <h3 className="account-add-title">微软账户</h3>
                <p className="muted">在浏览器中完成微软登录，返回后账号会出现在列表中。</p>
                <div className="account-add-actions">
                  <button type="button" className="btn primary" onClick={() => void loginMicrosoft(close)}>
                    登录微软账号
                  </button>
                </div>
              </section>
              <section className="account-add-section">
                <h3 className="account-add-title">离线模式</h3>
                <p className="muted">离线账号使用「设置 → 启动设置」中的玩家名，不依赖网络。</p>
                <div className="account-add-actions">
                  <button type="button" className="btn ghost" onClick={() => void switchOffline(close)}>
                    切换到离线账号
                  </button>
                </div>
              </section>
              <section className="account-add-section">
                <h3 className="account-add-title">MUA 账户</h3>
                <p className="muted">使用外置登录账号；存在多个角色时会显示角色选择。</p>
                <AppInput
                  placeholder="MUA 账号"
                  value={muaAccount}
                  onChange={(event) => setMuaAccount(event.target.value)}
                />
                <AppInput
                  type="password"
                  placeholder="密码"
                  value={muaPassword}
                  onChange={(event) => setMuaPassword(event.target.value)}
                />
                <div className="account-add-actions">
                  <button type="button" className="btn primary" onClick={() => void loginMua(close)}>
                    登录 MUA
                  </button>
                </div>
              </section>
              {status ? <p className="status">{status}</p> : null}
            </div>
          )}
        </Modal>
      ) : null}

      {skinPrompt ? (
        <PathPrompt
          title="选择本地皮肤"
          label="PNG 皮肤路径"
          initial="D:/Skins/skin.png"
          hint={skinHint}
          onCancel={() => setSkinPrompt(false)}
          onConfirm={(path) => {
            setSkinPrompt(false);
            void applySkin(path);
          }}
        />
      ) : null}
    </>
  );
}
