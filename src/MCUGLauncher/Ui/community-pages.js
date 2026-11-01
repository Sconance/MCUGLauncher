// Page rendering only; native operations are handled by feature modules.
function recCard(r) {
  const isServer = r.type === 'server';
  const meta = isServer
    ? `<span>${icon('users')} ${r.online === false ? '离线' : `${r.players} / ${r.max}`}</span><span class="sep"></span><span class="${r.online === false ? 'muted' : 'green-text'}">${r.online === false ? '—' : `${r.ping} ms`}</span>`
    : `<span>${escapeHtml(r.size || '—')}</span><span class="sep"></span><span>${escapeHtml(r.extra || r.version || '—')}</span><span class="sep"></span><span>${escapeHtml(r.version || '—')}</span>`;
  const foot = isServer
    ? `<span class="addr">${escapeHtml(r.address || r.addr || '未提供地址')}</span><button class="btn btn-soft" data-rec-server="${escapeHtml(r.address || r.addr || '')}" ${r.address || r.addr ? '' : 'disabled title="未提供服务器地址"'}>${icon('arrow-up-right')} 查看</button>`
    : `<a class="btn btn-soft" href="?screen=catalog&type=${r.type === 'shader' ? 'shader' : 'resourcepack'}&q=${encodeURIComponent(r.name)}">${icon('download')} 搜索资源</a>${/^https?:\/\//i.test(r.url || '') ? `<a class="btn btn-ghost" href="${escapeHtml(r.url)}" target="_blank" rel="noreferrer">资源链接</a>` : ''}`;
  return `<article class="card rec-card">
    <div class="r-card-top">
      <div class="r-card-icon ${tileClass(r.color)}">${icon(r.icon)}</div>
      <div class="r-card-heading">
        <div class="r-card-title">${escapeHtml(r.name)}</div>
        <div class="r-card-by">由 <b>${escapeHtml(r.author)}</b> 推荐 · ${typeMeta[r.type].label}</div>
      </div>
    </div>
    <div class="r-card-tags">${(r.tags || []).map((t, i) => `<span class="tag ${i === 0 ? 'green' : ''}">${escapeHtml(t)}</span>`).join('')}</div>
    <div class="r-card-desc">${escapeHtml(r.intro || r.desc || '暂无介绍')}</div>
    <div class="r-card-meta">${meta}</div>
    <div class="r-card-foot">${foot}</div>
  </article>`;
}

