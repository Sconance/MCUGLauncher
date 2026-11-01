(() => {
  const bridge = window.chrome?.webview;
  let favorites = [];
  const post = (value) => bridge?.postMessage(value);
  function mount() {
    document.querySelectorAll('[data-content-id]').forEach((card) => {
      if (card.querySelector('[data-bookmark]')) return;
      const id = card.dataset.contentId;
      const button = document.createElement('button');
      button.dataset.bookmark = '';
      button.className = 'bookmark-button';
      const selected = favorites.some((item) => item.id === id);
      button.textContent = selected ? '♥' : '♡';
      button.title = selected ? '取消收藏' : '收藏';
      button.setAttribute('aria-pressed', String(selected));
      button.onclick = () =>
        post({
          type: 'toggleBookmark',
          id,
          kind: card.dataset.contentKind,
          name: card.querySelector('b')?.textContent || id,
          description: card.querySelector('.content-desc')?.textContent || '',
          author: card.querySelector('.content-id span')?.textContent || '',
        });
      card.querySelector('.content-top')?.append(button);
    });
    const panel = document.querySelector('[data-bookmark-list]');
    if (!panel) return;
    panel.replaceChildren();
    if (!favorites.length) panel.textContent = '没有收藏的资源。';
    for (const item of favorites) {
      const row = document.createElement('div');
      row.className = 'task-row';
      const title = document.createElement('b');
      title.textContent = item.name;
      const description = document.createElement('p');
      description.textContent = item.description;
      const download = document.createElement('button');
      download.className = 'btn btn-primary';
      download.textContent = '加入下载队列';
      download.onclick = () => post({ type: 'installContent', id: item.id, category: item.kind });
      const remove = document.createElement('button');
      remove.className = 'btn btn-ghost';
      remove.textContent = '取消收藏';
      remove.onclick = () => post({ type: 'toggleBookmark', id: item.id });
      row.append(title, description, download, remove);
      panel.append(row);
    }
  }
  bridge?.addEventListener('message', (event) => {
    if (event.data?.type === 'state') {
      favorites = event.data.favorites || [];
      queueMicrotask(mount);
    }
  });
  window.McugBookmarks = { mount };
  mount();
})();
