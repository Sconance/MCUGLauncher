// Page rendering only; native operations are handled by feature modules.
function versionManagePage() {
  const managed = mods;
  const updateFor = (id) => mods.find((item) => item.id === id)?.update || '';
  const current = instances.find((i) => i.name === Q.inst) ||
    instances[0] || { name: '未选择实例', loader: '', mods: 0, updates: 0 };
  const visible = managed;
  const rows = visible
    .map((m) => {
      const disabled = m.enabled === false;
      const update = updateFor(m.id);
      const state = disabled
        ? '<span class="mstat off">已禁用</span>'
        : '<span class="mstat on"><i></i>已启动</span>';
      const actions = disabled
        ? `<button class="mact start" data-mod-action="enable">${icon('play')} 启动</button><button class="micon danger" title="删除模组">${icon('trash')}</button>`
        : `${update ? `<button class="mact update" data-mod-action="update">${icon('refresh')} 更新</button><button class="mact quiet" data-mod-action="pin" disabled title="固定更新尚未接入">禁止更新</button>` : ''}<button class="mact quiet" data-mod-action="disable">禁用</button><button class="micon danger" title="删除模组">${icon('trash')}</button>`;
      return `<div class="mtable-row">
      <div class="mfile"><span class="mod-icon ${m.color}">${icon(m.ic)}</span><div><b>${m.name}</b><span>${m.file || `${m.id}-fabric-${m.v}.jar`} · ${m.size}</span></div></div>
      <div class="mver">${m.v}</div>
      <div>${state}</div>
      <div>${update ? `<span class="update-tag">可更新至 ${update}</span>` : '<span class="inline">尚未检查更新</span>'}</div>
      <div class="mactions">${actions}</div>
    </div>`;
    })
    .join('');
  const switchMenu = Q.switchMenu
    ? `<div class="instance-menu">
    <div class="instance-menu-head"><b>切换实例</b><span>${instances.length} 个</span></div>
    ${instances.map((i) => `<a class="instance-option ${i.name === current.name ? 'active' : ''}" href="?screen=manage&inst=${encodeURIComponent(i.name)}"><span class="instance-check">${i.name === current.name ? icon('check') : ''}</span><span class="instance-option-copy"><b>${i.name}</b><span>${i.loader} · ${i.mods} 个模组</span></span></a>`).join('')}
    <a class="instance-menu-foot" href="?screen=home&panel=versions">${icon('folder')} 打开游戏目录 ${icon('chevron-right')}</a>
  </div>`
    : '';
  return shell(
    'home',
    '',
    `<div class="vm-head">
    <a class="back-link" href="?screen=home">${icon('chevron-left')} 返回启动</a>
    <div class="vm-title"><span class="vm-icon">${icon('folder')}</span><div><div class="page-title">版本内容管理</div><div class="page-sub">${current.mods} 个模组 · ${current.updates} 个可更新</div></div></div>
    <div class="vm-instance"><span class="inst-label">当前实例</span><a class="instance-pill ${Q.switchMenu ? 'open' : ''}" href="?screen=manage&inst=${encodeURIComponent(current.name)}&switch=${Q.switchMenu ? '0' : '1'}"><span class="inst-cube">${icon('box')}</span><span class="inst-copy"><b>${current.name}</b><span>${current.loader}</span></span>${icon('chevron-down')}</a>${switchMenu}</div>
    <div class="vm-actions"><button class="btn btn-ghost btn-sm">${icon('folder')} 打开文件夹</button><a class="btn btn-soft btn-sm" href="?screen=version&v=${encodeURIComponent(current.name)}">${icon('settings')} 版本设置</a></div>
  </div>
  <div class="vm-tabs">${['模组 ' + current.mods, '资源包 0', '光影 0', '存档 0', '服务器 0', '截图 0', '蓝图 0'].map((t, i) => `<span class="vm-tab ${i === 0 ? 'active' : ''}">${t}</span>`).join('')}</div>
  <div class="vm-toolbar"><div><b>已安装模组</b><span class="inline">启动游戏时只加载已启用的模组</span></div><div class="vm-tools"><label class="mini-search">${icon('search')}<input placeholder="搜索模组"></label><span class="filter-chip active">全部</span><span class="filter-chip">可更新</span><span class="filter-chip">已禁用</span></div></div>
  <div class="mtable"><div class="mtable-head"><span>模组</span><span>版本</span><span>状态</span><span>更新</span><span>操作</span></div>${rows}</div>
  <div class="vm-foot"><span class="inline">${icon('info')} 删除操作只移除当前实例中的模组文件，不会影响其他版本。</span><span>共 ${current.mods} 个 · 显示 ${visible.length} 个</span></div>`
  );
}