function downloadPage() {
  const type = ['server', 'material', 'shader'].includes(Q.type) ? Q.type : 'server';
  const list = recs.filter((r) => r.type === type);
  const counts = {
    server: recs.filter((r) => r.type === 'server').length,
    material: recs.filter((r) => r.type === 'material').length,
    shader: recs.filter((r) => r.type === 'shader').length,
  };
  if (Q.state === 'empty') counts[type] = 0;
  const head = `<div class="dl-head">
    <div class="dl-title-wrap">
      <div class="dl-big-icon">${icon('users')}</div>
      <div><div class="page-title">群友推荐</div><div class="page-sub">群友亲自挑过的服务器、材质包与光影。</div></div>
    </div>
    <div class="dl-head-side"><span class="dl-count">${state.communityUpdated ? '本次读取：' + escapeHtml(state.communityUpdated) : '尚未读取社区数据'}</span><button class="btn btn-ghost btn-sm" data-community-refresh>${icon('refresh')} 刷新</button></div>
  </div>`;
  const seg = `<div class="segbar">
    <div class="segmented" role="tablist" aria-label="推荐类型">
      ${['server', 'material', 'shader'].map((t) => `<a class="seg-btn ${type === t ? 'active' : ''}" href="?screen=download&type=${t}">${icon(typeMeta[t].icon)}${typeMeta[t].label}<span class="count">${counts[t]}</span></a>`).join('')}
    </div>
    <span class="muted" style="font-size:9.5px">点击卡片可在分类间切换 · 数据与主页同源</span>
  </div>`;
  let body = '';
  if (Q.state === 'loading') {
    body = `<div class="state-skeleton-grid">${[1, 2, 3, 4, 5, 6].map(() => `<div class="card state-skeleton-card"><div class="top"><div class="sk" style="width:48px;height:48px;border-radius:10px"></div><div class="flex1"><div class="sk sk-line tall" style="width:72%"></div><div class="sk sk-line" style="width:45%;margin-top:8px"></div></div></div><div class="sk sk-line" style="width:100%;margin-top:8px"></div><div class="sk sk-line" style="width:82%;margin-top:6px"></div><div class="sk sk-line" style="width:100%;margin-top:18px"></div><div class="sk sk-line" style="width:64%;margin-top:20px"></div></div>`).join('')}</div>`;
  } else if (Q.state === 'empty') {
    body = `<div class="state-page" style="height:588px"><div class="state-box">
      <div class="state-icon">${icon('sparkles')}</div>
      <div class="state-title">暂无群友推荐</div>
      <div class="state-copy">这一分类还没有内容。群友正在整理，稍后再来看看，或者去其他分类逛逛。</div>
      <div class="state-actions"><button class="btn btn-soft" data-community-refresh>${icon('refresh')} 刷新</button><a class="btn btn-ghost" href="?screen=download&type=server">看看服务器</a></div>
    </div></div>`;
  } else if (Q.state === 'error') {
    body = `<div class="state-page" style="height:588px"><div class="state-box">
      <div class="state-icon error">${icon('alert')}</div>
      <div class="state-title">推荐加载失败</div>
      <div class="state-copy">无法读取推荐数据源（超时或返回格式不正确）。你的游戏与下载不受影响，可以重试或切换数据源。</div>
      <div class="state-actions"><button class="btn btn-primary" data-community-refresh>${icon('refresh')} 重试</button><a class="btn btn-ghost" href="?screen=datasource">${icon('database')} 打开数据源设置</a></div>
    </div></div>`;
  } else {
    body = `<div class="dl-grid">${list.map(recCard).join('')}</div>
      <div class="dl-footnote"><span class="inline">${icon('info')} 推荐内容由群友维护，启动器只负责展示与跳转。</span><span>共 ${list.length} 条 · 按最近更新排序</span></div>`;
  }
  return shell('download', downloadSidebar('community'), `${head}${seg}${body}`);
}

