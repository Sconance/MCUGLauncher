import { defineConfig } from "vite";
import react from "@vitejs/plugin-react";
// @ts-expect-error type error without @types/node package
import process from "node:process";

const host = process.env.TAURI_DEV_HOST;

/** 演示模式标记：写进 index.html，dev 与 build 的行为完全一致。 */
const DEMO_FLAG = '<script>window.__MCUGL_DEMO__ = true;</script>';

// https://vite.dev/config/
export default defineConfig(({ mode }) => {
  const demo = mode === "demo";

  return {
    plugins: [
      react(),
      {
        name: "mcugl-demo-flag",
        transformIndexHtml(html) {
          return demo ? html.replace("<head>", `<head>${DEMO_FLAG}`) : html;
        },
      },
    ],

    // Vite options tailored for Tauri development and only applied in `tauri dev` or `tauri build`
    //
    // 1. prevent Vite from obscuring rust errors
    clearScreen: false,
    // 2. tauri expects a fixed port, fail if that port is not available
    server: {
      // 演示模式单独用 5174，避免和 tauri dev 的 1420 抢端口
      port: demo ? 5174 : 1420,
      strictPort: true,
      host: host || false,
      hmr: host
        ? {
            protocol: "ws",
            host,
            port: 1421,
          }
        : undefined,
      watch: {
        // 3. tell Vite to ignore watching `src-tauri`
        ignored: ["**/src-tauri/**"],
      },
    },
  };
});
