// Data-source tabs own drafts and test results; tests never change the active configuration.
(() => {
  let tab = 'default';
  let draft;
  let writeDraft;
  let visit = '';
  let sequence = 0;
  const tests = new Map();
  const pending = new Map();
  const post = (message) => window.chrome?.webview?.postMessage(message);
  const defaults = () =>
    window.state?.defaultDataSources || Object.fromEntries(DS.map((d) => [d.key, d.url]));
  const values = () =>
    tab === 'default' ? defaults() : (draft ||= { ...window.state?.dataSources });
  function page() {
    const sources = values();
    const rows = DS.map(
      (d) =>
        `<div class="ds-row" data-source="${d.key}"><div class="ds-label"><b>${escapeHtml(d.label)}</b><span>${escapeHtml(d.hint)}</span></div><div class="ds-input-wrap"><input class="input" aria-label="${escapeHtml(d.label)}地址" value="${escapeHtml(sources[d.key] || '')}" ${tab === 'default' ? 'readonly' : ''}></div><button class="btn btn-ghost ds-test">测试</button><div class="ds-result">尚未测试</div></div>`
    ).join('');
    return shell(
      'settings',
      settingsSidebar('datasource'),
      `${pageHead('数据源设置', '打开页面自动测试，也可逐项手动测试；测试不会切换正在使用的数据源。')}
      <div class="ds-tabs" role="tablist" aria-label="数据源类型"><button class="btn ${tab === 'default' ? 'btn-primary' : 'btn-ghost'}" role="tab" aria-selected="${tab === 'default'}" data-source-tab="default">默认源</button><button class="btn ${tab === 'custom' ? 'btn-primary' : 'btn-ghost'}" role="tab" aria-selected="${tab === 'custom'}" data-source-tab="custom">自定义源</button></div>
      <section class="card settings-card" data-source-page="${tab}"><div class="ds-rows">${rows}</div>${tab === 'custom' ? `<div class="ds-write"><div class="ds-label">写入接口（可选）</div><input class="input" value="${escapeHtml(writeDraft ?? window.state?.writeEndpoint ?? '')}" placeholder="留空只保存本地内容"></div>` : ''}<div class="ds-footer"><button class="btn btn-primary" data-source-save>${tab === 'default' ? '使用默认源' : '保存自定义源'}</button></div></section>`
    );
  }
  function paint(row, test) {
    const label = row.querySelector('.ds-result');
    label.className = `ds-result ${test?.ok ? 'ok' : test ? 'bad' : ''}`;
    label.textContent = test
      ? test.ok
        ? `连接正常 · ${test.ms} ms`
        : test.message || '连接失败'
      : '尚未测试';
  }
  function test(row) {
    const name = row.dataset.source;
    const url = row.querySelector('input').value.trim();
    const requestId = `source-${++sequence}`;
    pending.set(name, { requestId, url, tab });
    row.querySelector('.ds-result').className = 'ds-result';
    row.querySelector('.ds-result').textContent = url ? '测试中…' : '未填写地址';
    if (url) post({ type: 'testSource', name, url, requestId });
  }
  function mount() {
    const root = document.querySelector('[data-source-page]');
    if (!root) {
      visit = '';
      return;
    }
    if (root.dataset.bound) return;
    root.dataset.bound = 'true';
    document.querySelectorAll('[data-source-tab]').forEach((button) =>
      button.addEventListener('click', () => {
        tab = button.dataset.sourceTab;
        window.mcugGo('?screen=datasource');
      })
    );
    const automatic = visit !== tab;
    visit = tab;
    root.querySelectorAll('.ds-row').forEach((row) => {
      const input = row.querySelector('input');
      paint(row, tests.get(JSON.stringify([tab, row.dataset.source, input.value])));
      row.querySelector('.ds-test').addEventListener('click', () => test(row));
      input.addEventListener('input', () => {
        draft[row.dataset.source] = input.value;
        row.querySelector('.ds-result').className = 'ds-result';
        row.querySelector('.ds-result').textContent = '地址已修改，尚未测试';
      });
      if (automatic) test(row);
    });
    root
      .querySelector('.ds-write input')
      ?.addEventListener('input', (event) => (writeDraft = event.target.value));
    root.querySelector('[data-source-save]').addEventListener('click', () => {
      if (tab === 'default') post({ type: 'resetDataSources' });
      else
        post({
          type: 'saveSettings',
          dataSources: { ...draft },
          writeEndpoint: writeDraft ?? window.state?.writeEndpoint ?? '',
        });
    });
  }
  function result(message) {
    const request = pending.get(message.name);
    if (!request || request.url !== message.url || message.requestId !== request.requestId) return;
    pending.delete(message.name);
    tests.set(JSON.stringify([request.tab, message.name, message.url]), message);
    if (request.tab !== tab) return;
    const row = document.querySelector(`[data-source="${message.name}"]`);
    if (row?.querySelector('input').value.trim() === message.url) paint(row, message);
  }
  // Saved state does not overwrite an in-progress custom draft or the read-only defaults.
  function update() {
    mount();
  }
  window.McugSources = { page, mount, result, update };
})();
