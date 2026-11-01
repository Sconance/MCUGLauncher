(() => {
  const bridge = window.chrome?.webview;
  let state = null;
  let toasting = 0;

  const post = (message) => {
    try {
      bridge?.postMessage(message);
    } catch (_) {
      /* window is closing */
    }
  };

  const esc = (value) =>
    String(value ?? '')
      .replace(/&/g, '&amp;')
      .replace(/</g, '&lt;')
      .replace(/>/g, '&gt;')
      .replace(/"/g, '&quot;')
      .replace(/'/g, '&#39;');

  const asArray = (value) => (Array.isArray(value) ? value : []);

  function toast(message, bad = false) {
    if (!message) return;
    let node = document.querySelector('.host-toast');
    if (!node) {
      node = document.createElement('div');
      node.className = 'host-toast';
      document.body.appendChild(node);
    }
    node.textContent = message;
    node.classList.toggle('bad', bad);
    node.classList.add('show');
    clearTimeout(toasting);
    toasting = setTimeout(() => node.classList.remove('show'), 3200);
  }

  window.mcugToast = toast;

  function applyState(next) {
    state = Object.assign({}, state, next || {});
    if (Array.isArray(next.versionGroups)) {
      versionGroups.splice(
        0,
        versionGroups.length,
        ...next.versionGroups.map((group) => ({
          label: group.label,
          count: group.count,
          versions: asArray(group.versions).map((pair) => [pair?.[0] ?? '未命名', pair?.[1] ?? '']),
        }))
      );
    }
    if (Array.isArray(next.instances)) {
      instances.splice(
        0,
        instances.length,
        ...next.instances.map((item) => ({
          name: item.name,
          loader: item.loader || '原版',
          displayName: item.displayName || item.name,
          minecraft: item.minecraft || '',
          mods: Number(item.mods) || 0,
          updates: Number(item.updates) || 0,
        }))
      );
    }
    if (Array.isArray(next.mods)) {
      mods.splice(
        0,
        mods.length,
        ...next.mods.map((item, index) => ({
          id: item.id || `local-${index}`,
          name: item.name || item.file || '本地模组',
          author: item.author || '本地',
          desc: item.desc || '',
          tags: asArray(item.tags).length ? item.tags : ['本地'],
          size: item.size || '—',
          downloads: item.downloads || '',
          v: item.v || '—',
          color: item.color || 'slate',
          ic: item.ic || 'box',
          file: item.file || '',
          path: item.path || '',
          enabled: item.enabled !== false,
          update: item.update || '',
        }))
      );
    }
    if (Array.isArray(next.servers)) {
      const colors = ['green', 'blue', 'slate'];
      servers.splice(
        0,
        servers.length,
        ...next.servers.map((item, index) => ({
          id: item.address || `server-${index}`,
          name: item.name || item.address || '未命名服务器',
          address: item.address || '',
          version: '—',
          motd: '正在读取服务器状态…',
          players: 0,
          max: 0,
          ping: 0,
          online: false,
          color: colors[index % colors.length],
          icon: index % 3 === 2 ? 'globe' : 'server',
        }))
      );
    }
    if (!new URLSearchParams(location.search).get('inst') && next.selectedInstance)
      Q.inst = next.selectedInstance;
    window.state = Object.assign(window.state || {}, next, {
      servers,
      announcements,
      recs,
      quotes,
      mods,
      dataSources: next.dataSources || window.state?.dataSources || {},
    });
    render();
    applyAccount();
    applySources(next);
    requestPings();
  }

  function applySources(next) {
    if (!document.querySelector('.ds-rows')) return;
    const sources = next.dataSources || {};
    const values = [
      sources.announcements,
      sources.servers,
      sources.recommendations,
      sources.quotes,
    ];
    const inputs = document.querySelectorAll('.ds-rows .ds-row input');
    inputs.forEach((input, index) => {
      if (typeof values[index] === 'string') input.value = values[index];
    });
    const write = document.querySelector('.ds-write input');
    if (write && next.writeEndpoint) write.value = next.writeEndpoint;
  }

  function applyAccount() {
    const player = state?.player || 'Steve';
    if (!new URLSearchParams(location.search).get('inst') && state?.selectedInstance)
      Q.inst = state.selectedInstance;
    const name = document.querySelector('.acc-name');
    if (name) name.textContent = player;
    const tabs = document.querySelectorAll('.acc-tab');
    const accountType = state?.accountType || 'offline';
    const activeTab = accountType === 'microsoft' ? 0 : accountType === 'mua' ? 2 : 1;
    tabs.forEach((tab, index) => tab.classList.toggle('active', index === activeTab));
    const meta = document.querySelector('.acc-meta');
    if (meta) meta.textContent = state?.accountLabel || '离线账号 · 本地启动';
    const button = document.querySelector('.acc-btn');
    if (button) button.textContent = accountType === 'offline' ? '切换本地账号' : '切换账号';
    const subtitle = document.querySelector('.launch-text small');
    if (subtitle) {
      const version = state?.selectedVersion || state?.selectedInstance;
      subtitle.textContent = version
        ? `${version} · ${state?.autoMemory ? '自动内存' : (state?.ram || 4096) + ' MB'}`
        : '选择实例后启动';
    }
    const versionTitle = document.querySelector('.vs-name');
    if (versionTitle && state?.selectedVersion) versionTitle.textContent = state.selectedVersion;
  }

  function requestPings() {
    if (!document.querySelector('.server-rail')) return;
    servers.forEach((server) => {
      if (server.address) post({ type: 'pingServer', address: server.address, id: server.address });
    });
  }

  function parsePayload(raw) {
    try {
      const parsed = typeof raw === 'string' ? JSON.parse(raw) : raw;
      if (Array.isArray(parsed)) return parsed;
      return asArray(parsed?.items || parsed?.list || parsed?.data);
    } catch (_) {
      return [];
    }
  }

  function normalizeAnnouncement(item, index) {
    const rawDate = item.publishedAt || item.date || '';
    const date = rawDate ? new Date(rawDate) : null;
    const valid = date && !Number.isNaN(date.getTime());
    return {
      id: item.id || `ann-${index}`,
      tag: item.tag || '公告',
      title: item.title || '未命名公告',
      text: item.body || item.text || '',
      time: valid ? date.toLocaleString('zh-CN', { hour12: false }) : '',
      date: valid ? `${date.getMonth() + 1}/${String(date.getDate()).padStart(2, '0')}` : '—/—',
      image: item.imageUrl || '',
    };
  }

  function normalizeRecommendation(item, index) {
    return {
      id: item.id || `rec-${index}`,
      type: ['server', 'material', 'shader'].includes(item.type) ? item.type : 'server',
      name: item.name || '未命名推荐',
      author: item.author || '群友',
      desc: item.description || item.desc || '',
      tags: asArray(item.tags).slice(0, 3),
      version: item.version || '—',
      size: item.size || '—',
      url: item.url || item.address || '',
      address: item.address || item.url || '',
      online: false,
      players: 0,
      max: 0,
      ping: 0,
    };
  }

  function applyCommunity(payload) {
    if (!payload) return;
    state.communityUpdated = new Date().toLocaleTimeString('zh-CN', { hour12: false });
    window.state.communityUpdated = state.communityUpdated;
    if (payload.announcements) {
      const items = parsePayload(payload.announcements).map(normalizeAnnouncement);
      if (Array.isArray(items)) announcements.splice(0, announcements.length, ...items);
    }
    if (payload.recommendations) {
      const items = parsePayload(payload.recommendations).map(normalizeRecommendation);
      if (Array.isArray(items)) recs.splice(0, recs.length, ...items);
    }
    if (payload.quotes) {
      const items = parsePayload(payload.quotes).map((item, index) => ({
        id: item.id || `quote-${index}`,
        type: item.type === 'image' ? 'image' : 'text',
        text: item.text || '',
        author: item.author || '群友',
        meta: item.source || '',
        image: item.imageUrl || '',
      }));
      quotes.splice(0, quotes.length, ...items);
    }
    render();
    applyAccount();
  }

  function findModByName(name) {
    return mods.find((item) => item.name === name);
  }

  function decorate() {
    applyAccount();
    document.querySelector('.launch-btn')?.addEventListener('click', () => {
      post({ type: 'launch', version: state?.selectedVersion || '' });
    });

    document.querySelectorAll('.version-row').forEach((row) => {
      const name =
        row.dataset.instanceKey || row.querySelector('.version-copy b')?.textContent?.trim();
      if (!name) return;
      row.querySelector('button.version-icon')?.addEventListener('click', () => {
        post({ type: 'selectVersion', version: name });
        post({ type: 'launch', version: name });
      });
      row.addEventListener('click', (event) => {
        if (event.target.closest('a, button')) return;
        post({ type: 'selectVersion', version: name });
        toast(`已选择 ${name}`);
      });
    });

    document.querySelectorAll('.mtable-row').forEach((row) => {
      const name = row.querySelector('.mfile b')?.textContent?.trim();
      const item = findModByName(name);
      if (!item) return;
      row.querySelectorAll('.mact').forEach((button) => {
        button.addEventListener('click', (event) => {
          event.preventDefault();
          if (button.dataset.modAction === 'update') {
            window.mcugGo?.(
              '?screen=settings&panel=instances&v=' +
                encodeURIComponent(Q.inst || state?.selectedInstance || '')
            );
          } else if (button.dataset.modAction === 'enable') {
            post({ type: 'toggleMod', file: item.file, enabled: true });
          } else if (button.dataset.modAction === 'disable') {
            post({ type: 'toggleMod', file: item.file, enabled: false });
          } else if (button.dataset.modAction === 'pin') {
            toast('固定更新尚未接入，请使用实例维护查看更新预览。');
          }
        });
      });
      row.querySelector('.micon.danger')?.addEventListener('click', () => {
        if (confirm(`删除 ${name}？只会移除当前实例中的模组文件。`))
          post({ type: 'deleteMod', file: item.file });
      });
    });

    const install = document.querySelector('.mod-install-btn');
    if (install) {
      install.addEventListener('click', () => {
        const mode = Q.mode === 'local' ? 'local' : 'instance';
        const path =
          mode === 'local' ? document.querySelector('.path-field input')?.value || '' : '';
        const id = mods.find((item) => item.id === Q.id)?.id || Q.id;
        install.disabled = true;
        post({ type: 'installMod', id, mode, path });
      });
    }

    document.querySelectorAll('.win-btn').forEach((button) => {
      button.addEventListener('click', (event) => {
        event.preventDefault();
        const label = button.getAttribute('aria-label');
        post({
          type: 'window',
          action: label === '关闭' ? 'close' : label === '最大化' ? 'maximize' : 'minimize',
        });
      });
    });
    const bar = document.querySelector('.titlebar');
    if (bar && !bar.dataset.dragBound) {
      bar.dataset.dragBound = '1';
      bar.addEventListener('mousedown', (event) => {
        if (event.button !== 0 || event.target.closest('a,button')) return;
        post({ type: 'drag' });
      });
    }

    document.querySelectorAll('[data-stub]').forEach((node) => {
      node.addEventListener('click', (event) => {
        event.preventDefault();
        toast(`${node.dataset.stub}尚未接入当前版本。`);
      });
    });

    window.McugAccounts?.mount();

    document.querySelector('[data-mua-submit]')?.addEventListener('click', () => {
      const account = document.querySelector('#mua-account')?.value?.trim() || '';
      const password = document.querySelector('#mua-password')?.value || '';
      if (!account || !password) {
        toast('请填写 MUA 账号和密码。', true);
        return;
      }
      const button = document.querySelector('[data-mua-submit]');
      if (button) button.disabled = true;
      post({ type: 'loginMua', account, password });
    });

    document.querySelector('[data-ms-login]')?.addEventListener('click', (event) => {
      event.preventDefault();
      toast('正在打开微软登录页…');
      post({ type: 'loginMicrosoft' });
    });

    const versionLaunch =
      document.querySelector('.vs-layout') && document.querySelector('.vm-actions .btn-primary');
    versionLaunch?.addEventListener('click', () => {
      post({ type: 'launch', version: Q.v || state?.selectedVersion || '' });
    });

    document.querySelectorAll('.vs-chips button').forEach((button) => {
      button.addEventListener('click', () => {
        const text = button.textContent.trim();
        if (text.includes('版本文件夹')) post({ type: 'openFolder', kind: 'version' });
        else if (text.includes('存档')) post({ type: 'openFolder', kind: 'saves' });
        else if (text.includes('Mod 文件夹')) post({ type: 'openFolder', kind: 'mods' });
        else if (text.includes('截图')) post({ type: 'openFolder', kind: 'screenshots' });
        else toast('该版本修改功能尚未接入当前版本。');
      });
    });

    document.querySelector('.vs-form-row .btn')?.addEventListener('click', () => {
      toast('版本分类修改尚未接入当前版本。');
    });

    document.querySelector('.vs-layout .vm-actions .btn-ghost')?.addEventListener('click', () => {
      post({ type: 'openFolder', kind: 'version' });
    });

    document.querySelector('[data-delete-version]')?.addEventListener('click', (event) => {
      const version =
        event.currentTarget.dataset.deleteVersion || Q.v || state?.selectedVersion || '';
      if (!version) return;
      if (confirm(`把版本“${version}”移到回收目录？存档与截图不会删除。`))
        post({ type: 'deleteVersion', version });
    });

    document.querySelectorAll('.vs-nav .vs-item').forEach((item) => {
      item.style.cursor = 'pointer';
      item.addEventListener('click', () => toast('版本设置当前仅开放“概览”。'));
    });

    const chips = [...document.querySelectorAll('.vm-tools .filter-chip')];
    const rows = [...document.querySelectorAll('.mtable-row')];
    const search = document.querySelector('.vm-tools input');
    const filterRows = () => {
      const active = document.querySelector('.vm-tools .filter-chip.active')?.textContent || '全部';
      const query = (search?.value || '').trim().toLowerCase();
      rows.forEach((row) => {
        let show = !query || row.textContent.toLowerCase().includes(query);
        if (active.includes('可更新')) show = show && !!row.querySelector('.update-tag');
        if (active.includes('已禁用')) show = show && row.textContent.includes('已禁用');
        row.style.display = show ? '' : 'none';
      });
    };
    chips.forEach((chip) =>
      chip.addEventListener('click', () => {
        chips.forEach((item) => item.classList.remove('active'));
        chip.classList.add('active');
        filterRows();
      })
    );
    search?.addEventListener('input', filterRows);

    document.querySelector('.dl-head-side .btn-primary')?.addEventListener('click', () => {
      search?.focus();
      toast('在搜索框中输入模组名称，或从列表中选择。');
    });

    document
      .querySelectorAll(
        '.skin-modal .btn:not([data-pick-offline-skin]):not([data-default-skin]):not([data-refresh-avatar])'
      )
      .forEach((button) => {
        button.disabled = true;
        button.title = '皮肤站绑定与上传尚未接入当前版本';
      });

    decorateAddServer();
    decorateDataSources();
    decorateKeys();
    decoratePublish();
    window.McugSettings?.mount();
    bindCatalog();
    loadCatalog();
  }

  let catalogSequence = 0;
  function fetchCatalog(type, query) {
    if (!state || Q.screen !== 'catalog') return;
    state.catalogRequests ||= {};
    const requestId = (state.catalogRequests[type] = String(++catalogSequence));
    post(
      type === 'vanilla'
        ? { type: 'vanillaVersions' }
        : {
            type: 'searchContent',
            category: type,
            query: query || '',
            requestId,
            game: state.catalogFilters?.[type]?.game || '',
            loader: state.catalogFilters?.[type]?.loader || '',
          }
    );
  }

  function loadCatalog(force = false) {
    if (!state || Q.screen !== 'catalog') return;
    state.catalog ||= {};
    state.catalogLoading ||= {};
    state.vanilla ||= [];
    window.state = state;
    const type = Q.type || 'mod';
    if (!force && (state.catalog[type] !== undefined || state.catalogLoading[type])) return;
    state.catalogLoading[type] = true;
    render();
    fetchCatalog(type, Q.q || '');
  }

  function bindCatalog() {
    const search = document.querySelector('#catalog-query');
    const runSearch = () => {
      const type =
        document.querySelector('[data-content-search]')?.dataset.contentSearch || Q.type || 'mod';
      state.catalogFilters ||= {};
      state.catalogFilters[type] = {
        game: document.querySelector('[data-catalog-game]')?.value.trim() || '',
        loader: document.querySelector('[data-catalog-loader]')?.value || '',
      };
      state.catalog[type] = undefined;
      state.catalogLoading[type] = true;
      render();
      fetchCatalog(type, search?.value || '');
    };
    document.querySelector('[data-content-search]')?.addEventListener('click', runSearch);
    search?.addEventListener('keydown', (event) => {
      if (event.key === 'Enter') runSearch();
    });
    document.querySelector('[data-reload-vanilla]')?.addEventListener('click', () => {
      state.vanilla = [];
      state.catalogLoading.vanilla = true;
      render();
      fetchCatalog('vanilla', '');
    });
    document.querySelectorAll('[data-install-vanilla]').forEach((button) => {
      button.addEventListener('click', () =>
        post({ type: 'installVanilla', version: button.dataset.installVanilla })
      );
    });
    document.querySelectorAll('[data-content-install]').forEach((button) => {
      button.addEventListener('click', () => {
        const card = button.closest('.content-card');
        window.McugResourcePicker?.open(
          button.dataset.contentInstall,
          button.dataset.contentCategory,
          card?.querySelector('.content-id b')?.textContent || '资源'
        );
      });
    });
  }

  function decorateAddServer() {
    const title = document.querySelector('.modal-title')?.textContent || '';
    if (!title.includes('添加服务器') && !title.includes('编辑服务器')) return;
    const inputs = document.querySelectorAll('.modal-body input');
    const primary = document.querySelector('.modal-foot .btn-primary');
    if (!inputs.length || !primary) return;
    const address = new URLSearchParams(location.search).get('address');
    if (address && inputs[1]) inputs[1].value = address;
    primary.addEventListener('click', (event) => {
      event.preventDefault();
      const address = inputs[1]?.value?.trim() || '';
      if (!address) {
        toast('请填写服务器地址。', true);
        return;
      }
      post({
        type: 'saveServer',
        name: inputs[0]?.value?.trim() || '',
        address,
        oldAddress: document.querySelector('[data-server-old]')?.value || '',
      });
    });
  }

  function decorateDataSources() {
    const rows = document.querySelectorAll('.ds-rows .ds-row');
    if (!rows.length) return;
    document.querySelectorAll('.ds-test').forEach((button, index) => {
      button.addEventListener('click', (event) => {
        event.preventDefault();
        const input = rows[index]?.querySelector('input');
        const result = rows[index]?.querySelector('.ds-result');
        if (result) {
          result.className = 'ds-result';
          result.textContent = '测试中…';
        }
        post({ type: 'testSource', name: DS[index]?.key || '', url: input?.value || '' });
      });
    });
    document
      .querySelector('[data-reset-sources]')
      ?.addEventListener('click', () => post({ type: 'resetDataSources' }));
    document.querySelectorAll('.ds-rows .ds-row input').forEach((input) =>
      input.addEventListener('input', () => {
        const result = input.closest('.ds-row').querySelector('.ds-result');
        result.className = 'ds-result';
        result.textContent = '地址已修改，尚未测试';
      })
    );
    const save = [...document.querySelectorAll('.ds-footer .btn-primary')].find((button) =>
      button.textContent.includes('保存')
    );
    save?.addEventListener('click', (event) => {
      event.preventDefault();
      const inputs = [...document.querySelectorAll('.ds-rows .ds-row input')];
      post({
        type: 'saveSettings',
        dataSources: {
          announcements: inputs[0]?.value || '',
          servers: inputs[1]?.value || '',
          recommendations: inputs[2]?.value || '',
          quotes: inputs[3]?.value || '',
        },
        writeEndpoint: document.querySelector('.ds-write input')?.value || '',
      });
    });
  }

  function decorateKeys() {
    const keyInput = document.querySelector('.admin-auth-card input');
    if (keyInput) {
      document
        .querySelector('.admin-auth-card .btn-primary')
        ?.addEventListener('click', (event) => {
          event.preventDefault();
          post({ type: 'verifyKey', role: 'admin', key: keyInput.value });
        });
    }
    const communityInput = document.querySelector('.settings-card input[placeholder*="密钥"]');
    if (communityInput && Q.screen === 'options') {
      const wrapper = communityInput.closest('.settings-card');
      wrapper?.querySelector('.btn-primary')?.addEventListener('click', (event) => {
        event.preventDefault();
        post({ type: 'verifyKey', role: 'community', key: communityInput.value });
      });
    }
  }

  let publishImageData = '';

  function decoratePublish() {
    if (!document.querySelector('.image-upload')) return;
    const fileInput = document.querySelector('#publish-image-input');
    document.querySelector('.pick-publish-image')?.addEventListener('click', (event) => {
      event.preventDefault();
      fileInput?.click();
    });
    fileInput?.addEventListener('change', () => {
      const file = fileInput.files?.[0];
      if (!file) return;
      if (!/^image\//.test(file.type)) {
        toast('请选择图片文件。', true);
        return;
      }
      if (file.size > 4 * 1024 * 1024) {
        toast('图片不能超过 4 MB。', true);
        return;
      }
      const reader = new FileReader();
      reader.onload = () => {
        publishImageData = String(reader.result || '');
        const preview = document.querySelector('.image-upload-preview');
        if (preview) preview.innerHTML = `<img src="${publishImageData}" alt="公告配图">`;
        const meta = document.querySelector('.image-upload-copy span');
        if (meta) meta.textContent = `${file.name} · ${Math.round(file.size / 1024)} KB`;
      };
      reader.readAsDataURL(file);
    });
    document.querySelector('.remove-publish-image')?.addEventListener('click', (event) => {
      event.preventDefault();
      publishImageData = '';
      const preview = document.querySelector('.image-upload-preview');
      if (preview) preview.innerHTML = '';
    });
    const primary = document.querySelector('.modal-foot .btn-primary');
    if (!primary) return;
    const title = document.querySelector('.modal-body .input');
    const body = document.querySelector('.modal-body textarea');
    const expiry = document.querySelector('.modal-body input[type="datetime-local"]');
    primary.addEventListener('click', (event) => {
      event.preventDefault();
      post({
        type: 'publish',
        title: title?.value || '',
        body: body?.value || '',
        expiresAt: expiry?.value || '',
        imageData: publishImageData,
      });
    });
  }

  if (bridge) {
    bridge.addEventListener('message', (event) => {
      const message = event.data;
      if (!message) return;
      switch (message.type) {
        case 'accountState':
          state = Object.assign({}, state, message);
          Object.assign(window.state, message);
          applyAccount();
          if (Q.screen === 'options') render();
          break;
        case 'state':
          applyState(message);
          break;
        case 'contentList':
          if (message.requestId && message.requestId !== state?.catalogRequests?.[message.category])
            break;
          state.catalog ||= {};
          state.catalogLoading ||= {};
          state.catalog[message.category] = message.items || [];
          state.catalogLoading[message.category] = false;
          if (Q.screen === 'catalog') render();
          break;
        case 'vanillaList':
          state.catalog ||= {};
          state.catalogLoading ||= {};
          state.vanilla = message.items || [];
          state.catalog.vanilla = state.vanilla;
          state.catalogLoading.vanilla = false;
          if (Q.screen === 'catalog') render();
          break;
        case 'contentError':
          if (message.requestId && message.requestId !== state?.catalogRequests?.[message.category])
            break;
          if (message.category) {
            state.catalog[message.category] = [];
            state.catalogLoading[message.category] = false;
          }
          toast(message.message || '读取失败', true);
          if (Q.screen === 'catalog') render();
          break;
        case 'communityRefreshDone':
          toast(
            message.ok ? '社区数据已刷新。' : '部分社区数据读取失败，请检查数据源。',
            !message.ok
          );
          render();
          break;
        case 'community':
          applyCommunity(message.payload);
          break;
        case 'serverPing': {
          const server = servers.find(
            (item) => item.address === message.id || item.id === message.id
          );
          if (server) {
            server.online = !!message.online;
            server.players = message.players ?? 0;
            server.max = message.max ?? 0;
            server.ping = message.ping ?? 0;
            server.motd = message.motd || (message.online ? '在线' : '离线');
            server.version = message.version || '—';
            document.querySelectorAll('[data-server-id]').forEach((node) => {
              if (node.dataset.serverId !== String(server.id)) return;
              node.classList.toggle('offline', !server.online);
              const sub = node.querySelector('.server-sub');
              if (sub) {
                const dot = document.createElement('span');
                dot.className = 'dot ' + (server.online ? 'online' : 'offline');
                sub.replaceChildren(
                  dot,
                  document.createTextNode(`${server.online ? '在线' : '离线'} · ${server.version}`)
                );
              }
              const motd = node.querySelector('.server-motd');
              if (motd) motd.textContent = server.motd;
              const stat = node.querySelector('.server-stat');
              if (stat)
                stat.textContent = server.online
                  ? `${server.players} / ${server.max}`
                  : '上次在线人数未知';
              const ping = node.querySelector('.server-ping');
              if (ping) {
                ping.textContent = server.online ? `${server.ping} ms` : '离线';
                ping.className =
                  'server-ping ' + (!server.online ? 'bad' : server.ping < 45 ? 'good' : 'mid');
              }
              const status = document.querySelector('.server-status');
              if (status) {
                const online = servers.filter((s) => s.online).length;
                status.textContent = `${online} 在线 · ${servers.length - online} 离线`;
              }
            });
          }
          break;
        }
        case 'launchResult':
          toast(message.message || (message.ok ? '已启动' : '启动失败'), !message.ok);
          break;
        case 'authResult':
          toast(message.message || '', message.ok === false);
          document.querySelectorAll('[data-mua-submit]').forEach((button) => {
            button.disabled = false;
          });
          document.querySelectorAll('[data-ms-login]').forEach((button) => {
            button.disabled = false;
          });
          if (message.ok && Q.modal === 'mua') window.mcugGo?.('?screen=home');
          if (message.ok && Q.modal === 'ms') window.mcugGo?.('?screen=home');
          break;
        case 'serverResult':
          toast(message.message || '', !message.ok);
          if (message.ok) window.mcugGo?.('?screen=home');
          break;
        case 'modResult':
          toast(message.message || '', !message.ok);
          document.querySelectorAll('.mod-install-btn').forEach((button) => {
            button.disabled = false;
          });
          if (message.ok) post({ type: 'requestState' });
          break;
        case 'testResult': {
          state.sourceTests ||= {};
          state.sourceTests[message.name] = message;
          window.state.sourceTests = state.sourceTests;
          const index = DS.findIndex((item) => item.key === message.name);
          const result = document
            .querySelectorAll('.ds-rows .ds-row')
            [index]?.querySelector('.ds-result');
          const input = document
            .querySelectorAll('.ds-rows .ds-row')
            [index]?.querySelector('input');
          if (message.url && input?.value !== message.url) break;
          if (result) {
            result.className = `ds-result ${message.ok ? 'ok' : 'bad'}`;
            result.textContent = message.ok
              ? `连接正常 · ${message.ms} ms`
              : message.message || '连接失败';
          }
          break;
        }
        case 'settingsSaved':
          toast('设置已保存。');
          break;
        case 'pickedPath': {
          if (
            [
              'pack',
              'exportInstance',
              'gameRoot',
              'packDirectory',
              'resourceDestination',
              'offlineSkin',
            ].includes(message.target)
          )
            break;
          const selector =
            message.target === 'java' ? '[data-setting-java]' : '[data-setting-root]';
          const input = document.querySelector(selector);
          if (input && message.path) input.value = message.path;
          toast('已选择路径，保存后生效。');
          break;
        }
        case 'javaList':
          state.javaList = message.items || [];
          if (Q.screen === 'settings' && Q.panel === 'java') render();
          toast(`扫描完成，共 ${state.javaList.length} 个 Java。`);
          break;
        case 'backupDone':
          toast(`存档已备份：${message.path}`);
          break;
        case 'logText': {
          state.logText = message.text || '';
          const box = document.querySelector('#log-box');
          if (box) box.textContent = state.logText;
          break;
        }
        case 'systemInfo': {
          state.diagText = [
            `启动器：${message.app}`,
            `.NET：${message.dotnet}`,
            `WebView2：${message.webview}`,
            `系统：${message.os}`,
            `游戏目录：${message.root}`,
            `数据目录：${message.data}`,
          ].join('\n');
          const box = document.querySelector('#diag-box');
          if (box) box.textContent = state.diagText;
          break;
        }
        case 'keyResult':
          toast(message.message || (message.ok ? '验证成功' : '验证失败'), !message.ok);
          break;
        case 'publishResult':
          toast(message.message || (message.ok ? '已保存' : '发布失败'), !message.ok);
          break;
        case 'error':
          toast(message.message || '操作失败', true);
          break;
      }
    });
  }

  const baseRender = render;
  render = function () {
    baseRender();
    decorate();
  };

  document.addEventListener('keydown', (event) => {
    if (event.key === 'Escape' && location.search.includes('modal='))
      window.mcugGo?.('?screen=' + (Q.screen || 'home'));
  });
  window.addEventListener('hashchange', () => {
    setTimeout(() => post({ type: 'requestState' }), 40);
  });

  const style = document.createElement('style');
  style.textContent = `.host-toast{position:fixed;left:50%;bottom:24px;transform:translate(-50%,12px);background:#1d2b23;color:#fff;padding:9px 16px;border-radius:8px;font:12px/1.4 'Microsoft YaHei UI',sans-serif;opacity:0;pointer-events:none;transition:.18s;z-index:9999;box-shadow:0 8px 24px rgba(0,0,0,.18)}.host-toast.show{opacity:1;transform:translate(-50%,0)}.host-toast.bad{background:#8a3b35}`;
  document.head.appendChild(style);

  decorate();
  post({ type: 'ready' });
})();
