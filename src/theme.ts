import { createSystem, defaultConfig, defineConfig } from "@chakra-ui/react";

/**
 * Chakra UI v3 主题：以默认系统为基础，把主色阶对齐到 Chakra 的 blue 预设。
 *
 * 项目原本的 accent 是自定义蓝 #326ea7，现已统一到 Chakra blue：
 *   blue.400 #60a5fa  blue.500 #3b82f6  blue.600 #2563eb  blue.700 #1d4ed8
 * 全站主色由语义色 colorPalette 引用，CSS 里的 --app-accent 系列也同步改成了 blue。
 *
 * preflight 关掉：项目已有自己的 base.css 做全局 reset，两边都跑会打架
 * （Chakra 的 reset 在 @layer 里，但字体/边框/按钮细节仍可能和本项目的
 *   user-select、按钮字体继承等冲突）。
 */
export const theme = createSystem(
  defaultConfig,
  defineConfig({
    preflight: false,
    theme: {
      tokens: {
        colors: {
          // colorPalette 作为主色别名，方便组件用 colorPalette.500 取到蓝色
          blue: {
            50: { value: "#eff6ff" },
            100: { value: "#dbeafe" },
            200: { value: "#bfdbfe" },
            300: { value: "#93c5fd" },
            400: { value: "#60a5fa" },
            500: { value: "#3b82f6" },
            600: { value: "#2563eb" },
            700: { value: "#1d4ed8" },
            800: { value: "#1e40af" },
            900: { value: "#1e3a8a" },
          },
        },
      },
      semanticTokens: {
        colors: {
          colorPalette: {
            solid: { value: "{colors.blue.500}" },
            contrast: { value: "#ffffff" },
            muted: { value: "{colors.blue.400}" },
            subtle: { value: "{colors.blue.100}" },
          },
          // 桥接项目 CSS 变量：新写的 Chakra 组件也能直接引用 --app-accent 系列
          accent: { value: "var(--app-accent)" },
          "accent-dark": { value: "var(--app-accent-dark)" },
          "accent-deep": { value: "var(--app-accent-deep)" },
        },
      },
    },
  }),
);