function optionsPage() {
  if (window.chrome?.webview)
    return shell(
      'settings',
      settingsSidebar('launch'),
      `<div class="settings-content">
    ${pageHead('账号与社区设置', '本地账号状态与社区密钥；保存密钥不等于服务器身份认证。')}
    <section class="card settings-card"><h3>当前账号</h3><p>${escapeHtml(state.player || '未登录')} · ${escapeHtml(state.accountLabel || '离线账号')}</p>
    <a class="btn btn-ghost" href="?screen=home">返回主页切换账号</a><p>切换到离线不会删除在线账号。只有下方移除操作会清理本机授权。</p><button class="btn btn-ghost" data-forget-account="microsoft">移除保存的正版账号</button><button class="btn btn-ghost" data-forget-account="mua">移除保存的 MUA 账号</button></section>
    <section class="card settings-card"><h3>社区请求</h3><p>匿名模式：${state.anonymous ? '已开启' : '已关闭'}</p>
    <button class="btn btn-ghost ${state.anonymous ? 'on' : ''}" data-anon>切换匿名模式</button></section>
    <section class="card settings-card"><h3>社区密钥</h3><p>${state.communityKey ? '已保存在本机' : '尚未保存'}；此处只保存和检查格式，不验证远端社区身份。</p>
    <input class="input" type="password" data-key-input placeholder="粘贴群管理发给你的密钥" autocomplete="off"><button class="btn btn-primary" data-verify>保存密钥</button></section>
  </div>`
    );

  const st = ['default', 'verifying', 'error', 'verified'].includes(Q.state) ? Q.state : 'default';
  const anon = Q.anon || st === 'verified';
  const publicName = st === 'verified' ? '阿澈' : 'waibibabu1998';
  const publicMeta = st === 'verified' ? '社区身份 #A7F3' : '玩家 ID · 暮色森林';
  const anonCard = `<section class="card settings-card">
    <div class="settings-card-head">
      <div class="settings-card-icon">${icon('users')}</div>
      <div><div class="settings-card-title">匿名模式 <span class="help-wrap ${Q.tip ? 'open' : ''}"><button class="help-dot" aria-label="匿名模式说明">?</button><span class="tooltip">开启后，社区请求使用随机别名，隐藏玩家 ID、头像和实例名称；不改变游戏、下载或微软登录。</span></span></div><div class="settings-card-desc">在社区功能中隐藏你的身份。只影响社区请求，不影响游戏与账号。</div></div>
      <div class="settings-card-aside"><span class="switch-text">${anon ? '已开启' : '已关闭'}</span><button class="switch ${anon ? 'on' : ''}" role="switch" aria-checked="${anon}" data-anon></button></div>
    </div>
    <div class="anon-scope"><span class="scope-label">影响</span><span class="tag green">公告</span><span class="tag green">服务器</span><span class="tag green">推荐</span><span class="tag green">语录</span><span class="scope-sep"></span><span class="scope-label">不影响</span><span class="tag">游戏启动</span><span class="tag">下载</span><span class="tag">微软登录</span></div>
    <div class="anon-preview">
      <div class="who-card ${anon ? '' : 'on'}">
        <span class="who-label">关闭时</span>
        <div class="who-id"><span class="who-avatar">${Avatar()}</span><div><b>${publicName}</b><span>${publicMeta}</span></div></div>
      </div>
      <span class="anon-arrow">${icon('arrow-right')}</span>
      <div class="who-card ${anon ? 'on' : ''}">
        <span class="who-label">开启后</span>
        <div class="who-id"><span class="who-avatar ghost">${icon('hash')}</span><div><b>群友 #A7F3</b><span>匿名实例 · 社区请求</span></div></div>
      </div>
    </div>
    ${anon ? `<div class="status-bar green">${icon('shield')}<div><b>匿名模式已开启</b> · 社区请求使用随机别名，不显示玩家 ID、实例名称与头像。</div></div>` : `<div class="status-bar info">${icon('info')}<div>关闭后，社区功能会显示你的玩家 ID 与实例信息。不会收集其他使用数据。</div></div>`}
  </section>`;

  let keyBody = '';
  if (st === 'verified') {
    keyBody = `<div class="verified-box">
      <div class="verified-avatar">${Avatar()}</div>
      <div><div class="verified-name">阿澈 <span class="identity-badge">${icon('check')} 已验证</span></div><div class="verified-meta">社区身份 #A7F3 · 密钥已保存在本机</div></div>
      <div class="verified-actions"><button class="btn-link" data-verify-reset>更换密钥</button><button class="btn-link muted">退出</button></div>
    </div>
    <div class="key-helper">${icon('lock')} 密钥仅保存在本机，不会随启动器版本更新失效；清除配置或手动退出后需要重新验证。</div>`;
  } else if (st === 'verifying') {
    keyBody = `<div class="key-row"><div class="field"><div class="field-label">社区密钥</div><input class="input" value="mck_live_••••••••••••4f2a" disabled></div><button class="btn btn-primary" disabled><span class="spinner"></span>验证中…</button></div>
      <div class="status-bar info" style="margin-top:10px">${icon('loader')}<div>正在向社区服务验证密钥，请稍候。不会上传游戏或账号信息。</div></div>`;
  } else if (st === 'error') {
    keyBody = `<div class="key-row"><div class="field"><div class="field-label">社区密钥</div><input class="input error" value="mck_old_7d2c9a1b" autofocus></div><button class="btn btn-primary">重新验证</button></div>
      <div class="field-error" style="margin-top:7px">${icon('alert')} 密钥无效或已过期，请向群管理获取新密钥。</div>
      <div class="key-helper">${icon('lock')} 密钥仅保存在本机，不会上传到除社区验证接口以外的任何地方。</div>`;
  } else {
    keyBody = `<div class="key-row"><div class="field"><div class="field-label">社区密钥</div><input class="input" placeholder="粘贴群管理发给你的密钥" data-key-input></div><button class="btn btn-primary" data-verify>验证</button></div>
      <div class="key-helper">${icon('lock')} 粘贴时会自动去除首尾空格。密钥仅保存在本机，不会随版本更新失效。</div>`;
  }

  const keyUses = ['default', 'error'].includes(st)
    ? `<div class="key-uses">
    <span>${icon('check')} 显示社区身份标签</span>
    <span>${icon('check')} 未来可用于推荐投稿</span>
    <span class="no">${icon('x')} 不是激活码，不验证正版</span>
  </div>`
    : '';

  const keyCard = `<section class="card settings-card">
    <div class="settings-card-head">
      <div class="settings-card-icon">${icon('key')}</div>
      <div><div class="settings-card-title">社区身份密钥</div><div class="settings-card-desc">用于标识你在社区中的身份，不是激活码，也不验证正版账号。</div></div>
      <div class="settings-card-aside">${st === 'verified' ? `<span class="identity-badge">${icon('shield')} 已认证</span>` : `<span class="tag gray">未验证</span>`}</div>
    </div>
    ${keyBody}${keyUses}
  </section>`;

  return shell(
    'options',
    settingsSidebar('options'),
    pageHead(
      '更多选项',
      '社区身份、匿名与本地保存方式。',
      `<span class="inline">${icon('lock')} 设置仅保存在本机</span>`
    ) + `<div class="settings-body options-body">${anonCard}${keyCard}</div>`
  );
}

