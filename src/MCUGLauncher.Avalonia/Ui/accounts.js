// Mode switches restore credentials; changing or removing an account is explicit.
(() => {
  const bridge = window.chrome?.webview;
  const post = (message) => bridge?.postMessage(message);
  const login = (kind) => window.mcugGo?.('?screen=home&modal=' + (kind === 'mua' ? 'mua' : 'ms'));
  function mount() {
    document.querySelectorAll('.acc-tab[data-account]').forEach((tab) => {
      tab.onclick = () => post({ type: 'switchAccountMode', account: tab.dataset.account });
    });
    const button = document.querySelector('.acc-btn');
    if (button)
      button.onclick = () => {
        const state = window.state || {};
        if (state.accountType !== 'offline') {
          login(state.accountType);
          return;
        }
        const name = prompt('离线玩家名（3–16 位字母、数字或下划线）', state.player || '');
        if (name == null) return;
        if (!/^[A-Za-z0-9_]{3,16}$/.test(name.trim())) {
          window.mcugToast?.('玩家名格式不正确。', true);
          return;
        }
        post({ type: 'saveSettings', player: name.trim() });
      };
    document.querySelectorAll('[data-forget-account]').forEach((button) => {
      button.onclick = () => {
        const account = button.dataset.forgetAccount;
        if (
          confirm(
            '移除本机保存的' +
              (account === 'mua' ? ' MUA ' : '正版') +
              '账号？再次使用时需要重新登录，已运行游戏不受影响。'
          )
        )
          post({ type: 'forgetAccount', account });
      };
    });
  }
  bridge?.addEventListener('message', ({ data }) => {
    if (data?.type !== 'accountSwitchResult') return;
    window.mcugToast?.(data.message, !data.needsLogin);
    if (data.needsLogin) login(data.account);
  });
  window.McugAccounts = { mount };
})();
