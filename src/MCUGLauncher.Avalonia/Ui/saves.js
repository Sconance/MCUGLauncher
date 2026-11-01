(() => {
  const bridge = window.chrome?.webview;
  function mount() {
    const panel = document.querySelector('[data-save-backups]');
    if (!panel || panel.dataset.bound) return;
    panel.dataset.bound = '1';
    panel.textContent = '正在读取备份…';
    bridge?.postMessage({ type: 'listSaveBackups' });
  }
  bridge?.addEventListener('message', (event) => {
    if (event.data?.type !== 'saveBackups') return;
    const panel = document.querySelector('[data-save-backups]');
    if (!panel) return;
    panel.replaceChildren();
    if (!event.data.items.length) panel.textContent = '当前实例没有备份。';
    for (const backup of event.data.items)
      for (const world of backup.worlds) {
        const row = document.createElement('div');
        row.className = 'task-row';
        const label = document.createElement('p');
        label.textContent = `${world} · ${backup.created}`;
        const button = document.createElement('button');
        button.className = 'btn btn-ghost';
        button.textContent = '恢复为新存档';
        button.onclick = () => bridge?.postMessage({ type: 'restoreSave', id: backup.id, world });
        row.append(label, button);
        panel.append(row);
      }
  });
  window.McugSaves = { mount };
  mount();
})();