/* ---------------- settings: admin ---------------- */

function adminPage() {
  let st = ['before', 'verifying', 'after', 'published'].includes(Q.state) ? Q.state : 'before';
  if (
    Q.modal === 'publish' &&
    (Q.state === 'saved' || Q.state === 'normal' || Q.state === 'default')
  )
    st = 'after';
  const authed = st === 'after' || st === 'published';
  const published = st === 'published';
  const endpoint = published || Q.endpoint;
  if (!authed) {
    const verifying = st === 'verifying';
    const failed = Q.state === 'error';
    return shell(
      'admin',
      settingsSidebar('admin'),
      pageHead(
        '管理员',
        '仅群管理员使用。认证后可发布公告。',
        `<span class="inline">${icon('lock')} 本机认证</span>`
      ) +
        `
      <div class="admin-gate">
        <section class="card settings-card admin-auth-card">
          <div class="settings-card-head">
            <div class="settings-card-icon">${icon('shield')}</div>
            <div><div class="settings-card-title">管理员认证</div><div class="settings-card-desc">首次输入会设置本机管理员密钥；之后必须输入同一密钥。</div></div>
            <div class="settings-card-aside"><span class="tag ${failed ? 'red' : 'gray'}">${verifying ? '验证中' : failed ? '认证失败' : '未认证'}</span></div>
          </div>
          <div class="key-row"><div class="field"><div class="field-label">管理员密钥</div><input class="input ${failed ? 'error' : ''}" placeholder="粘贴管理员密钥" ${verifying || failed ? 'value="mck_admin_••••••••••8c1d"' : ''} ${verifying ? 'disabled' : ''}></div><button class="btn btn-primary" ${verifying ? 'disabled' : ''}>${verifying ? '<span class="spinner"></span>验证中…' : failed ? '重新验证' : '验证'}</button></div>
          ${failed ? `<div class="field-error" style="margin-top:7px">${icon('alert')} 管理员密钥无效或已过期，请向群主确认。</div>` : verifying ? `<div class="status-bar info" style="margin-top:10px">${icon('loader')}<div>正在验证管理员身份…</div></div>` : `<div class="key-helper">${icon('lock')} 管理员密钥与普通社区密钥相互独立，仅保存在本机。认证后才会出现发布入口。</div>`}
        </section>
      </div>`
    );
  }

  const adminIdentity = `<div class="admin-identity">
    <div class="admin-shield">${icon('shield')}</div>
    <div><div class="admin-name">TianXuan <span class="identity-badge">${icon('check')} 管理员</span></div><div class="admin-meta">群主 · 密钥已保存在本机 · 认证于 10 月 30 日</div></div>
    <div class="verified-actions"><button class="btn-link muted">退出管理</button></div>
  </div>`;

  return shell(
    'admin',
    settingsSidebar('admin'),
    pageHead(
      '管理员',
      '发布公告、查看最近的发布记录。',
      `<span class="inline">${icon('shield')} 已认证</span>`
    ) +
      `
    <div class="settings-body">
      <section class="card settings-card">
        ${adminIdentity}
        <div class="admin-publish-row">
          <div class="admin-publish-copy"><div class="admin-publish-title">发布一条新公告</div><div class="admin-publish-desc">标题 + 正文，可选过期时间。发布后出现在主页公告栏。</div></div>
          <a class="btn btn-primary btn-sm" href="?screen=admin&state=after&modal=publish${endpoint ? '&endpoint=1' : ''}">${icon('send')} 发布公告</a>
        </div>
        ${endpoint ? `<div class="status-bar green" style="margin-top:12px">${icon('check')}<div><b>写入接口已连接</b> · 发布会立即同步到社区数据源。</div></div>` : `<div class="status-bar amber" style="margin-top:12px">${icon('alert')}<div><b>写入接口未配置</b> · 发布会先保存为「待发布」，你可以在数据源设置中填写写入接口后再同步。</div></div>`}
      </section>
      <section class="card settings-card">
        <div class="settings-card-head"><div class="settings-card-icon">${icon('clock')}</div><div><div class="settings-card-title">最近发布</div><div class="settings-card-desc">仅显示本机记录，不会暴露给普通用户。</div></div><a class="card-action" href="?screen=datasource">${icon('database')} 数据源设置</a></div>
        <div class="recent-list">
          <div class="recent-row"><span class="tag green">已发布</span><span class="title">0.22.3 社区功能测试开启</span><span class="time">10 月 30 日 23:10</span></div>
          <div class="recent-row"><span class="tag amber">待发布</span><span class="title">周五晚 8 点 · 群友合建</span><span class="time">10 月 29 日 18:42</span></div>
        </div>
      </section>
      <section class="card settings-card admin-preview-card">
        <div class="settings-card-head"><div class="settings-card-icon">${icon('monitor')}</div><div><div class="settings-card-title">主页预览</div><div class="settings-card-desc">普通用户看到的公告栏样式，发布前先确认措辞。</div></div><a class="card-action" href="?screen=home">${icon('external')} 打开主页</a></div>
        <div class="preview-ann">
          <div class="preview-ann-date"><b>30</b><span>10 月</span></div>
          <div class="preview-ann-image">${QuotePhoto()}</div>
          <div class="preview-ann-copy"><div class="preview-ann-title"><span class="tag green">更新</span>${announcements[0].title}</div><div class="preview-ann-text">${announcements[0].text}</div></div>
          <span class="preview-ann-more">查看全部 ${icon('chevron-right')}</span>
        </div>
      </section>
      ${published ? `<div class="status-bar green">${icon('check')}<div><b>公告已发布</b> · 1 分钟前同步到主页公告栏，可在「查看全部」中管理。</div></div>` : ''}
    </div>`
  );
}

