// Query routing and shared rendering helpers. Keep feature behavior in its own module.
function escapeHtml(value) {
  return String(value ?? '').replace(
    /[&<>"']/g,
    (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' })[c]
  );
}

function parseQuery(search) {
  const params = new URLSearchParams(search);
  return {
    screen: params.get('screen') || 'home',
    v: params.get('v') || '',
    id: params.get('id') || '',
    state: params.get('state') || 'normal',
    type: params.get('type') || 'server',
    anon: params.get('anon') === '1',
    quote: params.get('quote') || 'text',
    qi: params.has('qi')
      ? Math.max(0, Math.min(4, Number(params.get('qi')) || 0))
      : params.get('quote') === 'image' || params.get('quote') === 'fallback'
        ? 1
        : 0,
    ann: Math.max(0, Math.min(2, Number(params.get('ann')) || 0)),
    menu: params.get('menu') || '',
    modal: params.get('modal') || '',
    endpoint: params.get('endpoint') === '1',
    merr: params.get('merr') || '',
    tip: params.get('tip') === '1',
    panel: params.get('panel') || '',
    mode: params.get('mode') || 'instance',
    switchMenu: params.get('switch') === '1',
    inst: params.get('inst') || '',
    skin: params.get('skin') || '',
    img: params.get('img') === '1',
  };
}

const Q = parseQuery(location.search);

function go(url) {
  const next = new URL(url, location.href);
  if (next.origin !== location.origin || next.pathname !== location.pathname) {
    location.href = next.href;
    return;
  }
  if (next.search !== location.search) {
    history.pushState(null, '', next.pathname + next.search);
  }
  Object.assign(Q, parseQuery(next.search));
  render();
  window.scrollTo(0, 0);
}

window.mcugGo = go;

document.addEventListener(
  'click',
  (event) => {
    const link = event.target.closest('a[href]');
    if (
      !link ||
      link.target ||
      event.defaultPrevented ||
      event.button !== 0 ||
      event.metaKey ||
      event.ctrlKey ||
      event.shiftKey ||
      event.altKey
    )
      return;
    const next = new URL(link.href, location.href);
    if (next.origin !== location.origin || next.pathname !== location.pathname) return;
    event.preventDefault();
    go(next.href);
  },
  true
);

window.addEventListener('popstate', () => {
  Object.assign(Q, parseQuery(location.search));
  render();
});

const icon = (name, cls = '') =>
  `<svg class="ico ${cls}" aria-hidden="true"><use href="#i-${name}"></use></svg>`;

const BrandLogo = () =>
  `<img class="brand-logo" src="assets/logo.png" width="34" height="34" alt="">`;

const typeMeta = {
  server: { label: '服务器', icon: 'server' },
  material: { label: '材质包', icon: 'palette' },
  shader: { label: '光影', icon: 'sun' },
};

const tileClass = (c) => `tile-${c || 'green'}`;

const appVersion = () => window.state?.appVersion || '预览';

function render() {
  let html = '';
  if (Q.screen === 'download') html = downloadPage();
  else if (Q.screen === 'mods') html = modListPage();
  else if (Q.screen === 'mod') html = modDetailPage();
  else if (Q.screen === 'catalog') html = catalogPage(Q.type || 'mod', Q.q || '');
  else if (Q.screen === 'manage') html = versionManagePage();
  else if (Q.screen === 'version') html = versionSettingsPage();
  else if (Q.screen === 'settings') html = settingsPage(Q.panel || 'launch');
  else if (Q.screen === 'options') html = optionsPage();
  else if (Q.screen === 'admin') html = adminPage();
  else if (Q.screen === 'datasource') html = datasourcePage();
  else html = homePage();
  document.getElementById('app').innerHTML = html;
  bind();
  window.McugTasks?.mount();
  window.McugInstall?.mount();
  window.McugGames?.mount();
  window.McugBookmarks?.mount();
  window.McugSaves?.mount();
  window.McugInstances?.mount();
  window.McugDirectory?.mount();
  window.McugCommunityActions?.mount();
  window.McugLaunchOptions?.mount();
  window.McugAvatars?.mount();
}

function bind() {
  const anon = document.querySelector('[data-anon]');
  if (anon)
    anon.addEventListener('click', () => {
      const next = !anon.classList.contains('on');
      if (window.chrome?.webview) {
        window.chrome.webview.postMessage({ type: 'saveSettings', anonymous: next });
        return;
      }
      const u = new URL(location.href);
      u.searchParams.set('screen', 'options');
      if (next) u.searchParams.set('anon', '1');
      else u.searchParams.delete('anon');
      if (Q.state === 'verified') u.searchParams.set('state', 'verified');
      go(u.toString());
    });
  const verify = document.querySelector('[data-verify]');
  if (verify)
    verify.addEventListener('click', () => {
      if (window.chrome?.webview) return;
      const u = new URL(location.href);
      u.searchParams.set('screen', 'options');
      u.searchParams.set('state', 'verified');
      go(u.toString());
    });
  const reset = document.querySelector('[data-verify-reset]');
  if (reset)
    reset.addEventListener('click', () => {
      const u = new URL(location.href);
      u.searchParams.set('screen', 'options');
      u.searchParams.delete('state');
      go(u.toString());
    });
  const help = document.querySelector('.help-dot');
  if (help) help.addEventListener('click', () => help.parentElement.classList.toggle('open'));
  const rail = document.querySelector('.server-rail');
  const railPrev = document.querySelector('[data-rail-prev]');
  const railNext = document.querySelector('[data-rail-next]');
  if (rail && railPrev)
    railPrev.addEventListener('click', () => rail.scrollBy({ left: -206, behavior: 'smooth' }));
  if (rail && railNext)
    railNext.addEventListener('click', () => rail.scrollBy({ left: 206, behavior: 'smooth' }));
  const quote = document.querySelector('.quote-strip');
  if (quote && !Q.modal)
    quote.addEventListener('click', (event) => {
      if (event.target.closest('button, a')) return;
      const u = new URL(location.href);
      u.searchParams.set('screen', 'home');
      u.searchParams.set('qi', String((Q.qi + 1) % quotes.length));
      u.searchParams.delete('quote');
      go(u.toString());
    });
  const prev = document.querySelector('[data-ann-prev]');
  const next = document.querySelector('[data-ann-next]');
  if (prev)
    prev.addEventListener('click', () => {
      const u = new URL(location.href);
      u.searchParams.set('ann', String((Q.ann + announcements.length - 1) % announcements.length));
      go(u.toString());
    });
  if (next)
    next.addEventListener('click', () => {
      const u = new URL(location.href);
      u.searchParams.set('ann', String((Q.ann + 1) % announcements.length));
      go(u.toString());
    });
  document.querySelector('[data-recs-manage]')?.addEventListener('click', () => {
    go('?screen=home&modal=recs-manage');
  });
  document.querySelectorAll('[data-delete-announcement]').forEach((button) => {
    button.addEventListener('click', () => {
      const id = button.getAttribute('data-delete-announcement');
      if (!confirm('确定删除这条公告？')) return;
      const index = announcements.findIndex((a) => a.id === id);
      if (index >= 0) announcements.splice(index, 1);
      window.chrome?.webview?.postMessage({ type: 'saveAnnouncements', items: announcements });
    });
  });
  document.querySelectorAll('[data-delete-rec]').forEach((button) => {
    button.addEventListener('click', () => {
      const id = button.getAttribute('data-delete-rec');
      if (!confirm('确定删除这条群友推荐？')) return;
      const index = recs.findIndex((r) => r.id === id);
      if (index >= 0) recs.splice(index, 1);
      window.chrome?.webview?.postMessage({ type: 'saveRecommendations', items: recs });
    });
  });
  document.querySelectorAll('[data-server-menu]').forEach((menu) =>
    menu.addEventListener('click', () => {
      const id = menu.getAttribute('data-server-menu');
      const u = new URL(location.href);
      if (Q.menu === id) u.searchParams.delete('menu');
      else u.searchParams.set('menu', id);
      go(u.toString());
    })
  );
  document.querySelectorAll('[data-delete-server]').forEach((button) => {
    button.addEventListener('click', () => {
      const address = button.getAttribute('data-delete-server');
      if (confirm(`确定移除服务器“${address}”？`)) {
        window.chrome?.webview?.postMessage({ type: 'deleteServer', address });
      }
    });
  });
}
render();
