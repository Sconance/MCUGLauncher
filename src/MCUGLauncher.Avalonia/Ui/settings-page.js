// Page rendering only; native operations are handled by feature modules.
function settingsPage(panel = 'launch') {
  const card = (title, desc, body, foot = '') =>
    `<section class="card settings-card"><div class="settings-card-head"><div><div class="settings-card-title">${title}</div><div class="settings-card-desc">${desc}</div></div>${foot}</div>${body}</section>`;
  const field = (label, input) =>
    `<div class="field"><div class="field-label">${label}</div>${input}</div>`;
  const serverRows =
    (state.servers || [])
      .map(
        (s) =>
          `<div class="server-manage-row"><div class="server-manage-copy"><b>${s.name || s.address}</b><span>${s.address}</span></div><div class="server-manage-actions"><button class="btn btn-ghost btn-sm" data-ping-server="${s.address}">查询</button><button class="btn btn-ghost btn-sm" data-server-menu="${s.id}">管理</button></div></div>`
      )
      .join('') || `<div class="empty-line">还没有收藏服务器。</div>`;

  let body = '';
  let title = '启动设置';
  let sub = '游戏目录、Java 与默认启动参数。';

  if (panel === 'java') {
    title = 'Java 管理';
    sub = '扫描本机 Java，并指定启动时优先使用的版本。';
    const rows =
      (state.javaList || [])
        .map(
          (j) =>
            `<div class="java-row ${j.error ? 'bad' : ''}"><div><b>Java ${j.major || '?'} · ${j.arch || ''} ${j.vendor || ''}</b><span>${j.path}</span>${j.error ? `<em>${j.error}</em>` : ''}</div><button class="btn btn-ghost btn-sm" data-use-java="${j.path}">使用</button></div>`
        )
        .join('') || '<div class="empty-line">还没有扫描结果。点击「扫描本机 Java」。</div>';
    body =
      card(
        'Java 运行时',
        '留空时由启动核心按版本自动选择；指定后优先使用该路径。',
        `
      ${field('当前 Java 路径', `<div class="path-row"><input class="input" data-setting-java value="${state.java || ''}" placeholder="留空自动选择"><button class="btn btn-ghost" data-pick="java">选择</button></div>`)}
      <div class="settings-actions"><button class="btn btn-ghost" data-scan-java>扫描本机 Java</button><button class="btn btn-primary" data-save-launch>保存</button></div>`
      ) +
      card(
        '扫描结果',
        '列出本机常见位置中存在的 java.exe；不会修改游戏版本。',
        `<div class="java-list" id="java-list">${rows}</div>`
      );
  } else if (panel === 'network') {
    title = '下载与网络';
    sub = '数据源、超时与网络诊断。';
    body =
      card(
        '社区数据源',
        '公告、服务器、推荐、语录分别读取独立 JSON。',
        `
      <div class="info-row"><span>公告</span><code>${state.dataSources?.announcements || ''}</code></div>
      <div class="info-row"><span>服务器</span><code>${state.dataSources?.servers || ''}</code></div>
      <div class="info-row"><span>推荐</span><code>${state.dataSources?.recommendations || ''}</code></div>
      <div class="info-row"><span>语录</span><code>${state.dataSources?.quotes || ''}</code></div>
      <div class="settings-actions"><a class="btn btn-primary" href="?screen=datasource">打开数据源设置</a></div>`
      ) +
      card(
        '连接测试',
        '测试当前默认社区地址是否可访问。',
        `<div class="settings-actions"><button class="btn btn-ghost" data-test-all>测试四类地址</button><button class="btn btn-ghost" data-open-folder="root">打开游戏目录</button></div>`
      );
  } else if (panel === 'appearance') {
    title = '界面与背景';
    sub = '蓝白主题与窗口显示。';
    body = card(
      '主题',
      '默认使用蓝白主题。',
      `
      <div class="theme-preview"><span class="theme-dot"></span><div><b>蓝白 · 默认</b><span>#006DB8 强调色 / #F3F7F9 页面底色</span></div><span class="tag">当前</span></div>
      <div class="settings-actions"><button class="btn btn-ghost" data-reset-window>恢复默认窗口 1200×780</button></div>`
    );
  } else if (panel === 'servers') {
    title = '服务器收藏';
    sub = '收藏的服务器会出现在主页右侧。';
    body = card(
      '已收藏',
      `${(state.servers || []).length} 台服务器，状态由 SLP 实时查询。`,
      `<div class="server-manage-list">${serverRows}</div><div class="settings-actions"><a class="btn btn-primary" href="?screen=home&modal=add-server">添加服务器</a><a class="btn btn-ghost" href="?screen=home">返回主页</a></div>`
    );
  } else if (panel === 'saves') {
    title = '存档保护';
    sub = '打开存档目录，或把当前存档打包备份。';
    body = card(
      '存档备份',
      '备份会写到本地数据目录的 backups 文件夹，不修改原存档。',
      `
      <div class="settings-actions"><button class="btn btn-primary" data-backup-saves>立即备份</button><button class="btn btn-ghost" data-open-folder="saves">打开存档文件夹</button></div>
      <div class="inline-note">备份按当前实例分别保存，恢复会创建新存档。</div><div data-save-backups></div>`
    );
  } else if (panel === 'instances') {
    title = '实例维护';
    sub = '克隆、导出、卸载范围与内容更新预览。';
    body = card(
      '维护目标实例',
      '操作参数在提交时固定，执行进度在右上角查看。',
      '<div data-instance-tools></div>'
    );
  } else if (panel === 'bookmarks') {
    title = '我的收藏';
    sub = '收藏保存在本地，可以继续下载或取消收藏。';
    body = card('资源收藏', '下载时按当前实例重新检查兼容版本。', '<div data-bookmark-list></div>');
  } else if (panel === 'games') {
    title = '运行中的游戏';
    sub = '各个游戏独立运行，关闭前会再次确认。';
    body = card('游戏会话', '关闭启动器不会自动关闭游戏。', '<div data-games-list></div>');
  } else if (panel === 'install') {
    title = '安装与导入';
    sub = '使用现有 Core 安装加载器或导入整合包，执行进度在下载任务中查看。';
    body = card(
      '游戏安装',
      '支持 Fabric、Quilt、Forge 和 NeoForge。',
      '<div data-install-tools></div>'
    );
  } else if (panel === 'tasks') {
    title = '下载任务';
    sub = '下载任务独立于当前页面，支持排队、暂停、取消和重试。';
    body = card(
      '下载队列',
      '中断的任务可以继续；取消保留已校验缓存。',
      '<div data-task-list></div>'
    );
  } else if (panel === 'recovery') {
    title = '任务与恢复';
    sub = '窗口、界面缓存与配置目录。';
    body = card(
      '恢复操作',
      '用于窗口尺寸异常或界面资源损坏时。',
      `
      <div class="settings-actions"><button class="btn btn-ghost" data-reset-window>恢复窗口尺寸</button><button class="btn btn-ghost" data-reset-ui>重新加载界面</button><button class="btn btn-ghost" data-open-folder="config">打开配置目录</button></div>
      <div class="inline-note">重新加载不会删除账号、服务器或社区数据。</div>`
    );
  } else if (panel === 'logs') {
    title = '运行日志';
    sub = '最近 400 行启动与启动游戏记录。';
    body = card(
      '日志',
      '日志保存在 %LOCALAPPDATA%\\MCUGLauncher\\launcher.log。',
      `
      <div class="settings-actions"><button class="btn btn-primary" data-read-log>刷新日志</button><button class="btn btn-ghost" data-open-folder="config">打开日志目录</button></div>
      <pre class="log-box" id="log-box">${state.logText || '点击「刷新日志」读取。'}</pre>`
    );
  } else if (panel === 'diagnose') {
    title = '诊断与反馈';
    sub = '查看运行环境，定位启动或显示问题。';
    body = card(
      '系统信息',
      '反馈问题时请附上这些信息。',
      `
      <div class="settings-actions"><button class="btn btn-primary" data-system-info>读取系统信息</button><button class="btn btn-ghost" data-copy-diag>复制</button></div>
      <pre class="log-box" id="diag-box">${state.diagText || '点击「读取系统信息」。'}</pre>`
    );
  } else if (panel === 'about') {
    title = '关于与许可';
    sub = 'MCUG Launcher ' + appVersion() + '。';
    body = card(
      'MCUG Launcher',
      '面向 Windows 的 Minecraft Java 版启动器。',
      `
      <div class="about-hero"><img src="assets/logo.png" alt=""><div><b>MCUG Launcher</b><span>${appVersion()} · WPF + WebView2</span></div></div>
      <div class="info-row"><span>社区数据仓库</span><a href="https://github.com/waibibabu1239/MCUGL-storage" target="_blank" rel="noreferrer">waibibabu1239/MCUGL-storage</a></div>
      <div class="info-row"><span>管理员文档</span><a href="docs/admin-and-community.md" target="_blank">docs/admin-and-community.md</a></div>
      <div class="info-row"><span>许可</span><span>沿用 MCUG Launcher 原仓库策略</span></div>`
    );
  } else {
    body = card(
      '启动设置',
      '修改后会写入本机配置；下次启动生效。',
      `
      ${field('游戏根目录', `<div class="path-row"><input class="input" data-setting-root value="${state.root || ''}"><button class="btn btn-ghost" data-pick="root">选择</button></div>`)}
      ${field('Java 路径（可留空）', `<div class="path-row"><input class="input" data-setting-java value="${state.java || ''}" placeholder="留空自动选择"><button class="btn btn-ghost" data-pick="java">选择</button></div>`)}
      <div class="three-grid">
        <label><input type="checkbox" data-setting-auto ${state.autoMemory ? 'checked' : ''}> 自动分配内存</label>
        ${field('最大内存 / MB', `<input class="input" type="number" min="512" step="512" data-setting-ram value="${state.ram || 4096}">`)}
        ${field('游戏窗口宽', `<input class="input" type="number" min="640" data-setting-width value="${state.width || 1280}">`)}
        ${field('游戏窗口高', `<input class="input" type="number" min="480" data-setting-height value="${state.height || 720}">`)}
      </div>
      <div class="settings-actions"><button class="btn btn-primary" data-save-launch>保存设置</button><button class="btn btn-ghost" data-open-launch-options>高级启动设置</button><a class="btn btn-ghost" href="?screen=settings&panel=java">Java 管理</a></div>`
    );
  }

  return shell(
    'settings',
    settingsSidebar(panel),
    `<div class="settings-content">${pageHead(title, sub)}${body}</div>`
  );
}
