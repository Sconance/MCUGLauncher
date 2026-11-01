// Global and per-instance launch editing share the same validated form and opaque target token.
(() => {
  const bridge = window.chrome?.webview;
  const post = (message) => bridge?.postMessage(message);
  let view = null;
  let sequence = 0;
  function open(version = '') {
    view?.close();
    const dialog = window.McugDialog.open(
      version ? '单实例启动设置' : '全局高级启动设置',
      `<div data-launch-form>正在读取…</div>`
    );
    view = { ...dialog, version, requestId: String(++sequence), token: '' };
    post({ type: 'readLaunchOptions', version, requestId: view.requestId });
  }
  function fill(data) {
    const panel = view.root.querySelector('[data-launch-form]');
    const fields = [
      ['java', 'Java 路径（留空自动）', 'text'],
      ['ramMb', '手动内存 / MB', 'number'],
      ['width', '窗口宽', 'number'],
      ['height', '窗口高', 'number'],
      ['jvmArguments', '附加 JVM 参数', 'text'],
      ['windowTitle', '游戏窗口标题', 'text'],
      ['server', '启动后连接服务器（可留空）', 'text'],
    ];
    panel.innerHTML = `${view.version ? '<label>实例显示名称<input class="input" data-launch-name></label><label><input type="checkbox" data-launch-inherit>继承全局启动设置</label>' : ''}<label><input type="checkbox" data-launch-auto>自动分配内存</label><label><input type="checkbox" data-launch-fullscreen>全屏</label><div class="install-fields">${fields.map(([key, label, type]) => `<label>${label}<input class="input" type="${type}" data-launch-field="${key}"></label>`).join('')}</div><p role="status" data-launch-status></p><button class="btn btn-primary" data-launch-save>保存启动设置</button>`;
    view.token = data.token;
    if (view.version) {
      panel.querySelector('[data-launch-name]').value = data.name;
      panel.querySelector('[data-launch-inherit]').checked = data.inherited;
    }
    const auto = panel.querySelector('[data-launch-auto]');
    const full = panel.querySelector('[data-launch-fullscreen]');
    auto.checked = data.settings.autoMemory;
    full.checked = data.settings.fullScreen;
    for (const [key] of fields)
      panel.querySelector(`[data-launch-field=${key}]`).value = data.settings[key] ?? '';
    const update = () => {
      const inherited = panel.querySelector('[data-launch-inherit]')?.checked;
      auto.disabled = !!inherited;
      full.disabled = !!inherited;
      panel
        .querySelectorAll('[data-launch-field]')
        .forEach(
          (input) =>
            (input.disabled =
              !!inherited || (input.dataset.launchField === 'ramMb' && auto.checked))
        );
    };
    auto.onchange = update;
    if (view.version) panel.querySelector('[data-launch-inherit]').onchange = update;
    update();
    panel.querySelector('[data-launch-save]').onclick = () => {
      const settings = { autoMemory: auto.checked, fullScreen: full.checked };
      for (const [key, , type] of fields)
        settings[key] =
          type === 'number'
            ? Number(panel.querySelector(`[data-launch-field=${key}]`).value)
            : panel.querySelector(`[data-launch-field=${key}]`).value;
      post({
        type: 'saveLaunchOptions',
        token: view.token,
        name: panel.querySelector('[data-launch-name]')?.value || '',
        inherit: !!panel.querySelector('[data-launch-inherit]')?.checked,
        settings,
      });
    };
  }
  function mount() {
    document
      .querySelectorAll('[data-open-launch-options]')
      .forEach((button) => (button.onclick = () => open(button.dataset.version || '')));
    const auto = document.querySelector('[data-setting-auto]');
    const ram = document.querySelector('[data-setting-ram]');
    if (auto && ram) {
      auto.onchange = () => (ram.disabled = auto.checked);
      auto.onchange();
    }
  }
  bridge?.addEventListener('message', ({ data }) => {
    if (!view?.root.isConnected) return;
    if (data?.type === 'launchOptions' && data.requestId === view.requestId) fill(data);
    else if (data?.type === 'launchOptionsSaved') {
      view.close();
      view = null;
    } else if (data?.type === 'error') {
      const status =
        view.root.querySelector('[data-launch-status]') ||
        view.root.querySelector('[data-launch-form]');
      status.textContent = data.message;
    }
  });
  window.McugLaunchOptions = { mount, open };
})();