/* ---------------- settings: datasource ---------------- */

function datasourcePage() {
  const err = Q.state === 'error';
  const urls = {
    announcements: err
      ? 'https://raw.githubusercontent.com/waibibabu1239/MCUGL-storage/main/announcements-v2.json'
      : DS[0].url,
    servers: DS[1].url,
    recommendations: err
      ? 'github.com/waibibabu1239/MCUGL-storage/recommendations.json'
      : DS[2].url,
    quotes: DS[3].url,
  };
  const results = err
    ? {
        announcements: ['bad', 'HTTP 404 · 文件不存在'],
        servers: ['bad', '连接超时（8s）'],
        recommendations: ['bad', '地址需以 https:// 开头'],
        quotes: ['ok', '连接正常 · 104 ms'],
      }
    : {
        announcements: ['ok', '连接正常 · 128 ms'],
        servers: ['ok', '连接正常 · 96 ms'],
        recommendations: ['', '尚未测试'],
        quotes: ['', '尚未测试'],
      };
  const rows = DS.map(
    (d) => `<div class="ds-row">
    <div class="ds-label"><b>${d.label}</b><span>${d.hint}</span></div>
    <div class="ds-input-wrap"><input class="input ${err && (d.key === 'announcements' || d.key === 'recommendations') ? 'error' : ''}" value="${urls[d.key]}"></div>
    <button class="btn btn-ghost ds-test">${results[d.key][0] === 'ok' ? icon('check') : icon('wifi')} 测试</button>
    <div class="ds-result ${results[d.key][0]}">${results[d.key][0] === 'ok' ? icon('check') : results[d.key][0] === 'bad' ? icon('alert') : icon('clock')} ${results[d.key][1]}</div>
  </div>`
  ).join('');

  return shell(
    'datasource',
    settingsSidebar('datasource'),
    pageHead(
      '数据源',
      '四类数据分别读取独立 JSON，切换只影响对应的区块。',
      `<span class="inline">${icon('database')} 支持 GitHub Raw / 自有服务器</span>`
    ) +
      `
    <div class="settings-body">
      <section class="card settings-card">
        <div class="ds-preset">
          <div class="segmented"><button class="seg-btn ${err ? '' : 'active'}">GitHub 官方源</button><button class="seg-btn ${err ? 'active' : ''}">自定义</button></div>
          <span class="ds-preset-note">${err ? '当前为自定义源，四项可分别填写。' : '官方源跟随 waibibabu1239/MCUGL-storage 仓库更新。'}</span>
        </div>
        <div class="ds-rows">${rows}</div>
        <div class="ds-write">
          <div class="ds-label"><b>写入接口</b><span>管理员发布，可选</span></div>
          <input class="input" placeholder="https://api.example.com/announcements（留空则发布为待发布）" value="${err ? '' : ''}">
          <div class="ds-write-note">只用于管理员发布公告；普通用户不会调用。留空时发布的内容只保存在本机，需导出 JSON 手动上传。</div>
        </div>
        <div class="ds-footer">
          <button class="btn btn-primary">保存数据源</button>
          <button class="btn btn-ghost">恢复默认</button>
          <span class="spacer"></span>
          <span class="ds-footer-note">${icon('info')} 四项独立生效，保存后立即刷新对应区块</span>
        </div>
      </section>
      <div class="status-bar ${err ? 'red' : 'info'}">${icon(err ? 'alert' : 'info')}<div>${err ? '<b>有 3 个数据源不可用。</b> 红色结果只影响对应区块，其他内容仍会正常显示。修正后逐项测试，再保存。' : '修改任一项后点击「保存数据源」才会生效。测试只请求该地址，不会写入任何内容。'}</div></div>
    </div>`
  );
}

