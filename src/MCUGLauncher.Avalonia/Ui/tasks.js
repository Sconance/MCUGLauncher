// Download UI reads only persisted host task state; it owns no network transfers.
(() => {
  const bridge = window.chrome?.webview;
  let tasks = [];
  const labels = {
    Running: '进行中',
    Queued: '排队中',
    Paused: '已暂停',
    Pausing: '正在暂停',
    Cancelling: '正在取消',
    Completed: '已完成',
    Cancelled: '已取消',
    Failed: '失败',
    Interrupted: '上次中断',
  };
  const escape = (value) =>
    String(value ?? '').replace(
      /[&<>"']/g,
      (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[c]
    );
  const post = (value) => bridge?.postMessage(value);
  function mount() {
    const bar = document.querySelector('.titlebar');
    if (bar && !bar.querySelector('[data-task-summary]')) {
      const summary = document.createElement('button');
      summary.dataset.taskSummary = '';
      summary.className = 'task-summary';
      summary.title = '查看下载任务';
      summary.onclick = () => window.mcugGo?.('?screen=settings&panel=tasks');
      const controls = bar.querySelector('.win-ctrl');
      bar.insertBefore(summary, controls || null);
    }
    const active = tasks.filter((t) =>
      ['Running', 'Queued', 'Pausing', 'Cancelling'].includes(t.state)
    );
    const current = active.find((t) => t.state === 'Running');
    const summary = document.querySelector('[data-task-summary]');
    if (summary)
      summary.innerHTML = `↓ 下载任务 ${active.length}${current ? `<progress max="100" ${current.percent == null ? '' : `value="${Number(current.percent)}"`}></progress>` : ''}`;
    const panel = document.querySelector('[data-task-list]');
    if (!panel) return;
    panel.innerHTML =
      tasks
        .map((t) => {
          const active = ['Running', 'Queued', 'Paused', 'Failed', 'Interrupted'].includes(t.state);
          const pending = ['Paused', 'Failed', 'Interrupted'].includes(t.state);
          const button = (action, text) =>
            `<button class="btn btn-ghost btn-sm" data-task-action="${action}" data-task-id="${escape(t.id)}">${text}</button>`;
          return `<article class="task-row"><div class="task-row-heading"><b>${escape(t.name)}</b><span>${escape(labels[t.state] || t.state)}</span></div><p>${escape(t.message)}</p>${t.state === 'Running' ? `<progress max="100" ${t.percent == null ? '' : `value="${Number(t.percent)}"`}></progress>` : ''}<div class="settings-actions">${t.committing ? '正在提交文件，请稍候' : pending ? button('resume', '继续 / 重试') + button('cancel', '取消') : active ? button('pause', '暂停') + button('cancel', '取消') : button('delete', '删除记录')}</div></article>`;
        })
        .join('') || '<div class="empty-line">没有下载任务。</div>';
    panel.querySelectorAll('[data-task-action]').forEach(
      (button) =>
        (button.onclick = () =>
          post({
            type: 'taskAction',
            id: button.dataset.taskId,
            action: button.dataset.taskAction,
          }))
    );
  }
  window.McugTasks = { mount };
  bridge?.addEventListener('message', (event) => {
    if (event.data?.type !== 'tasks') return;
    tasks = Array.isArray(event.data.items) ? event.data.items : [];
    mount();
  });
  mount();
  post({ type: 'requestTasks' });
})();
