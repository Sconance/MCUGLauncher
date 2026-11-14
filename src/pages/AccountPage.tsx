import { useEffect, useState } from "react";
import { launcher } from "../api/launcher";
import PathPrompt from "../components/PathPrompt";
import type { AvatarInfo, LauncherState, MuaProfileInfo } from "../types/launcher";
import "./AccountPage.css";

function modeLabel(mode: string): string {
  if (mode === "microsoft") return "正版账号 · 在线验证";
  if (mode === "mua") return "MUA 账号 · 外置登录";
  return "离线账号 · 本地启动";
}

/** 账号页：当前账号、模式切换、MUA 登录与角色选择、本地皮肤。 */
export default function AccountPage() {
  const [state, setState] = useState<LauncherState | null>(null);
  const [avatar, setAvatar] = useState<AvatarInfo | null>(null);
  const [status, setStatus] = useState("");
  const [skinHint, setSkinHint] = useState("");
  const [muaAccount, setMuaAccount] = useState("");
  const [muaPassword, setMuaPassword] = useState("");
  const [profiles, setProfiles] = useState<MuaProfileInfo[]>([]);
  const [skinPrompt, setSkinPrompt] = useState(false);

  useEffect(() => {
    let alive = true;
    void Promise.all([launcher.getState(), launcher.readAvatar()]).then(([snapshot, avatarInfo]) => {
      if (!alive) return;
      setState(snapshot);
      setAvatar(avatarInfo);
      setSkinHint(
        snapshot.accountType === "offline"
          ? "本地皮肤仅对离线账号生效；选择 PNG 后立即生效。"
          : "在线账号的皮肤来自认证站，不能在此选择本地皮肤。",
      );
    });
    return () => {
      alive = false;
    };
  }, []);

  const refresh = async () => {
    const [snapshot, avatarInfo] = await Promise.all([launcher.getState(), launcher.readAvatar()]);
    setState(snapshot);
    setAvatar(avatarInfo);
    return snapshot;
  };

  const applyAuth = async (ok: boolean, message: string) => {
    setStatus(message);
    if (!ok) return;
    await refresh();
  };

  const loginMicrosoft = async () => {
    setStatus("正在打开微软登录页…");
    const result = await launcher.loginMicrosoft();
    await applyAuth(result.ok, result.message);
  };

  const loginMua = async () => {
    if (muaAccount.trim().length === 0 || muaPassword.length === 0) {
      setStatus("请填写 MUA 账号和密码。");
      return;
    }
    setStatus("正在登录 MUA…");
    const result = await launcher.loginMua(muaAccount.trim(), muaPassword);
    await applyAuth(result.ok, result.message);
    if (result.ok) setProfiles(await launcher.listMuaProfiles());
  };

  const switchOffline = async () => {
    setStatus("正在切换到离线账号…");
    const result = await launcher.switchAccountMode("offline");
    setStatus(result.message);
    if (!result.needsLogin) await refresh();
  };

  const forget = async (mode: string) => {
    setStatus("正在移除本机保存的账号…");
    const result = await launcher.forgetAccount(mode);
    await applyAuth(result.ok, result.message);
  };

  const applySkin = async (path: string) => {
    setStatus("正在应用本地皮肤…");
    const info = await launcher.setOfflineSkin(path);
    setAvatar(info);
    setSkinHint(path.length === 0 ? "正在使用默认头像；离线模式可选择本地皮肤。" : "本地皮肤仅对离线账号生效");
  };

  if (!state) {
    return (
      <div className="subpage">
        <div className="empty">正在读取账号…</div>
      </div>
    );
  }

  const offline = state.accountType === "offline";

  return (
    <div className="subpage account-scroll">
      <section className="card">
        <div className="account-grid">
          <div className="stack" style={{ gap: 8 }}>
            <h2 className="h2">当前账号</h2>
            <span className="account-player">{state.player.length > 0 ? state.player : "未登录"}</span>
            <span className="muted">{modeLabel(state.accountType)}</span>
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
            <img src={avatar?.image} alt={state.player} />
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
                  void launcher.selectMuaProfile("token", profile.id).then(async (result) => {
                    await refresh();
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