/* ---------------- modals ---------------- */

function addServerModal() {
  const edit = Q.modal === 'edit-server';
  const editing = edit ? servers.find((s) => s.id === Q.sid) : null;
  const err = (Q.merr === '1' || Q.state === 'error') && !edit;
  return `<div class="overlay">
    <section class="modal" role="dialog" aria-modal="true" aria-labelledby="add-title">
      <div class="modal-head"><div class="modal-icon">${icon('server')}</div><div><div class="modal-title" id="add-title">${edit ? '编辑服务器' : '添加服务器'}</div><div class="modal-sub">支持 host 或 host:port，默认端口 25565。</div></div><a class="modal-close" href="?screen=home" aria-label="关闭">${icon('x')}</a></div>
      <div class="modal-body">
        <div class="field"><div class="field-label">名称 <span class="opt">可选</span></div><input class="input" data-server-name placeholder="例如：暮色森林" value="${editing?.name || (edit ? '暮色森林' : err ? '暮色森林' : '')}"></div>
        <div class="field"><div class="field-label">服务器地址</div><input class="input ${err ? 'error' : ''}" data-server-address placeholder="play.example.com 或 play.example.com:25565" value="${editing?.address || (edit ? 'play.mclc.cn:25565' : err ? 'play.mclc.cn:2556' : '')}"><input type="hidden" data-server-old value="${editing?.address || ''}"></div>
        ${err ? `<div class="field-error">${icon('alert')} 端口 2556 无法连接。请检查端口，或只填写域名使用默认 25565。</div>` : `<div class="field-help">${edit ? '修改后会立即重新查询状态，并更新本机保存的地址。' : '添加后会立即查询一次状态；之后只在卡片可见时刷新，每 30 秒最多一次。'}</div>`}
        ${err ? `<div class="modal-status red">${icon('alert')}<div><b>无法连接到该服务器</b> · 请确认地址、端口与服务器是否在线。</div></div>` : `<div class="modal-status ${edit ? 'info' : 'green'}">${icon(edit ? 'info' : 'wifi')}<div>${edit ? '只修改本机收藏，不会影响服务器本身。' : '支持 Java 版服务器列表 ping；不解析 DNS SRV，必要时请填写实际端口。'}</div></div>`}
      </div>
      <div class="modal-foot"><a class="btn btn-ghost" href="?screen=home">取消</a>${edit ? `<button class="btn-link danger-link">${icon('trash')} 移除收藏</button>` : ''}<span class="spacer"></span><button class="btn btn-primary">${err ? `${icon('refresh')} 重试` : edit ? `${icon('check')} 保存修改` : `${icon('plus')} 添加并查询`}</button></div>
    </section>
  </div>`;
}

