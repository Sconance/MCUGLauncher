// Page rendering only; native operations are handled by feature modules.
function modCard(m) {
  return `<a class="mod-card" href="?screen=mod&id=${m.id}">
    <div class="mod-card-top"><span class="mod-icon ${m.color}">${icon(m.ic)}</span><div class="mod-card-id"><div class="mod-card-name">${m.name}</div><div class="mod-card-author">${m.author}</div></div><span class="mod-card-arrow">${icon('chevron-right')}</span></div>
    <div class="mod-card-desc">${m.desc}</div>
    <div class="mod-card-tags">${m.tags.map((t, i) => `<span class="tag ${i === 0 ? 'green' : ''}">${t}</span>`).join('')}</div>
    <div class="mod-card-foot"><span>${icon('download')} ${m.downloads}</span><span>${m.size}</span><span class="mod-open">查看</span></div>
  </a>`;
}

function modListPage() {
  const head = `<div class="dl-head">
    <div class="dl-title-wrap"><div class="dl-big-icon mod">${icon('box')}</div><div><div class="page-title">模组</div><div class="page-sub">安装、更新和管理这个实例的模组。</div></div></div>
    <div class="dl-head-side"><span class="inline">实例 ${escapeHtml(window.state?.selectedInstance || '未选择')}</span><a class="btn btn-primary btn-sm" href="?screen=catalog&type=mod">${icon('plus')} 添加模组</a></div>
  </div>`;
  const search = `<div class="mod-search-row">
    <div class="mod-filters"><span class="filter-chip active">全部</span><span class="filter-chip">已安装</span><span class="filter-chip">可更新</span><span class="filter-chip">客户端</span><span class="filter-chip">服务端</span></div>
    <label class="mod-search">${icon('search')}<input placeholder="搜索模组名称、作者或描述"><span class="search-hint">支持中文与英文</span></label>
  </div>`;
  return shell(
    'download',
    downloadSidebar('mod'),
    `${head}${search}
    <div class="mod-grid">${mods.map(modCard).join('')}</div>
    <div class="dl-footnote"><span class="inline">${icon('info')} 点击卡片查看详情；下载前会检查加载器与游戏版本。</span><span>共 ${mods.length} 个结果 · 按相关度排序</span></div>`
  );
}

