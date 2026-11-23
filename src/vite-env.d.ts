/// <reference types="vite/client" />

interface Window {
  /** 只有 `npm run dev:demo` / `npm run build:demo` 的 index.html 会注入它。 */
  __MCUGL_DEMO__?: boolean;
}