function publishModal() {
  const saved = Q.state === 'saved';
  if (saved && Q.endpoint) {
    return `<div class="overlay"><section class="modal wide" role="dialog" aria-modal="true">
      <div class="modal-head"><div class="modal-icon">${icon('check')}</div><div><div class="modal-title">公告已发布</div><div class="modal-sub">已同步到社区数据源，主页公告栏随即更新。</div></div><a class="modal-close" href="?screen=admin&state=after&endpoint=1" aria-label="关闭">${icon('x')}</a></div>
      <div class="modal-body">
        <div class="modal-status green">${icon('check')}<div><b>1 分钟前同步完成</b> · 公告已写入 announcements.json，其他用户刷新主页即可看到。</div></div>
        <div class="publish-preview"><div class="publish-preview-image">${QuotePhoto()}</div><div><div class="publish-preview-label">已发布内容</div><div class="publish-preview-title">0.22.3 社区功能测试开启</div><div class="publish-preview-text">群友推荐、服务器状态与数据源切换开始试运行，欢迎反馈。</div><div class="publish-preview-meta">10 月 31 日 · 管理员 TianXuan · 不过期</div></div></div>
      </div>
      <div class="modal-foot"><a class="btn btn-ghost" href="?screen=admin&state=after&modal=publish&endpoint=1">${icon('plus')} 再发一条</a><span class="spacer"></span><a class="btn btn-primary" href="?screen=home">查看主页</a></div>
    </section></div>`;
  }
  if (saved) {
    return `<div class="overlay"><section class="modal wide" role="dialog" aria-modal="true">
      <div class="modal-head"><div class="modal-icon">${icon('clock')}</div><div><div class="modal-title">已保存为待发布</div><div class="modal-sub">内容已保存在本机，不会自动同步到社区。</div></div><a class="modal-close" href="?screen=admin&state=after" aria-label="关闭">${icon('x')}</a></div>
      <div class="modal-body">
        <div class="modal-status amber">${icon('alert')}<div><b>公告草稿已保存</b> · 你可以在数据源设置中填写写入接口后重新发布，或导出 JSON 手动上传到 GitHub。</div></div>
        <div class="publish-preview"><div class="publish-preview-image">${QuotePhoto()}</div><div><div class="publish-preview-label">预览</div><div class="publish-preview-title">0.22.3 社区功能测试开启</div><div class="publish-preview-text">群友推荐、服务器状态与数据源切换开始试运行，欢迎反馈。</div><div class="publish-preview-meta">10 月 31 日 · 管理员 TianXuan</div></div></div>
      </div>
      <div class="modal-foot"><button class="btn btn-ghost">${icon('copy')} 导出 JSON</button><span class="spacer"></span><a class="btn btn-primary" href="?screen=admin&state=after">完成</a></div>
    </section></div>`;
  }
  return `<div class="overlay"><section class="modal wide" role="dialog" aria-modal="true" aria-labelledby="pub-title">
    <div class="modal-head"><div class="modal-icon">${icon('send')}</div><div><div class="modal-title" id="pub-title">发布公告</div><div class="modal-sub">发布后会出现在主页公告栏，普通用户只读。</div></div><a class="modal-close" href="?screen=admin&state=after" aria-label="关闭">${icon('x')}</a></div>
    <div class="modal-body">
      <div class="field"><div class="field-label">标题</div><input class="input" value="0.22.3 社区功能测试开启"></div>
      <div class="field"><div class="field-label">正文</div><textarea class="textarea">群友推荐、服务器状态与数据源切换开始试运行，欢迎反馈。</textarea><div class="char-count">37 / 500</div></div>
      <div class="field"><div class="field-label">配图 <span class="opt">可选</span></div>
        <div class="image-upload has-image"><div class="image-upload-preview">${QuotePhoto()}</div><div class="image-upload-copy"><b>公告封面图已选择</b><span>1024 × 640 · JPG · 218 KB</span><div class="image-upload-actions"><button class="btn-link pick-publish-image">${icon('image')} 更换图片</button><button class="btn-link muted remove-publish-image">移除</button></div></div></div>
        <input type="file" id="publish-image-input" accept="image/png,image/jpeg,image/webp" hidden>
      </div>
      <div class="expiry-row"><label class="checkbox-line"><span class="checkbox checked">${icon('check')}</span>设置过期时间</label><input class="input" value="2026-11-15 23:59" aria-label="过期时间"><span class="muted" style="font-size:9px">到期后自动隐藏</span></div>
      <div class="modal-status ${Q.endpoint ? 'green' : 'amber'}">${icon(Q.endpoint ? 'check' : 'alert')}<div>${Q.endpoint ? '<b>写入接口已连接</b> · 点击发布后会立即同步到社区数据源。' : '<b>写入接口未配置</b> · 本次发布只会保存为「待发布」，可在数据源设置中配置后再同步。'}</div></div>
    </div>
    <div class="modal-foot"><a class="btn btn-ghost" href="?screen=admin&state=after">取消</a><span class="spacer"></span><button class="btn btn-soft">${icon('edit')} 存为草稿</button><button class="btn btn-primary">${icon('send')} ${Q.endpoint ? '发布公告' : '保存为待发布'}</button></div>
  </section></div>`;
}

