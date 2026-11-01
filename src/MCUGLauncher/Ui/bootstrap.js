// Loads preview fixtures only outside WebView2, then starts the app and feature bindings.
(async () => {
  const load = (file) =>
    new Promise((resolve, reject) => {
      const script = document.createElement('script');
      script.src = file;
      script.onload = resolve;
      script.onerror = () => reject(new Error('界面脚本加载失败：' + file));
      document.body.append(script);
    });
  try {
    if (!window.chrome?.webview) await load('preview-data.js');
    for (const file of [
      'app.js',
      'tasks.js',
      'install.js',
      'games.js',
      'bookmarks.js',
      'saves.js',
      'instance-tools.js',
      'settings-actions.js',
      'directory.js',
      'community-actions.js',
      'host.js',
    ])
      await load(file);
  } catch (error) {
    document.getElementById('app').textContent = error.message;
  }
})();
