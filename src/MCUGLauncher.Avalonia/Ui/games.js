(() => {
  const bridge = window.chrome?.webview;
  let games = [];
  function mount() {
    const launch = document.querySelector('.launch-btn');
    if (launch && !document.querySelector('[data-running-games]')) {
      const button = document.createElement('button');
      button.dataset.runningGames = '';
      button.className = 'btn btn-ghost';
      button.onclick = () => window.mcugGo?.('?screen=settings&panel=games');
      launch.closest('.launch-row')?.after(button);
      if (!button.isConnected) launch.parentElement.after(button);
    }
    const button = document.querySelector('[data-running-games]');
    if (button) button.textContent = `运行中的游戏（${games.length}）`;
    const panel = document.querySelector('[data-games-list]');
    if (!panel) return;
    panel.replaceChildren();
    if (!games.length) panel.textContent = '没有由本次启动器启动的游戏。';
    for (const game of games) {
      const row = document.createElement('div');
      row.className = 'task-row';
      const label = document.createElement('p');
      label.textContent = `${game.version} · ${game.player} · PID ${game.pid}`;
      const close = document.createElement('button');
      close.className = 'btn btn-ghost';
      close.textContent = '关闭游戏';
      close.onclick = () => bridge?.postMessage({ type: 'stopGame', id: game.id });
      row.append(label, close);
      panel.append(row);
    }
  }
  bridge?.addEventListener('message', (event) => {
    if (event.data?.type === 'games') {
      games = event.data.items || [];
      mount();
    }
  });
  window.McugGames = { mount };
  mount();
  bridge?.postMessage({ type: 'requestGames' });
})();
