// Small native-host dialogs share cancellation behavior; domain choices stay in feature modules.
(() => {
  function open(title, body, onCancel = () => {}) {
    const root = document.createElement('div');
    root.className = 'overlay custom-overlay';
    root.innerHTML = `<section class="modal wide" role="dialog" aria-modal="true"><div class="modal-head"><h2 class="modal-title"></h2><button class="modal-close" aria-label="关闭">×</button></div><div class="modal-body">${body}</div></section>`;
    root.querySelector('h2').textContent = title;
    const previous = document.activeElement;
    const onKey = (event) => {
      if (event.key !== 'Escape') return;
      event.preventDefault();
      event.stopImmediatePropagation();
      onCancel();
      close();
    };
    const close = () => {
      root.remove();
      document.removeEventListener('keydown', onKey, true);
      if (previous?.isConnected && typeof previous.focus === 'function') previous.focus();
    };
    root.querySelector('.modal-close').onclick = () => {
      onCancel();
      close();
    };
    root.querySelector('[role="dialog"]').setAttribute('aria-label', title);
    document.body.append(root);
    document.addEventListener('keydown', onKey, true);
    root.querySelector('input:not(:disabled),select:not(:disabled),button')?.focus();
    return { root, close };
  }
  window.McugDialog = { open };
})();
