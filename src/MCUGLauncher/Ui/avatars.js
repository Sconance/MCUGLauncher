// Avatar updates do not rebuild the account card; stale host responses carry account identity.
(() => {
  const bridge = window.chrome?.webview;
  const post = (message) => bridge?.postMessage(message);
  let key = '';
  function draw() {
    document
      .querySelectorAll('.acc-avatar,[data-avatar-preview]')
      .forEach((node) => (node.innerHTML = Avatar()));
    document
      .querySelectorAll('[data-avatar-label]')
      .forEach((node) => (node.textContent = window.state?.avatarLabel || '默认头像'));
  }
  function mount() {
    const state = window.state || {};
    const current = (state.accountType || '') + ':' + (state.player || '');
    if (bridge && state.player && key !== current) {
      key = current;
      delete state.avatar;
      post({ type: 'readAvatar' });
    }
    draw();
    document
      .querySelector('[data-pick-offline-skin]')
      ?.addEventListener('click', () => post({ type: 'pickFile', target: 'offlineSkin' }), {
        once: true,
      });
    document
      .querySelector('[data-default-skin]')
      ?.addEventListener('click', () => post({ type: 'setOfflineSkin', path: '' }), { once: true });
    document
      .querySelector('[data-refresh-avatar]')
      ?.addEventListener('click', () => post({ type: 'readAvatar' }), { once: true });
  }
  bridge?.addEventListener('message', ({ data }) => {
    if (data?.type === 'state' || data?.type === 'accountState') queueMicrotask(mount);
    else if (
      data?.type === 'avatar' &&
      data.account === window.state?.accountType &&
      data.player === window.state?.player &&
      /^data:image\/png;base64,/.test(data.image)
    ) {
      window.state.avatar = data.image;
      window.state.avatarLabel = data.label;
      draw();
    } else if (data?.type === 'pickedPath' && data.target === 'offlineSkin')
      post({ type: 'setOfflineSkin', path: data.path });
  });
  window.McugAvatars = { mount };
})();
