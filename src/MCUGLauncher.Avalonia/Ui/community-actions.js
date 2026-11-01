// Refresh and recommendation destinations use explicit actions and validated links.
(() => {
  const bridge = window.chrome?.webview;
  function mount() {
    document.querySelectorAll('[data-community-refresh]').forEach((button) => {
      button.onclick = () => {
        button.disabled = true;
        button.textContent = '正在刷新…';
        bridge?.postMessage({ type: 'refreshCommunity' });
      };
    });
    document.querySelectorAll('[data-rec-server]').forEach((button) => {
      button.onclick = () =>
        window.mcugGo?.(
          '?screen=home&modal=add-server&address=' + encodeURIComponent(button.dataset.recServer)
        );
    });
  }
  bridge?.addEventListener('message', ({ data }) => {
    if (data?.type === 'error')
      document.querySelectorAll('[data-community-refresh]').forEach((button) => {
        button.disabled = false;
        button.textContent = '重试刷新';
      });
  });
  window.McugCommunityActions = { mount };
})();
