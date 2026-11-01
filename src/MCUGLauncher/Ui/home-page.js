// Page rendering only; native operations are handled by feature modules.
function accountCard() {
  return `<section class="card account-card">
    <div class="acc-tabs" role="tablist" aria-label="账号类型">
      <button class="acc-tab active" data-account="microsoft">正版</button>
      <button class="acc-tab" data-account="offline">离线</button>
      <button class="acc-tab" data-account="mua">MUA 登录</button>
    </div>
    <a class="acc-avatar-wrap" href="?screen=home&modal=skin" title="皮肤与披风" aria-label="打开皮肤与披风">
      <div class="acc-avatar">${Avatar()}</div>
      <span class="avatar-overlay">${icon('shirt')} 皮肤与披风</span>
    </a>
    <div class="acc-name">waibibabu1998</div>
    <div class="acc-meta">${icon('shield')} 正版账号 · 启动时验证</div>
    <button class="btn btn-soft acc-btn">切换微软账号</button>
    <a class="acc-link" href="?screen=options">账号设置</a>
    <div class="acc-foot"><span>账号保存在本机</span><strong>已保存</strong></div>
  </section>`;
}

function launchPanel(expanded = false) {
  return `<div class="launch-wrap">
    <div class="launch-row">
      <button class="launch-btn" aria-label="启动 1.20.1-OptiFine_I6">
        <span class="launch-glyph">${icon('play')}</span>
        <span class="launch-text"><b>启动游戏</b><small>1.20.1-OptiFine_I6 · 4096 MB</small></span>
      </button>
      <div class="launch-side">
        <a class="launch-mini ${expanded ? 'open' : ''}" href="${expanded ? '?screen=home' : '?screen=home&panel=versions'}" aria-label="${expanded ? '收起实例列表' : '选择实例'}" title="${expanded ? '收起实例列表' : '选择实例'}">${icon(expanded ? 'chevron-left' : 'chevron-right')}</a>
        <a class="launch-mini" href="?screen=manage" aria-label="版本内容管理" title="版本内容管理">${icon('settings')}</a>
      </div>
    </div>
  </div>`;
}

function instancePanel() {
  const groups = versionGroups
    .map(
      (g, gi) => `<section class="version-group">
    <button type="button" class="version-group-head" aria-expanded="true"><span class="version-caret">${icon('chevron-down')}</span><b>${g.label}</b><span class="version-count">(${g.count})</span></button>
    <div class="version-list">${g.versions
      .map(
        (v, i) => `<div class="version-row ${v[0] === Q.inst ? 'selected' : ''}">
      <div class="version-copy"><b>${v[0]}</b><span>${v[1]}</span></div>
      <button class="version-icon" aria-label="启动 ${v[0]}">${icon('play')}</button>
      <a class="version-icon" href="?screen=version&v=${encodeURIComponent(v[0])}" aria-label="版本设置 ${v[0]}">${icon('settings')}</a>
    </div>`
      )
      .join('')}</div>
  </section>`
    )
    .join('');
  return `<section class="card version-panel">
    <div class="version-head">
      <div><div class="version-title">游戏目录</div><div class="version-sub">选择实例后可直接启动，或进入版本设置。</div></div>
      <div class="version-head-actions"><button class="btn btn-ghost btn-sm" data-home-import>${icon('download')} 导入整合包</button><button class="btn btn-ghost btn-sm" data-refresh-instances>${icon('refresh')} 刷新版本</button><a class="version-close" href="?screen=home" aria-label="收起游戏目录">${icon('chevron-left')} 收起</a></div>
    </div>
    <div class="version-path-row"><select class="input version-path" data-game-root aria-label="游戏目录">${[...new Set([state.root, ...(state.gameRoots || [])].filter(Boolean))].map((root) => `<option value="${escapeHtml(root)}" ${root === state.root ? 'selected' : ''}>${escapeHtml(root)}</option>`).join('') || '<option value="">请添加游戏目录</option>'}</select><button class="version-add" data-add-root aria-label="添加游戏目录">${icon('plus')}</button></div>
    <div class="version-search">${icon('search')}<input placeholder="点此处搜索实例"><span class="search-hint">按名称、版本或加载器筛选</span></div>
    <div class="version-groups">${groups}</div>
  </section>`;
}

