// Settings panels send only controls present on the page; absent fields are never defaults.
(() => {
  const post = (message) => window.chrome?.webview?.postMessage(message);
  const toast = (message, bad) => window.mcugToast?.(message, bad);
  function mount() {
    const scope = document.querySelector('.settings-content');
    if (!scope || scope.dataset.settingsBound) return;
    scope.dataset.settingsBound = '1';
    scope.querySelector('[data-save-launch]')?.addEventListener('click', () => {
      const message = { type: 'saveSettings' };
      for (const key of ['root', 'java', 'ram', 'width', 'height']) {
        const input = scope.querySelector(`[data-setting-${key}]`);
        if (!input) continue;
        if (['ram', 'width', 'height'].includes(key)) {
          if (!input.value.trim() || !Number.isFinite(Number(input.value))) {
            toast('请填写有效的数值。', true);
            return;
          }
          message[key] = Number(input.value);
        } else message[key] = input.value.trim();
      }
      const auto = scope.querySelector('[data-setting-auto]');
      if (auto) message.autoMemory = auto.checked;
      post(message);
    });
    scope.querySelectorAll('[data-pick]').forEach((button) => {
      button.addEventListener('click', () => {
        const target = button.dataset.pick;
        post({ type: target === 'java' ? 'pickFile' : 'pickFolder', target });
      });
    });
    scope.querySelector('[data-scan-java]')?.addEventListener('click', (event) => {
      event.preventDefault();
      toast('正在扫描本机 Java…');
      post({ type: 'scanJava' });
    });
    scope.querySelectorAll('[data-use-java]').forEach((button) => {
      button.addEventListener('click', () => {
        const input = scope.querySelector('[data-setting-java]');
        if (input) input.value = button.dataset.useJava || '';
        toast('已填入 Java 路径，保存后生效。');
      });
    });
    scope
      .querySelector('[data-backup-saves]')
      ?.addEventListener('click', () => post({ type: 'backupSaves' }));
    scope
      .querySelector('[data-read-log]')
      ?.addEventListener('click', () => post({ type: 'readLog' }));
    scope
      .querySelector('[data-system-info]')
      ?.addEventListener('click', () => post({ type: 'systemInfo' }));
    scope.querySelector('[data-copy-diag]')?.addEventListener('click', async () => {
      try {
        await navigator.clipboard.writeText(
          window.state?.diagText || document.querySelector('#diag-box')?.textContent || ''
        );
        toast('诊断信息已复制。');
      } catch {
        toast('复制失败，请手动选择文本。', true);
      }
    });
    scope
      .querySelector('[data-reset-window]')
      ?.addEventListener('click', () => post({ type: 'resetWindow' }));
    scope.querySelector('[data-reset-ui]')?.addEventListener('click', () => location.reload());
    scope.querySelectorAll('[data-open-folder]').forEach((button) => {
      button.addEventListener('click', () =>
        post({ type: 'openFolder', kind: button.dataset.openFolder })
      );
    });
    scope.querySelectorAll('[data-ping-server]').forEach((button) => {
      button.addEventListener('click', () =>
        post({ type: 'pingServer', id: button.dataset.pingServer })
      );
    });
    scope.querySelector('[data-test-all]')?.addEventListener('click', () => {
      window.mcugGo?.('?screen=datasource');
      toast('在数据源页逐项测试，结果会显示在地址下方。');
    });
  }

  window.McugSettings = { mount };
})();