function catalogPage(type = 'mod', query = '') {
  const meta = {
    vanilla: {
      title: '原版游戏',
      sub: '安装 Mojang 官方正式版，不修改现有实例。',
      icon: 'grid',
      code: 'grid',
    },
    mod: { title: '模组', sub: '从 Modrinth 搜索并下载到当前实例。', icon: 'box', code: 'mod' },
    modpack: {
      title: '整合包',
      sub: '下载整合包后，在安装与导入页面选择文件。',
      icon: 'layers',
      code: 'layers',
    },
    resourcepack: {
      title: '资源包',
      sub: '下载后放入当前实例的 resourcepacks。',
      icon: 'image',
      code: 'image',
    },
    shader: { title: '光影', sub: '下载后放入当前实例的 shaderpacks。', icon: 'sun', code: 'sun' },
    datapack: {
      title: '数据包',
      sub: '保存到下载目录，随后导入具体存档的 datapacks 文件夹。',
      icon: 'database',
      code: 'database',
    },
  }[type] || { title: '下载', sub: '浏览可下载内容。', icon: 'download', code: 'download' };

  if (type === 'vanilla') {
    const items = state.vanilla || [];
    const body = !items.length
      ? `<div class="catalog-empty">${icon('loader')}<b>正在读取原版版本列表</b><span>需要访问 Mojang 版本清单；失败时可稍后重试。</span></div>`
      : `<div class="vanilla-grid">${items.map((v) => `<article class="vanilla-row"><div class="vanilla-id"><b>${v.id}</b><span>${v.released}</span></div><span class="tag">正式版</span><button class="btn btn-primary btn-sm" data-install-vanilla="${v.id}">${icon('download')} 安装</button></article>`).join('')}</div>`;
    return shell(
      'download',
      downloadSidebar('vanilla'),
      `
      <div class="catalog-page">
        <div class="dl-head"><div class="dl-title-wrap"><div class="dl-big-icon mod">${icon('grid')}</div><div><div class="page-title">${meta.title}</div><div class="page-sub">${meta.sub}</div></div></div><button class="btn btn-ghost btn-sm" data-reload-vanilla>${icon('refresh')} 刷新列表</button></div>
        ${body}
      </div>`
    );
  }

  const cached = state.catalog[type];
  const loading = state.catalogLoading[type];
  const items = cached || [];
  let body;
  if (loading || cached === undefined) {
    body = `<div class="catalog-grid">${[1, 2, 3, 4, 5, 6].map(() => `<div class="content-card skeleton"><div class="sk sk-line" style="width:44%;height:18px"></div><div class="sk sk-line" style="width:88%;margin-top:18px"></div><div class="sk sk-line" style="width:70%;margin-top:8px"></div><div class="sk sk-line" style="width:52%;margin-top:32px"></div></div>`).join('')}</div>`;
  } else if (!items.length) {
    body = `<div class="catalog-empty">${icon('search')}<b>没有找到内容</b><span>换一个关键词，或确认当前实例的版本与加载器。</span></div>`;
  } else {
    body = `<div class="catalog-grid">${items
      .map(
        (
          item
        ) => `<article class="content-card" data-content-id="${item.id}" data-content-kind="${type}">
      <div class="content-top">${item.icon ? `<img class="content-icon" src="${item.icon}" alt="">` : `<span class="content-icon fallback">${(item.name || 'M').slice(0, 1)}</span>`}<div class="content-id"><b>${item.name}</b><span>${item.author || 'Modrinth'}</span></div></div>
      <div class="content-desc">${item.description || ''}</div>
      <div class="content-tags">${(item.tags || '')
        .split(' · ')
        .filter(Boolean)
        .slice(0, 3)
        .map((t) => `<span class="tag">${t}</span>`)
        .join('')}</div>
      <div class="content-foot"><span>${icon('download')} ${Number(item.downloads || 0).toLocaleString()}</span><button class="btn btn-soft btn-sm" data-content-install="${item.id}" data-content-category="${type}">${type === 'modpack' ? '下载整合包' : '下载'}</button></div>
    </article>`
      )
      .join('')}</div>`;
  }

  return shell(
    'download',
    downloadSidebar(type),
    `
    <div class="catalog-page">
      <div class="dl-head"><div class="dl-title-wrap"><div class="dl-big-icon mod">${icon(meta.icon)}</div><div><div class="page-title">${meta.title}</div><div class="page-sub">${meta.sub}</div></div></div><span class="inline">${state.selectedInstance || '当前实例'} · ${state.selectedVersion || ''}</span></div>
      <div class="catalog-search-row"><div class="catalog-search">${icon('search')}<input id="catalog-query" placeholder="搜索${meta.title}名称、作者或描述" value="${query}"><button class="btn btn-primary btn-sm" data-content-search="${type}">搜索</button></div></div>
      <div class="catalog-custom-filters"><label>游戏版本<input class="input" data-catalog-game placeholder="留空不限版本" value="${escapeHtml(state.catalogFilters?.[type]?.game || '')}"></label><label>加载器<select class="select" data-catalog-loader>${['', 'vanilla', 'fabric', 'forge', 'neoforge', 'quilt'].map((value) => `<option value="${value}" ${value === (state.catalogFilters?.[type]?.loader || '') ? 'selected' : ''}>${value || '不限'}</option>`).join('')}</select></label></div>
      ${body}
      <div class="dl-footnote">${icon('info')} 内容来自 Modrinth；下载前会检查当前实例的游戏版本与加载器。整合包仅下载 .mrpack 文件。</div>
    </div>`
  );
}

