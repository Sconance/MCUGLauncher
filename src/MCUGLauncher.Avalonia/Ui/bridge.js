// Bridges Avalonia NativeWebView's invokeCSharpAction to the chrome.webview API used by the UI.
(() => {
  if (typeof invokeCSharpAction !== 'function' || window.chrome?.webview) return;
  const listeners = [];
  window.chrome = {
    webview: {
      postMessage: (message) => invokeCSharpAction(JSON.stringify(message)),
      addEventListener: (type, handler) => {
        if (type === 'message') listeners.push(handler);
      },
      removeEventListener: (type, handler) => {
        const index = listeners.indexOf(handler);
        if (type === 'message' && index >= 0) listeners.splice(index, 1);
      },
    },
  };
  window.mcugReceive = (text) => {
    const data = typeof text === 'string' ? JSON.parse(text) : text;
    for (const handler of listeners) handler({ data });
  };
})();
