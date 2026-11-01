// Page rendering only; native operations are handled by feature modules.
function microsoftLoginModal() {
  return `<div class="overlay"><section class="modal" role="dialog" aria-modal="true" aria-labelledby="ms-title">
    <div class="modal-head"><div class="modal-icon">${icon('users')}</div><div><div class="modal-title" id="ms-title">正版账号登录</div><div class="modal-sub">使用 Microsoft 账号完成正版验证，登录后可启动正版实例。</div></div><a class="modal-close" href="?screen=home" aria-label="关闭">${icon('x')}</a></div>
    <div class="modal-body">
      <div class="inline-note">${icon('info')} 点击下方按钮后，会打开独立的 Microsoft 登录窗口；完成授权后会自动返回启动器。</div>
      <div class="modal-status green" style="margin-top:14px">${icon('check')}<div><b>启动器不会保存你的密码</b><br>只保存用于启动游戏的授权令牌；切到离线会保留账号，可在账号设置中移除。</div></div>
    </div>
    <div class="modal-foot"><a class="btn btn-ghost" href="?screen=home">取消</a><span class="spacer"></span><button class="btn btn-primary" type="button" data-ms-login>${icon('external')} 打开登录窗口</button></div>
  </section></div>`;
}

function skinModal() {
  if (window.chrome?.webview)
    return `<div class="overlay"><section class="modal" role="dialog" aria-modal="true"><div class="modal-head"><h2>头像与皮肤</h2><a class="modal-close" href="?screen=home" aria-label="关闭">×</a></div><div class="modal-body"><div style="width:96px;height:96px" data-avatar-preview>${Avatar()}</div><h3>${escapeHtml(window.state?.player || '未登录')}</h3><p data-avatar-label>${escapeHtml(window.state?.avatarLabel || '默认头像')}</p><p>在线账号读取正版或 MUA 皮肤；离线可选择 PNG 作为启动器头像，不会上传或修改游戏内皮肤。</p>${window.state?.accountType === 'offline' ? '<button class="btn btn-primary" data-pick-offline-skin>选择本地 PNG 皮肤</button><button class="btn btn-ghost" data-default-skin>恢复默认头像</button>' : ''}<button class="btn btn-ghost" data-refresh-avatar>重新读取皮肤</button><p>披风上传与替换暂未接入。</p></div></section></div>`;

  const verified = Q.skin === 'verified';
  return `<div class="overlay"><section class="modal skin-modal" role="dialog" aria-modal="true" aria-labelledby="skin-title">
    <div class="modal-head"><div class="modal-icon">${icon('shirt')}</div><div><div class="modal-title" id="skin-title">皮肤与披风</div><div class="modal-sub">只改变角色外观，不影响账号安全与游戏启动。</div></div><a class="modal-close" href="?screen=home" aria-label="关闭">${icon('x')}</a></div>
    <div class="modal-body skin-body">
      <div class="skin-hero">
        <div class="skin-figure">${SkinFigure()}</div>
        <div class="skin-hero-copy"><b>waibibabu1998</b><span>正版账号 · 当前使用本地皮肤</span><div class="skin-tags"><span class="tag green">经典皮肤</span><span class="tag">Alex 模型</span></div></div>
      </div>
      <div class="skin-section">
        <div class="skin-section-head"><div class="skin-section-icon">${icon('image')}</div><div><b>当前皮肤</b><span>本地皮肤只保存在这台电脑，不会自动上传。</span></div></div>
        <div class="skin-actions"><button class="btn btn-soft btn-sm">${icon('image')} 更换皮肤文件</button><button class="btn btn-ghost btn-sm">${icon('shield')} 使用正版皮肤</button><span class="skin-file">waibibabu1998.png · 64×64</span></div>
      </div>
      <div class="skin-section">
        <div class="skin-section-head"><div class="skin-section-icon">${icon('key')}</div><div><b>皮肤站认证</b><span>${verified ? '已连接 LittleSkin，可同步皮肤与披风。' : '认证后可同步皮肤站里的皮肤与披风；不认证也能使用本地皮肤。'}</span></div><span class="skin-state ${verified ? 'ok' : ''}">${verified ? '已认证' : '未认证'}</span></div>
        <div class="skin-station"><div class="skin-station-id"><b>${verified ? 'LittleSkin · 阿澈' : 'LittleSkin'}</b><span>${verified ? '上次同步 2 分钟前 · 自动同步已开启' : '支持 Blessing Skin 系皮肤站'}</span></div><button class="btn btn-ghost btn-sm">${verified ? '更换绑定' : '认证皮肤站'}</button></div>
      </div>
      <div class="skin-section">
        <div class="skin-section-head"><div class="skin-section-icon">${icon('cape')}</div><div><b>披风</b><span>仅在支持披风显示的服务器中可见。</span></div></div>
        <div class="cape-options">
          <label class="cape-option"><span class="cape-swatch none">${icon('x')}</span><span>不显示</span></label>
          <label class="cape-option selected"><span class="cape-swatch custom"></span><span>自定义披风</span><span class="cape-check">${icon('check')}</span></label>
          <label class="cape-option"><span class="cape-swatch mojang"></span><span>Mojang 披风</span><span class="skin-lock">正版账号</span></label>
        </div>
      </div>
    </div>
    <div class="modal-foot"><a class="btn btn-ghost" href="?screen=home">取消</a><span class="spacer"></span><button class="btn btn-primary">${icon('check')} 保存更改</button></div>
  </section></div>`;
}

/* ---------------- shell ---------------- */

function muaLoginModal() {
  return `<div class="overlay"><section class="modal" role="dialog" aria-modal="true" aria-labelledby="mua-title">
    <div class="modal-head"><div class="modal-icon">${icon('key')}</div><div><div class="modal-title" id="mua-title">MUA 登录</div><div class="modal-sub">使用 LittleSkin / MUA 账号密码登录，仅用于外置登录。</div></div><a class="modal-close" href="?screen=home" aria-label="关闭">${icon('x')}</a></div>
    <div class="modal-body">
      <div class="field"><div class="field-label">MUA 账号</div><input class="input" id="mua-account" autocomplete="username" placeholder="邮箱或用户名"></div>
      <div class="field"><div class="field-label">密码</div><input class="input" id="mua-password" type="password" autocomplete="current-password" placeholder="MUA 密码"></div>
      <div class="modal-status info">${icon('info')}<div>登录后启动器会用 authlib-injector 连接 MUA 皮肤站；密码只发送给 skin.mualliance.ltd。</div></div>
    </div>
    <div class="modal-foot"><a class="btn btn-ghost" href="?screen=home">取消</a><span class="spacer"></span><button class="btn btn-primary" data-mua-submit>登录 MUA</button></div>
  </section></div>`;
}
