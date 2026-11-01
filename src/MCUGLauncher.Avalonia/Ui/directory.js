// Directory selection and instance-list state belong to this component, not the shell.
(() => {
  const bridge = window.chrome?.webview;
  const post = (message) => bridge?.postMessage(message);
  const collapsed = new Set();
  let query = '';
  let activeRoot = '';
  function mount() {
    const panel = document.querySelector('.version-panel');
    if (!panel || panel.dataset.bound) return;
    panel.dataset.bound = '1';
    if (activeRoot !== window.state?.root) {
      query = '';
      activeRoot = window.state?.root || '';
    }
    const select = panel.querySelector('[data-game-root]');
    select.onchange = () => post({ type: 'saveSettings', root: select.value });
    panel.querySelector('[data-add-root]').onclick = () =>
      post({ type: 'pickFolder', target: 'gameRoot' });
    panel.querySelector('[data-home-import]').onclick = () =>
      post({ type: 'pickFile', target: 'pack' });
    panel.querySelector('[data-refresh-instances]').onclick = () => post({ type: 'requestState' });
    const search = panel.querySelector('.version-search input');
    search.value = query;
    const filter = () => {
      query = search.value;
      panel.querySelectorAll('.version-group').forEach((group) => {
        const head = group.querySelector('.version-group-head');
        const key = activeRoot + ':' + head.querySelector('b').textContent;
        let count = 0;
        group.querySelectorAll('.version-row').forEach((row) => {
          row.hidden = !row.textContent.toLowerCase().includes(query.trim().toLowerCase());
          if (!row.hidden) count++;
        });
        group.hidden = count === 0 && !!query.trim();
        group.querySelector('.version-list').hidden = collapsed.has(key) && !query.trim();
        head.setAttribute('aria-expanded', String(!group.querySelector('.version-list').hidden));
      });
      let empty = panel.querySelector('[data-instance-empty]');
      if (!empty) {
        empty = document.createElement('p');
        empty.dataset.instanceEmpty = '';
        empty.className = 'empty-line';
        panel.querySelector('.version-groups').append(empty);
      }
      const rows = [...panel.querySelectorAll('.version-row')];
      empty.hidden = rows.some((row) => !row.hidden);
      empty.textContent = rows.length
        ? '没有匹配的实例，请调整搜索条件。'
        : '此目录下没有发现游戏实例，可切换目录或导入整合包。';
    };
    search.oninput = filter;
    panel.querySelectorAll('.version-group-head').forEach((head) => {
      head.onclick = () => {
        const key = activeRoot + ':' + head.querySelector('b').textContent;
        if (collapsed.has(key)) collapsed.delete(key);
        else collapsed.add(key);
        filter();
      };
    });
    filter();
  }
  bridge?.addEventListener('message', ({ data }) => {
    if (data?.type === 'pickedPath' && data.target === 'gameRoot' && data.path)
      post({ type: 'saveSettings', root: data.path });
  });
  window.McugDirectory = { mount };
})();
