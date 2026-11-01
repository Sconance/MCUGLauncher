// Page rendering only; native operations are handled by feature modules.
function titlebar(active) {
  const navs = [
    ['home', '启动', 'play'],
    ['download', '下载', 'download'],
    ['options', '设置', 'settings'],
  ];
  return `<header class="titlebar">
    <div class="brand">
      ${BrandLogo()}
      <span class="brand-name">MCUG Launcher</span>
      <span class="brand-version">${appVersion()}</span>
    </div>
    <nav class="topnav">
      ${navs.map(([id, label, ic]) => `<a class="nav-item ${active === id ? 'active' : ''}" href="?screen=${id}">${icon(ic)}<span>${label}</span></a>`).join('')}
    </nav>
    <div class="win-ctrl">
      <button class="win-btn" aria-label="最小化"><svg viewBox="0 0 12 12"><path d="M2 6h8"/></svg></button>
      <button class="win-btn" aria-label="最大化"><svg viewBox="0 0 12 12"><rect x="2.5" y="2.5" width="7" height="7"/></svg></button>
      <button class="win-btn close" aria-label="关闭"><svg viewBox="0 0 12 12"><path d="m2.5 2.5 7 7M9.5 2.5l-7 7"/></svg></button>
    </div>
  </header>`;
}

function browseSidebar(active) {
  const item = (id, label, ic, href, badge = '') =>
    `<a class="side-item ${active === id ? 'active' : ''}" href="${href}">${icon(ic)}<span>${label}</span>${badge ? `<span class="side-badge">${badge}</span>` : ''}</a>`;
  return `<aside class="sidebar">
    <div class="side-group">
      <div class="side-label">开始</div>
      ${item('home', '主页', 'home', '?screen=home')}
    </div>
    <div class="side-group">
      <div class="side-label">社区</div>
      ${item('community', '群友推荐', 'users', '?screen=download&type=server', '新')}
    </div>
    <div class="side-group">
      <div class="side-label">下载</div>
      ${item('vanilla', '原版游戏', 'grid', '?screen=download')}
      ${item('mod', '模组', 'box', '?screen=download')}
      ${item('pack', '整合包', 'layers', '?screen=download')}
      ${item('resource', '资源包', 'image', '?screen=download')}
      ${item('shader', '光影', 'sun', '?screen=download')}
      ${item('datapack', '数据包', 'database', '?screen=download')}
    </div>
    <div class="side-foot"><span class="live-dot"></span><span>社区内容</span></div>
  </aside>`;
}

function settingsSidebar(active) {
  const item = (id, label, ic, href) =>
    `<a class="side-item ${active === id ? 'active' : ''}" href="${href}">${icon(ic)}<span>${label}</span></a>`;
  const page = (id, label, ic) => item(id, label, ic, `?screen=settings&panel=${id}`);
  return `<aside class="sidebar settings-sidebar">
    <div class="side-group">
      <div class="side-label">启动与环境</div>
      ${page('launch', '启动设置', 'play')}
      ${page('java', 'Java 管理', 'terminal')}
      ${page('install', '安装与导入', 'box')}
      ${page('network', '下载与网络', 'download')}
    </div>
    <div class="side-group">
      <div class="side-label">个性化</div>
      ${page('appearance', '界面与背景', 'image')}
    </div>
    <div class="side-group">
      <div class="side-label">社区</div>
      ${item('options', '更多选项', 'users', '?screen=options')}
      ${item('admin', '管理员', 'shield', '?screen=admin')}
      ${item('datasource', '数据源', 'database', '?screen=datasource')}
    </div>
    <div class="side-group">
      <div class="side-label">游戏工具</div>
      ${page('servers', '服务器收藏', 'server')}
      ${page('games', '运行中的游戏', 'play')}
      ${page('instances', '实例维护', 'folder')}
      ${page('bookmarks', '我的收藏', 'heart')}
      ${page('saves', '存档保护', 'box')}
    </div>
    <div class="side-group">
      <div class="side-label">任务与诊断</div>
      ${page('tasks', '下载任务', 'download')}
      ${page('recovery', '任务与恢复', 'refresh')}
      ${page('logs', '运行日志', 'terminal')}
      ${page('diagnose', '诊断与反馈', 'alert')}
    </div>
    <div class="side-group">
      <div class="side-label">关于</div>
      ${page('about', '关于与许可', 'info')}
    </div>
    <div class="side-foot"><span class="live-dot"></span><span>社区内容</span></div>
  </aside>`;
}

function downloadSidebar(active = 'community') {
  const item = (id, label, ic, href, badge = '') =>
    `<a class="side-item ${active === id ? 'active' : ''}" href="${href}">${icon(ic)}<span>${label}</span>${badge ? `<span class="side-badge">${badge}</span>` : ''}</a>`;
  return `<aside class="sidebar">
    <div class="side-group">
      <div class="side-label">社区</div>
      ${item('community', '群友推荐', 'users', '?screen=download&type=server', '新')}
    </div>
    <div class="side-group">
      <div class="side-label">下载</div>
      ${item('vanilla', '原版游戏', 'grid', '?screen=catalog&type=vanilla')}
      ${item('mod', '模组', 'box', '?screen=catalog&type=mod')}
      ${item('modpack', '整合包', 'layers', '?screen=catalog&type=modpack')}
      ${item('resourcepack', '资源包', 'image', '?screen=catalog&type=resourcepack')}
      ${item('shader', '光影', 'sun', '?screen=catalog&type=shader')}
      ${item('datapack', '数据包', 'database', '?screen=catalog&type=datapack')}
      ${item('install', '安装与导入', 'box', '?screen=settings&panel=install')}
    </div>
    <div class="side-foot"><span class="live-dot"></span><span>社区内容</span></div>
  </aside>`;
}

function pageHead(title, sub, right = '') {
  return `<div class="page-head">
    <div><div class="page-title">${title}</div><div class="page-sub">${sub}</div></div>
    <div class="page-note">${right}</div>
  </div>`;
}

/* ---------------- home ---------------- */

function shell(active, sidebar, content) {
  let overlay = '';
  if (Q.modal === 'add-server' || Q.modal === 'edit-server') overlay = addServerModal();
  if (Q.modal === 'publish') overlay = publishModal();
  if (Q.modal === 'announcements') overlay = announcementsModal();
  if (Q.modal === 'recs-manage') overlay = recManageModal();
  if (Q.modal === 'skin') overlay = skinModal();
  if (Q.modal === 'ms') overlay = microsoftLoginModal();
  if (Q.modal === 'mua') overlay = muaLoginModal();
  return `<div class="app">
    ${titlebar(active)}
    <div class="workspace">${sidebar}<main class="content">${content}</main></div>
    ${overlay}
  </div>`;
}