function modDetailPage() {
  if (window.chrome?.webview) {
    const m = mods.find((x) => x.id === Q.id) || mods[0];
    const safe = (value) =>
      String(value ?? '').replace(
        /[&<>"']/g,
        (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[c]
      );
    return shell(
      'download',
      downloadSidebar('mod'),
      `<a class="back-link" href="?screen=mods">返回模组列表</a><section class="card" style="padding:24px;margin-top:16px"><h2>${safe(m?.name || '未选择本地模组')}</h2><p>${safe(m?.desc || '请选择本地模组，或到下载分类搜索资源。')}</p><p>${safe(m?.v || '')}</p><button class="btn btn-ghost" data-open-folder="mods">打开模组目录</button><a class="btn btn-primary" href="?screen=catalog&type=mod">搜索资源</a></section>`
    );
  }
  const m = mods.find((x) => x.id === Q.id) || mods[0];
  const local = Q.mode === 'local';
  const shots = [0, 1, 2]
    .map(
      (i) =>
        `<div class="shot ${i === 0 ? 'on' : ''}" style="filter:hue-rotate(${i * 18}deg)">${QuotePhoto()}</div>`
    )
    .join('');
  return shell(
    'download',
    downloadSidebar('mod'),
    `
    <div class="detail-back"><a class="back-link" href="?screen=mods">${icon('chevron-left')} 返回模组列表</a><span class="inline">1.20.1-OptiFine_I6 · Fabric</span></div>
    <div class="mod-detail">
      <div class="mod-detail-main">
        <div class="mod-hero">
          <span class="mod-icon big ${m.color}">${icon(m.ic)}</span>
          <div class="mod-hero-copy"><div class="mod-hero-title">${m.name} <span class="mod-verified">${icon('check')} 已校验</span></div><div class="mod-hero-by">作者：${m.author}</div><div class="mod-card-tags">${m.tags.map((t, i) => `<span class="tag ${i === 0 ? 'green' : ''}">${t}</span>`).join('')}</div></div>
          <div class="mod-hero-stats"><span>${icon('download')} ${m.downloads}</span><span>${icon('clock')} 2026-10-28</span></div>
        </div>
        <div class="detail-tabs"><span class="detail-tab active">介绍</span><span class="detail-tab">版本</span><span class="detail-tab">依赖</span></div>
        <div class="shot-strip">${shots}</div>
        <div class="detail-copy"><b>关于 ${m.name}</b><p>${m.desc} 适用于 Minecraft 1.20.1 与 Fabric 加载器，安装后无需额外配置。</p><p>此页面展示的版本、依赖与下载地址由模组源提供；启动器只负责安装到目标实例。</p></div>
        <div class="detail-log"><div class="detail-log-title">更新记录</div>
          <div class="log-row"><b>${m.v}</b><span>适配 Minecraft 1.20.1，修复与部分光影的渲染冲突。</span><em>2026-10-28</em></div>
          <div class="log-row"><b>${m.v.replace(/\d+$/, (n) => Math.max(0, Number(n) - 1))}</b><span>优化启动时的资源加载，减少首次进入世界的卡顿。</span><em>2026-09-14</em></div>
        </div>
      </div>
      <aside class="mod-install card">
        <div class="mod-install-title">${local ? '下载选项' : '安装选项'} <span class="install-either">二选一</span></div>
        <div class="install-mode">
          <a class="${local ? '' : 'active'}" href="?screen=mod&id=${m.id}">安装到实例</a>
          <a class="${local ? 'active' : ''}" href="?screen=mod&id=${m.id}&mode=local">自定义位置</a>
        </div>
        ${
          local
            ? `
          <label class="field-label">模组版本</label>
          <div class="select-like">${m.v} + mc1.20.1 ${icon('chevron-down')}</div>
          <label class="field-label">保存位置</label>
          <div class="path-pick"><label class="path-field">${icon('folder')}<input value="D:\\MCUG\\Downloads\\mods" aria-label="下载目录"></label><button class="btn btn-ghost btn-sm">选择文件夹</button></div>
          <div class="local-summary">${icon('download')}<div><b>${m.id}-fabric-${m.v}.jar</b><span>${m.size} · 下载后不自动安装</span></div></div>
          <button class="btn btn-primary mod-install-btn">${icon('download')} 下载到本地</button>
          <div class="mod-install-note">只下载 .jar 文件，不检查前置依赖，也不写入实例；下载后需手动放入 mods 文件夹。</div>
        `
            : `
          <label class="field-label">目标实例</label>
          <div class="select-like">1.20.1-OptiFine_I6 ${icon('chevron-down')}</div>
          <label class="field-label">模组版本</label>
          <div class="select-like">${m.v} + mc1.20.1 ${icon('chevron-down')}</div>
          <div class="dep-title">前置依赖</div>
          <div class="dep-row"><span class="dep-check">${icon('check')}</span><span>Fabric API</span><span class="dep-ver">0.92.2+1.20.1</span></div>
          <div class="dep-row"><span class="dep-check">${icon('check')}</span><span>Fabric Loader</span><span class="dep-ver">0.15.11</span></div>
          <button class="btn btn-primary mod-install-btn">${icon('download')} 下载并安装</button>
          <div class="mod-install-note">安装前会备份当前模组列表；可随时在版本内容管理中禁用或删除。</div>
        `
        }
        <div class="file-info"><div class="file-info-title">文件信息</div><div><span>${local ? '保存文件名' : '文件名'}</span><b>${m.id}-fabric-${m.v}.jar</b></div><div><span>文件大小</span><b>${m.size}</b></div><div><span>加载器</span><b>Fabric 0.15.11</b></div><div><span>许可证</span><b>LGPL-3.0</b></div></div>
      </aside>
    </div>`
  );
}
