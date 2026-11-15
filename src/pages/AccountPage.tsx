import { useState } from "react";
import * as accounts from "../api/commands/accounts";
import PathPrompt from "../components/PathPrompt";
import { useLauncher } from "../state/launcher";
import type { MuaProfileInfo } from "../types/launcher";
import "./AccountPage.css";

function modeLabel(mode: string): string {
  if (mode === "microsoft") return "正版账号 · 在线验证";
  if (mode === "mua") return "MUA 账号 · 外置登录";
  return "离线账号 · 本地启动";
}

/** 账号页：账号状态由后端事件同步，操作通过命令提交。 */
export default function AccountPage() {
  const { state, avatar, refreshState, refreshAvatar } = useLauncher();
  const [status, setStatus] = useState("");
  const [muaAccount, setMuaAccount] = useState("");
  const [muaPassword, setMuaPassword] = useState("");
  const [profiles, setProfiles] = useState<MuaProfileInfo[]>([]);
  const [skinPrompt, setSkinPrompt] = useState(false);

  if (!state) {
    return (
      <div className="subpage">
        <div className="empty">正在读取账号…</div>
      </div>
    );
  }

  const activeAccount = state.accounts.find((account) => account.id === state.activeAccountId) ?? null;
  const offline = activeAccount?.kind === "offline";
  const skinHint = !offline
    ? "在线账号的皮肤来自认证站，不能在此选择本地皮肤。"
    : avatar?.fallback
      ? "正在使用默认头像；离线模式可选择本地皮肤。"
      : "本地皮肤仅对离线账号生效";

  const loginMicrosoft = async () => {
    setStatus("正在打开微软登录页…");
    const result = await accounts.loginMicrosoft();
    await Promise.all([refreshState(), refreshAvatar()]);
    setStatus(result.message);
  };

  const loginMua = async () => {
    if (muaAccount.trim().length === 0 || muaPassword.length === 0) {
      setStatus("请填写 MUA 账号和密码。");
      return;
    }
    setStatus("正在登录 MUA…");
    const result = await accounts.loginMua(muaAccount.trim(), muaPassword);
    await Promise.all([refreshState(), refreshAvatar()]);
    setStatus(result.message);
    if (result.ok) setProfiles(await accounts.listMuaProfiles());
  };

  const switchOffline = async () => {
    setStatus("正在切换到离线账号…");
    const offlineAccount = state.accounts.find((account) => account.kind === "offline");
    if (!offlineAccount) {
      setStatus("离线账号不可用。");
      return;
    }
    const result = await accounts.selectAccount(offlineAccount.id);
    await Promise.all([refreshState(), refreshAvatar()]);
    setStatus(result.message);
  };

  const forget = async (kind: "microsoft" | "mua") => {
    const account = state.accounts.find((item) => item.kind === kind);
    if (!account) {
      setStatus("本机没有此类账号。");
      return;
    }
    setStatus("正在移除本机保存的账号…");
    const result = await accounts.forgetAccount(account.id);
    await Promise.all([refreshState(), refreshAvatar()]);
    setStatus(result.message);
  };

  const applySkin = async (path: string) => {
    setStatus("正在应用本地皮肤…");
    await accounts.setOfflineSkin(path);
    await refreshAvatar();
    setStatus(path.length === 0 ? "已清除本地皮肤。" : "已应用本地皮肤。");
  };

  return (
    <div className="subpage account-scroll">
      <section className="card">
        <div className="account-grid">
          <div className="stack" style={{ gap: 8 }}>
            <h2 className="h2">当前账号</h2>
            <span className="account-player">{activeAccount?.player || "未登录"}</span>
            <span className="muted">{modeLabel(activeAccount?.kind ?? "offline")}</span>
            <span className="status">{status}</span>
            <span className="muted">{avatar?.label ?? ""}</span>
            <span className="muted">{skinHint}</span>
            <div className="row" style={{ gap: 8 }}>
              <button type="button" className="btn ghost" disabled={!offline} onClick={() => setSkinPrompt(true)}>
                选择本地皮肤
              </button>
              <button type="button" className="btn ghost" disabled={!offline} onClick={() => void applySkin("")}>
                清除本地皮肤
              </button>
            </div>
          </div>
          <div className="account-avatar">
            <span className="account-avatar-shadow" />
            <img src={avatar?.image} alt={activeAccount?.player ?? "未登录"} />
          </div>
        </div>
      </section>

      <section className="card stack" style={{ gap: 8 }}>
        <h2 className="h2">账号模式</h2>
        <div className="account-actions">
          <button type="button" className="btn primary" onClick={() => void loginMicrosoft()}>
            登录正版账号
          </button>
          <button type="button" className="btn ghost" onClick={() => void switchOffline()}>
            切换到离线
          </button>
          <button type="button" className="btn ghost" onClick={() => void forget("microsoft")}>
            忘记正版账号
          </button>
          <button type="button" className="btn ghost" onClick={() => void forget("mua")}>
            忘记 MUA 账号
          </button>
        </div>
        <p className="muted">离线玩家名在「设置 → 启动设置」中修改；关闭启动器不会退出已启动的游戏。</p>
      </section>

      <section className="card stack" style={{ gap: 8 }}>
        <h2 className="h2">MUA 账号</h2>
        <p className="muted">使用外置登录账号；存在多个角色时会显示角色选择。</p>
        <input
          className="input"
          placeholder="MUA 账号"
          value={muaAccount}
          onChange={(event) => setMuaAccount(event.target.value)}
        />
        <input
          className="input"
          type="password"
          placeholder="密码"
          value={muaPassword}
          onChange={(event) => setMuaPassword(event.target.value)}
        />
        <button
          type="button"
          className="btn primary"
          style={{ alignSelf: "flex-start" }}
          onClick={() => void loginMua()}
        >
          登录 MUA
        </button>
      </section>

      {profiles.length > 0 ? (
        <section className="card stack" style={{ gap: 8 }}>
          <h2 className="h2">选择 MUA 角色</h2>
          <div className="stack" style={{ gap: 6 }}>
            {profiles.map((profile) => (
              <button
                key={profile.id}
                type="button"
                className="btn ghost"
                style={{ alignSelf: "flex-start" }}
                onClick={() => {
                  setProfiles([]);
                  setStatus(`已选择角色 ${profile.name}，正在完成登录…`);
                  void accounts.selectMuaProfile("token", profile.id).then(async (result) => {
                    await refreshState();
                    setStatus(result.message);
                  });
                }}
              >
                {profile.name}
              </button>
            ))}
          </div>
        </section>
      ) : null}

      {skinPrompt ? (
        <PathPrompt
          title="选择本地皮肤"
          label="PNG 皮肤路径"
          initial="D:/Skins/skin.png"
          hint="仅离线账号生效。"
          onCancel={() => setSkinPrompt(false)}
          onConfirm={(path) => {
            setSkinPrompt(false);
            void applySkin(path);
          }}
        />
      ) : null}
    </div>
  );
}