function serverTile(s) {
  const pingClass = !s.online ? 'bad' : s.ping < 45 ? 'good' : 'mid';
  return `<article data-server-id="${s.id}" class="server-tile ${s.online ? '' : 'offline'}">
    <div class="server-top">
      <div class="server-icon ${tileClass(s.color)}">${icon(s.icon)}</div>
      <div class="flex1">
        <div class="server-name">${s.name}</div>
        <div class="server-sub"><span class="dot ${s.online ? 'online' : 'offline'}"></span>${s.online ? '在线' : '离线'} · ${s.version}</div>
      </div>
    </div>
    <button class="server-more" data-server-menu="${s.id}" aria-label="服务器操作">${icon('more')}</button>
    <div class="server-motd">${s.motd}</div>
    <div class="server-bottom">
      <span class="server-stat">${icon('users')}${s.online ? `<b>${s.players}</b> / ${s.max}` : '<span class="offline-label">上次在线人数未知</span>'}</span>
      <span class="server-ping ${pingClass}">${s.online ? `${s.ping} ms` : '离线'}</span>
    </div>
    ${
      Q.menu === s.id
        ? `<div class="mini-menu">
      <a href="?screen=home&modal=edit-server&sid=${encodeURIComponent(s.id)}">${icon('edit')} 编辑服务器</a>
      <button class="danger" data-delete-server="${s.id}">${icon('trash')} 移除</button>
    </div>`
        : ''
    }
  </article>`;
}

function serverAddTile() {
  return `<a class="server-add" href="?screen=home&modal=add-server">
    <span class="add-circle">${icon('plus')}</span>
    <span>添加服务器</span>
  </a>`;
}

function serverPanel(mode = 'normal') {
  const onlineCount = servers.filter((s) => s.online).length;
  const status =
    mode === 'loading'
      ? '读取中…'
      : mode === 'error'
        ? '状态不可用'
        : mode === 'empty' || servers.length === 0
          ? '0 台服务器'
          : `${onlineCount} 在线 · ${servers.length - onlineCount} 离线`;
  const head = `<div class="card-head">
    <span class="card-title">${icon('server')} 正在开设的服务器</span>
    <span class="server-status">${status}</span>
    <div class="rail-tools"><button class="rail-arrow" data-rail-prev aria-label="上一台">${icon('chevron-left')}</button><button class="rail-arrow" data-rail-next aria-label="下一台">${icon('chevron-right')}</button></div>
    <a class="card-action" href="?screen=home&modal=add-server">${icon('plus')} 添加服务器</a>
  </div>`;
  if (mode === 'loading') {
    return `<section class="card server-panel">${head}<div class="server-rail">
      ${[1, 2, 3, 4].map(() => `<div class="server-tile"><div class="server-top"><div class="sk" style="width:33px;height:33px;border-radius:8px"></div><div class="flex1"><div class="sk sk-line tall" style="width:72%"></div><div class="sk sk-line" style="width:44%;margin-top:7px"></div></div></div><div class="sk sk-line" style="width:92%;margin-top:14px"></div><div class="sk sk-line" style="width:70%;margin-top:6px"></div><div class="sk sk-line" style="width:48%;margin-top:22px"></div></div>`).join('')}
    </div></section>`;
  }
  if (mode === 'empty') {
    return `<section class="card server-panel">${head}<div class="server-empty">
      <div class="mini-empty-icon">${icon('server')}</div>
      <div><b>还没有添加服务器</b><span>填入 host:port，就能在这里看到实时人数与延迟。</span></div>
      <a class="btn btn-soft btn-sm" href="?screen=home&modal=add-server">${icon('plus')} 添加</a>
    </div></section>`;
  }
  if (mode === 'error') {
    return `<section class="card server-panel">${head}<div class="mini-error">
      ${icon('alert')}
      <div><b>服务器状态加载失败</b><span>请检查网络，或稍后重试。</span></div>
      <button class="btn btn-ghost btn-xs" data-community-refresh>${icon('refresh')} 重试</button>
    </div></section>`;
  }
  if (servers.length === 0) {
    return `<section class="card server-panel">${head}<div class="server-empty">
      <div class="mini-empty-icon">${icon('server')}</div>
      <div><b>还没有添加服务器</b><span>填入 host:port，就能在这里看到实时人数与延迟。</span></div>
      <a class="btn btn-soft btn-sm" href="?screen=home&modal=add-server">${icon('plus')} 添加</a>
    </div></section>`;
  }
  return `<section class="card server-panel">${head}<div class="server-rail">${servers.map(serverTile).join('')}</div></section>`;
}

