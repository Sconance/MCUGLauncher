// MUA profile selection is separate from credentials and starts only when the server returns choices.
(() => {
  const bridge = window.chrome?.webview;
  const post = (message) => bridge?.postMessage(message);
  let view = null;
  bridge?.addEventListener('message', ({ data }) => {
    if (data?.type !== 'muaProfiles') return;
    view?.close();
    const cancel = () => post({ type: 'selectMuaProfile', token: data.token, profileId: '' });
    view = window.McugDialog.open(
      '选择 MUA 角色',
      `<label>游戏角色<select class="select" data-mua-profile></select></label><div class="settings-actions"><button class="btn btn-primary" data-mua-profile-confirm>使用所选角色</button><button class="btn btn-ghost" data-mua-profile-cancel>取消</button></div>`,
      cancel
    );
    view.root
      .querySelector('select')
      .replaceChildren(...data.items.map((p) => new Option(p.name, p.id)));
    view.root.querySelector('[data-mua-profile-confirm]').onclick = () => {
      post({
        type: 'selectMuaProfile',
        token: data.token,
        profileId: view.root.querySelector('select').value,
      });
      view.close();
    };
    view.root.querySelector('[data-mua-profile-cancel]').onclick = () => {
      cancel();
      view.close();
    };
  });
})();