function versionSettingsPage() {
  if (window.chrome?.webview) {
    const key = Q.v || window.state?.selectedInstance || '';
    const instance = instances.find((item) => item.name === key);
    return shell(
      'home',
      '',
      `<a class="back-link" href="?screen=home">返回启动页</a><section class="card" style="padding:24px;margin-top:16px">
      <h2>${escapeHtml(instance?.displayName || key || '未选择实例')}</h2>
      <p>${escapeHtml(instance?.minecraft || '')} · ${escapeHtml(instance?.loader || '请先选择已安装实例')}</p>
      <button class="btn btn-primary" data-open-launch-options data-version="${escapeHtml(key)}">编辑此实例启动设置</button><p>可继承全局设置，或独立设置 Java、内存、窗口与 JVM 参数。</p>
      <a class="btn btn-primary" href="?screen=settings&panel=instances&v=${encodeURIComponent(key)}">维护此实例</a>
      <a class="btn btn-ghost" href="?screen=manage&inst=${encodeURIComponent(key)}">管理本地模组</a></section>`
    );
  }
  return shell(
    'home',
    '',
    `<div class="vm-head">
    <a class="back-link" href="?screen=manage">${icon('chevron-left')} 返回版本内容管理</a>
    <div class="vm-title"><span class="vm-icon">${icon('box')}</span><div><div class="page-title">版本设置</div><div class="page-sub">1.20.1-OptiFine_I6 · Minecraft 1.20.1 · optifine HD U I6</div></div></div>
    <div class="vm-actions"><button class="btn btn-primary btn-sm">${icon('play')} 启动此版本</button></div>
  </div>
  <div class="vs-layout">
    <aside class="vs-nav"><span class="vs-item active">${icon('info')} 概览</span><span class="vs-item">${icon('settings')} 设置</span><span class="vs-item">${icon('edit')} 修改</span><span class="vs-item">${icon('external')} 导出</span></aside>
    <div class="vs-content">
      <section class="card vs-card vs-hero"><span class="mod-icon big green">${icon('box')}</span><div><div class="vs-name">1.20.1-OptiFine_I6</div><div class="vs-meta">Minecraft 1.20.1 · optifine HD U I6</div><div class="mod-card-tags"><span class="tag green">Fabric</span><span class="tag">OptiFine</span><span class="tag">已安装</span></div></div><span class="vs-stamp">${icon('check')} 文件完整</span></section>
      <section class="card vs-card"><div class="vs-card-title">个性化</div>
        <div class="vs-form-row"><label>图标</label><div class="select-like">自动 ${icon('chevron-down')}</div><label>分类</label><div class="select-like">自动 ${icon('chevron-down')}</div><button class="btn btn-soft btn-sm">保存分类</button></div>
        <div class="vs-chips"><button>修改版本名</button><button>修改版本描述</button><button>加入收藏夹</button><button>自定义分类</button></div>
      </section>
      <section class="card vs-card"><div class="vs-card-title">快捷方式</div><div class="vs-chips"><button>${icon('folder')} 存档与截图库</button><button>版本文件夹</button><button>存档文件夹</button><button>Mod 文件夹</button><button>截图文件夹</button></div></section>
      <section class="card vs-card"><div class="vs-card-title">启动概览</div><div class="vs-launch-grid"><div><span>Java</span><b>${window.state?.java || '自动选择'}</b></div><div><span>内存</span><b>${window.state?.ram || 4096} MB</b></div><div><span>游戏窗口</span><b>${window.state?.width || 1280} × ${window.state?.height || 720}</b></div><div><span>运行状态</span><b>请查看运行中的游戏</b></div></div></section>
      <section class="card vs-card"><div class="vs-card-title">高级管理</div><div class="vs-chips"><button>${icon('external')} 导出启动脚本</button><button>${icon('refresh')} 补全文件</button><button class="danger-text" data-delete-version="${Q.v || ''}">${icon('trash')} 删除版本</button></div><div class="vs-note">删除操作会先显示完整卸载范围，再次确认后执行。</div></section>
    </div>
  </div>`
  );
}
