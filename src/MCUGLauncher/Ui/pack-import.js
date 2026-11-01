// Import never starts on file selection; name and game directory are reviewed first.
(() => {
  const bridge = window.chrome?.webview;
  const post = (message) => bridge?.postMessage(message);
  let view = null;
  bridge?.addEventListener('message', ({ data }) => {
    if (data?.type === 'packPreview') {
      view?.close();
      const dialog = window.McugDialog.open(
        '确认导入整合包',
        `<p data-pack-summary></p><label>实例名称<input class="input" data-pack-name></label><label>游戏目录<div class="path-row"><input class="input" data-pack-directory><button class="btn btn-ghost" data-pack-directory-pick>选择</button></div></label><p role="status" data-pack-status></p><button class="btn btn-primary" data-pack-confirm>确认导入</button>`
      );
      view = { ...dialog, token: data.token };
      dialog.root.querySelector('[data-pack-summary]').textContent =
        data.game + ' / ' + data.loader;
      dialog.root.querySelector('[data-pack-name]').value = data.name;
      dialog.root.querySelector('[data-pack-directory]').value = data.root;
      dialog.root.querySelector('[data-pack-directory-pick]').onclick = () =>
        post({ type: 'pickFolder', target: 'packDirectory' });
      dialog.root.querySelector('[data-pack-confirm]').onclick = () =>
        post({
          type: 'confirmPack',
          token: view.token,
          name: dialog.root.querySelector('[data-pack-name]').value,
          directory: dialog.root.querySelector('[data-pack-directory]').value,
        });
    } else if (
      view?.root.isConnected &&
      data?.type === 'pickedPath' &&
      data.target === 'packDirectory'
    )
      view.root.querySelector('[data-pack-directory]').value = data.path;
    else if (view && data?.type === 'packQueued' && data.token === view.token) {
      view.close();
      view = null;
    } else if (view?.root.isConnected && data?.type === 'error')
      view.root.querySelector('[data-pack-status]').textContent = data.message;
  });
})();
