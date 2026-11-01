// Resource version and target are explicit. The host token freezes the chosen compatibility filters.
(() => {
  const bridge = window.chrome?.webview;
  const post = (message) => bridge?.postMessage(message);
  let view = null;
  let sequence = 0;
  function open(id, category, name) {
    view?.close();
    const dialog = window.McugDialog.open(
      '下载选项 · ' + name,
      `<label>筛选依据实例<select class="select" data-resource-instance></select></label>
      <div class="install-fields"><label>游戏版本（自定义筛选，可留空）<input class="input" data-resource-game></label><label>加载器<select class="select" data-resource-loader><option value="">不限</option>${['vanilla', 'fabric', 'forge', 'neoforge', 'quilt'].map((x) => `<option>${x}</option>`).join('')}</select></label></div>
      <label>资源版本<select class="select" data-resource-version disabled></select></label>
      <label>操作<select class="select" data-resource-mode><option value="instance">安装到所选实例</option><option value="local">下载到自定义目录</option></select></label>
      <div class="path-row"><input class="input" data-resource-path placeholder="自定义下载目录"><button class="btn btn-ghost" data-resource-folder>选择目录</button></div>
      <p role="status" data-resource-status></p><button class="btn btn-primary" data-resource-confirm disabled>确认下载</button>`
    );
    view = { ...dialog, id, category, requestId: '', token: '' };
    const root = dialog.root;
    const instances = root.querySelector('[data-resource-instance]');
    instances.replaceChildren(
      new Option('不使用实例，手动筛选 / 仅下载', ''),
      ...(window.state?.instances || []).map((i) => new Option(i.displayName || i.name, i.name))
    );
    instances.value = window.state?.selectedInstance || '';
    const mode = root.querySelector('[data-resource-mode]');
    if (['modpack', 'datapack'].includes(category)) {
      mode.value = 'local';
      mode.querySelector('[value=instance]').disabled = true;
    }
    const update = () => {
      root.querySelector('[data-resource-game]').disabled = !!instances.value;
      root.querySelector('[data-resource-loader]').disabled = !!instances.value;
      root.querySelector('[data-resource-path]').disabled = mode.value !== 'local';
      root.querySelector('[data-resource-folder]').disabled = mode.value !== 'local';
    };
    const read = () => {
      if (!view || view.root !== root) return;
      update();
      view.token = '';
      view.requestId = String(++sequence);
      root.querySelector('[data-resource-confirm]').disabled = true;
      root.querySelector('[data-resource-status]').textContent = '正在读取兼容版本…';
      post({
        type: 'listContentVersions',
        id,
        category,
        instance: instances.value,
        game: root.querySelector('[data-resource-game]').value.trim(),
        loader: root.querySelector('[data-resource-loader]').value,
        requestId: view.requestId,
      });
    };
    instances.onchange = read;
    root.querySelector('[data-resource-game]').onchange = read;
    root.querySelector('[data-resource-loader]').onchange = read;
    mode.onchange = update;
    root.querySelector('[data-resource-folder]').onclick = () =>
      post({ type: 'pickFolder', target: 'resourceDestination' });
    root.querySelector('[data-resource-confirm]').onclick = () => {
      if (mode.value === 'instance' && !instances.value) {
        root.querySelector('[data-resource-status]').textContent =
          '请选择安装实例，或改用自定义下载。';
        return;
      }
      post({
        type: 'queueContentVersion',
        token: view.token,
        versionId: root.querySelector('[data-resource-version]').value,
        mode: mode.value,
        path: root.querySelector('[data-resource-path]').value.trim(),
      });
    };
    read();
  }
  bridge?.addEventListener('message', ({ data }) => {
    if (!view || !view.root.isConnected) return;
    const root = view.root;
    if (data?.type === 'resourceVersions' && data.requestId === view.requestId) {
      view.token = data.token;
      if (root.querySelector('[data-resource-instance]').value) {
        root.querySelector('[data-resource-game]').value = data.game;
        root.querySelector('[data-resource-loader]').value = data.loader;
      }
      const select = root.querySelector('[data-resource-version]');
      select.replaceChildren(
        ...data.items.map((v) => new Option(v.name + ' · ' + v.files.join(', '), v.id))
      );
      select.disabled = !data.items.length;
      root.querySelector('[data-resource-confirm]').disabled = !data.items.length;
      root.querySelector('[data-resource-status]').textContent = data.items.length
        ? '请选择版本和目标后确认。筛选：' + (data.game || '不限') + ' / ' + (data.loader || '不限')
        : '没有匹配的资源版本，请调整条件。';
    } else if (data?.type === 'pickedPath' && data.target === 'resourceDestination')
      root.querySelector('[data-resource-path]').value = data.path;
    else if (data?.type === 'resourceQueued' && data.token === view.token) {
      view.close();
      view = null;
    } else if (data?.type === 'error')
      root.querySelector('[data-resource-status]').textContent = data.message;
  });
  window.McugResourcePicker = { open };
})();