function recManageModal() {
  return `<div class="overlay"><section class="modal" role="dialog" aria-modal="true" aria-labelledby="rec-manage-title">
    <div class="modal-head"><div class="modal-icon">${icon('sparkles')}</div><div><div class="modal-title" id="rec-manage-title">管理群友推荐</div><div class="modal-sub">删除只影响这台电脑的显示；管理员可在数据源中同步修改。</div></div><a class="modal-close" href="?screen=home" aria-label="关闭">${icon('x')}</a></div>
    <div class="modal-body rec-manage-list">
      ${recs.map((r) => `<div class="rec-manage-row"><span class="rec-icon ${tileClass(r.color)}">${icon(r.icon)}</span><div class="rec-manage-copy"><b>${escapeHtml(r.name)}</b><span>${typeMeta[r.type].label} · 由 ${escapeHtml(r.author)} 推荐</span></div><button class="micon danger" data-delete-rec="${r.id}" title="删除推荐" aria-label="删除推荐">${icon('trash')}</button></div>`).join('')}
    </div>
    <div class="modal-foot"><span class="modal-note">共 ${recs.length} 条推荐。</span><span class="spacer"></span><a class="btn btn-primary" href="?screen=home">完成</a></div>
  </section></div>`;
}

function announcementsModal() {
  return `<div class="overlay"><section class="modal wide" role="dialog" aria-modal="true" aria-labelledby="ann-title">
    <div class="modal-head"><div class="modal-icon">${icon('bell')}</div><div><div class="modal-title" id="ann-title">全部公告</div><div class="modal-sub">由群管理员发布，按时间倒序排列。</div></div><a class="modal-close" href="?screen=home" aria-label="关闭">${icon('x')}</a></div>
    <div class="modal-body ann-modal-body">
      ${announcements
        .map(
          (a, i) => `<article class="ann-modal-row ${i === 0 ? 'latest' : ''}">
        <div class="ann-modal-date"><b>${a.date.split('/')[1]}</b><span>${a.date.split('/')[0]} 月</span></div>
        ${a.image ? `<div class="ann-modal-image">${annImage(a)}</div>` : ''}
        <div class="ann-modal-copy"><div class="ann-modal-title"><span class="tag ${i === 0 ? 'green' : 'gray'}">${a.tag}</span>${a.title}</div><div class="ann-modal-text">${a.text}</div><div class="ann-modal-time">${a.time} · 管理员 TianXuan</div></div>
        <button class="ann-delete" data-delete-announcement="${a.id}" title="删除公告" aria-label="删除公告">${icon('trash')}</button>
      </article>`
        )
        .join('')}
    </div>
    <div class="modal-foot"><span class="modal-note">公告会保留到过期时间；过期后自动隐藏。</span><span class="spacer"></span><a class="btn btn-primary" href="?screen=home">完成</a></div>
  </section></div>`;
}