function annImage(a) {
  if (!a.image) return '';
  return a.image.startsWith('data:') || /^https?:/.test(a.image)
    ? `<img src="${a.image}" alt="公告配图">`
    : QuotePhoto();
}

function annPanel(mode = 'normal') {
  const head = `<div class="card-head"><span class="card-title">${icon('bell')} 群公告</span><a class="card-action" href="?screen=home&modal=announcements">查看全部 ${icon('chevron-right')}</a></div>`;
  if (mode === 'loading') {
    return `<section class="card ann-panel">${head}<div class="ann-body"><div class="sk" style="width:36px;height:38px;border-radius:7px"></div><div class="flex1"><div class="sk sk-line tall" style="width:64%"></div><div class="sk sk-line" style="width:88%;margin-top:7px"></div></div></div></section>`;
  }
  if (mode === 'empty') {
    return `<section class="card ann-panel">${head}<div class="ann-body"><div class="ann-empty">${icon('bell')}<span>暂无公告，新的消息会出现在这里。</span></div></div></section>`;
  }
  if (mode === 'error') {
    return `<section class="card ann-panel">${head}<div class="ann-body"><div class="mini-error compact">${icon('alert')}<div><b>公告加载失败</b><span>数据源可能暂时不可用。</span></div><button class="btn-link" data-community-refresh>重试</button></div></div></section>`;
  }
  if (!announcements.length) return annPanel('empty');
  const a = announcements[Q.ann % announcements.length];
  const month = a.date.split('/')[0];
  const day = a.date.split('/')[1];
  return `<section class="card ann-panel">${head}<div class="ann-body">
    <div class="ann-date"><b>${day}</b><span>${month} 月</span></div>
    ${a.image ? `<div class="ann-thumb">${annImage(a)}</div>` : ''}
    <div class="ann-copy"><div class="ann-title"><span class="tag green">${a.tag}</span>${a.title}</div><div class="ann-text">${a.text}</div></div>
    <div class="ann-pager"><button class="ann-arrow" data-ann-prev aria-label="上一条">${icon('chevron-left')}</button><span class="pager-count">${(Q.ann % announcements.length) + 1} / ${announcements.length}</span><button class="ann-arrow" data-ann-next aria-label="下一条">${icon('chevron-right')}</button></div>
  </div></section>`;
}

function recRow(r) {
  return `<a class="rec-row" href="?screen=download&type=${r.type}">
    <span class="rec-icon ${tileClass(r.color)}">${icon(r.icon)}</span>
    <span class="rec-copy"><span class="rec-name">${r.name}</span><span class="rec-meta"><span class="rec-type">${typeMeta[r.type].label}</span>由 ${r.author} 推荐</span></span>
    <span class="rec-arrow">${icon('chevron-right')}</span>
  </a>`;
}

function recPanel(mode = 'normal') {
  const head = `<div class="card-head"><span class="card-title">${icon('sparkles')} 群友推荐</span><button class="card-action link-action" data-recs-manage>管理</button><a class="card-action" href="?screen=download&type=server">查看更多 ${icon('chevron-right')}</a></div>`;
  if (mode === 'loading') {
    return `<section class="card rec-panel">${head}<div class="rec-list">${[1, 2, 3].map(() => `<div class="rec-row"><div class="sk" style="width:28px;height:28px;border-radius:7px"></div><div class="flex1"><div class="sk sk-line" style="width:60%"></div><div class="sk sk-line" style="width:40%;margin-top:6px"></div></div></div>`).join('')}</div></section>`;
  }
  if (mode === 'empty') {
    return `<section class="card rec-panel">${head}<div class="rec-empty">${icon('sparkles')}<div><b>暂无群友推荐</b><span>等群友整理好，第一时间放上来。</span></div></div></section>`;
  }
  if (mode === 'error') {
    return `<section class="card rec-panel">${head}<div class="mini-error compact">${icon('alert')}<div><b>推荐加载失败</b><span>请检查推荐数据源。</span></div><button class="btn-link" data-community-refresh>重试</button></div></section>`;
  }
  return `<section class="card rec-panel">${head}<div class="rec-list">${recs.slice(0, 3).map(recRow).join('')}</div></section>`;
}

