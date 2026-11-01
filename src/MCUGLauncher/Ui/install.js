// Installation controls are separate from catalog rendering and transfer state.
(() => {
  const bridge = window.chrome?.webview;
  const post = (message) => bridge?.postMessage(message);
  function mount() {
    const panel = document.querySelector('[data-install-tools]');
    if (!panel || panel.dataset.bound) return;
    panel.dataset.bound = '1';
    panel.innerHTML = `<div class="install-fields"><label>游戏版本<input class="input" data-install-game placeholder="例如 1.21.1"></label><label>加载器<select class="select" data-install-loader><option value="fabric">Fabric</option><option value="quilt">Quilt</option><option value="forge">Forge</option><option value="neoforge">NeoForge</option></select></label><label>加载器版本<select class="select" data-install-release disabled><option>请先读取版本</option></select></label></div><div class="settings-actions"><button class="btn btn-ghost" data-loader-read>读取加载器版本</button><button class="btn btn-primary" data-loader-install disabled>加入安装队列</button></div><p data-install-status role="status"></p><hr><h3>本地整合包</h3><p>选择 mrpack 或受支持的整合包 ZIP，导入为独立实例。</p><div class="settings-actions"><button class="btn btn-primary" data-pack-pick>选择整合包</button><a class="btn btn-ghost" href="?screen=settings&panel=tasks">查看任务</a></div>`;
    const game = panel.querySelector('[data-install-game]');
    const loader = panel.querySelector('[data-install-loader]');
    const release = panel.querySelector('[data-install-release]');
    const install = panel.querySelector('[data-loader-install]');
    const status = panel.querySelector('[data-install-status]');
    const reset = () => {
      install.disabled = true;
      release.disabled = true;
      release.replaceChildren();
      status.textContent = '条件已改变，请重新读取版本。';
    };
    game.oninput = reset;
    loader.onchange = reset;
    panel.querySelector('[data-loader-read]').onclick = () => {
      reset();
      status.textContent = '正在读取…';
      post({ type: 'listLoaders', version: game.value.trim(), loader: loader.value });
    };
    install.onclick = () => {
      post({
        type: 'installLoader',
        version: game.value.trim(),
        loader: loader.value,
        loaderVersion: release.value,
      });
      status.textContent = '已提交请求，请在右上角下载任务中确认状态。';
    };
    panel.querySelector('[data-pack-pick]').onclick = () =>
      post({ type: 'pickFile', target: 'pack' });
  }
  bridge?.addEventListener('message', (event) => {
    const m = event.data;
    if (m?.type === 'pickedPath' && m.target === 'pack') {
      post({ type: 'previewPack', path: m.path });
      return;
    }
    const panel = document.querySelector('[data-install-tools]');
    if (!panel) return;
    if (m?.type === 'error') {
      panel.querySelector('[data-install-status]').textContent = m.message;
      return;
    }
    if (m?.type !== 'loaderList') return;
    if (
      panel.querySelector('[data-install-game]').value.trim() !== m.version ||
      panel.querySelector('[data-install-loader]').value !== m.loader
    )
      return;
    const select = panel.querySelector('[data-install-release]');
    select.replaceChildren(...m.versions.map((v) => new Option(v, v)));
    select.disabled = !m.versions.length;
    panel.querySelector('[data-loader-install]').disabled = !m.versions.length;
    panel.querySelector('[data-install-status]').textContent = m.versions.length
      ? `找到 ${m.versions.length} 个版本。`
      : '没有适配版本。';
  });
  window.McugInstall = { mount };
  mount();
})();