function quoteStrip(mode = 'normal') {
  if (mode === 'loading') {
    return `<section class="card quote-strip"><div class="sk" style="width:52px;height:36px"></div><div class="flex1"><div class="sk sk-line tall" style="width:62%"></div><div class="sk sk-line" style="width:28%;margin-top:9px"></div></div></section>`;
  }
  if (mode === 'empty') {
    return `<section class="card quote-strip"><div class="quote-label"><div class="qmark">“</div><b>群友语录</b></div><div class="quote-main"><div class="quote-fallback">${icon('message')}<span>还没有语录。等第一条来自群友的话。</span></div></div></section>`;
  }
  if (mode === 'error') {
    return `<section class="card quote-strip"><div class="quote-label"><div class="qmark">“</div><b>群友语录</b></div><div class="quote-main"><div class="quote-fallback">${icon('alert')}<span>语录加载失败。</span></div></div><div class="quote-side"><button class="btn-link" data-community-refresh>${icon('refresh')} 重试</button></div></section>`;
  }
  const q = quotes[Q.qi % Math.max(1, quotes.length)] || {
    type: 'text',
    text: '暂无群友语录',
    author: '',
    meta: '',
  };
  const isImage = q.type === 'image';
  const showFallback = Q.quote === 'fallback' && isImage;
  const dots = `<div class="quote-dots">${quotes.map((_, i) => `<span class="qdot ${i === Q.qi ? 'on' : ''}"></span>`).join('')}</div>`;
  if (showFallback) {
    return `<section class="card quote-strip">
      <div class="quote-label"><div class="qmark">“</div><b>群友语录</b></div>
      <div class="quote-main quote-anim"><div class="quote-photo is-broken">${icon('image-off')}</div><div class="quote-photo-copy"><div class="quote-photo-title">图片暂时无法加载</div><div class="quote-photo-meta">已自动回退为文字：${q.text}</div><div class="quote-hint" style="margin-top:6px">${icon('refresh')} 点击换一条，或稍后重试</div></div></div>
      <div class="quote-side">${dots}<span class="quote-hint">图片模式</span></div>
    </section>`;
  }
  if (isImage) {
    return `<section class="card quote-strip" title="点击查看下一条">
      <div class="quote-label"><div class="qmark">“</div><b>群友语录</b></div>
      <div class="quote-main quote-anim"><div class="quote-photo">${QuotePhoto()}</div><div class="quote-photo-copy"><div class="quote-photo-title">${q.text}</div><div class="quote-photo-meta">来自 ${q.author} · ${q.meta}</div><div class="quote-hint" style="margin-top:6px">${icon('image')} 点击空白处查看下一条</div></div></div>
      <div class="quote-side">${dots}<span class="quote-hint">${icon('refresh')} 点击切换</span></div>
    </section>`;
  }
  return `<section class="card quote-strip" title="点击查看下一条">
    <div class="quote-label"><div class="qmark">“</div><b>群友语录</b></div>
    <div class="quote-main quote-anim"><div class="quote-text">${q.text}<span class="author">—— ${q.author} · ${q.meta}</span></div></div>
    <div class="quote-side">${dots}<span class="quote-hint">${icon('refresh')} 点击切换</span></div>
  </section>`;
}

function homePage() {
  const m = Q.state;
  const modes = m === 'loading' || m === 'empty' || m === 'error' ? m : 'normal';
  const versions = Q.panel === 'versions';
  if (versions) {
    return shell(
      'home',
      '',
      `<div class="home-main version-open">
      <div class="home-left">${accountCard()}${launchPanel(true)}</div>
      <div class="home-right version-right">${instancePanel()}</div>
    </div>`
    );
  }
  return shell(
    'home',
    '',
    `<div class="home-main">
      <div class="home-left">${accountCard()}${launchPanel()}</div>
      <div class="home-right">${serverPanel(modes)}${annPanel(modes)}${recPanel(modes)}</div>
    </div>
    ${quoteStrip(modes)}`
  );
}

/* ---------------- download ---------------- */
