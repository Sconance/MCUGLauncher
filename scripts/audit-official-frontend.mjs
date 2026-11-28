#!/usr/bin/env node
/**
 * 官方 UI 逻辑解耦审计脚本：
 * 1. 确保除窗口桥接 `src/api/window.ts` 外，官方 UI 不直接引用 `@tauri-apps/*`；
 * 2. 确保 `src/api/commands/*.ts` 中所有 `call("...")` 全部属于 `kernel.rs::METHODS` 公开业务内核表；
 * 3. 确保 `callHost("...")` 仅出现在宿主管理模块 `src/api/commands/frontend.ts` 且全部属于 `HOST_COMMANDS`；
 * 4. 确保 `public/frontend.json` 符合 `.mcuglf` 标准前端包清单规范。
 */

import { existsSync, readdirSync, readFileSync, statSync } from "node:fs";
import { join, relative, sep } from "node:path";
import { HOST_COMMANDS } from "@mcugl/frontpack-sdk";

const ALLOWED_TAURI_FILES = new Set(["src/api/window.ts", "src/api/shellBridge.ts"]);
const ALLOWED_HOST_CALL_FILES = new Set(["src/api/commands/frontend.ts"]);

function walk(dir, acc = []) {
  for (const entry of readdirSync(dir, { withFileTypes: true })) {
    const full = join(dir, entry.name);
    if (entry.isDirectory()) {
      walk(full, acc);
    } else if (entry.isFile() && /\.(ts|tsx|js|jsx|mjs)$/.test(entry.name)) {
      acc.push(full);
    }
  }
  return acc;
}

function rel(file) {
  return relative(process.cwd(), file).split(sep).join("/");
}

function readKernelMethods() {
  const kernelPath = "src-tauri/src/commands/kernel.rs";
  const source = readFileSync(kernelPath, "utf8");
  const start = source.indexOf("METHODS");
  const arrayStart = source.indexOf("&[", start);
  const arrayEnd = source.indexOf("];", arrayStart);
  if (start < 0 || arrayStart < 0 || arrayEnd < 0) {
    throw new Error(`无法解析 ${kernelPath} 中的 METHODS 表`);
  }
  const block = source.slice(arrayStart, arrayEnd);
  return new Set([...block.matchAll(/"([^"\\]+)"/g)].map((m) => m[1]));
}

const problems = [];
const kernelMethods = readKernelMethods();
const hostCommandSet = new Set(HOST_COMMANDS);
const srcFiles = walk("src");

// 1. 检查 @tauri-apps/* 直接依赖边界
for (const file of srcFiles) {
  const normalized = rel(file);
  if (ALLOWED_TAURI_FILES.has(normalized)) continue;
  const content = readFileSync(file, "utf8");
  if (/from\s+["']@tauri-apps\//.test(content) || /import\(\s*["']@tauri-apps\//.test(content)) {
    problems.push(`禁止在 ${normalized} 中直接引入 @tauri-apps/*（应通过 src/api/invoke.ts 或 src/api/window.ts）。`);
  }
}

// 2 & 3. 检查 call(...) 与 callHost(...) 的命令边界
const usedKernelMethods = new Set();
const usedHostCommands = new Set();
const commandFiles = walk("src/api/commands");

for (const file of commandFiles) {
  const normalized = rel(file);
  const content = readFileSync(file, "utf8");

  for (const match of content.matchAll(/\bcall\s*(?:<[\s\S]*?>)?\s*\(\s*"([^"]+)"/g)) {
    const cmd = match[1];
    usedKernelMethods.add(cmd);
    if (!kernelMethods.has(cmd)) {
      problems.push(`${normalized} 调用了未登记在 kernel.rs::METHODS 的业务命令："${cmd}"`);
    }
  }

  for (const match of content.matchAll(/\bcallHost\s*(?:<[\s\S]*?>)?\s*\(\s*"([^"]+)"/g)) {
    const cmd = match[1];
    usedHostCommands.add(cmd);
    if (!ALLOWED_HOST_CALL_FILES.has(normalized)) {
      problems.push(`${normalized} 不应直接调用宿主特权命令 "${cmd}"（请收敛到 src/api/commands/frontend.ts）。`);
    }
    if (!hostCommandSet.has(cmd)) {
      problems.push(`${normalized} 调用了未在 HOST_COMMANDS 声明的宿主命令："${cmd}"`);
    }
  }
}

// 4. 检查 public/frontend.json
const manifestPath = "public/frontend.json";
if (!existsSync(manifestPath)) {
  problems.push(`缺少官方前端包清单：${manifestPath}`);
} else {
  try {
    const manifest = JSON.parse(readFileSync(manifestPath, "utf8"));
    for (const key of ["identifier", "name", "version", "kernelApi", "entry"]) {
      if (typeof manifest[key] !== "string" || manifest[key].trim() === "") {
        problems.push(`${manifestPath} 缺少必填字符串字段：${key}`);
      }
    }
    if (manifest.icon) {
      const iconPath = join("public", manifest.icon);
      if (!existsSync(iconPath) || !statSync(iconPath).isFile()) {
        problems.push(`${manifestPath} 指定的图标不存在：${iconPath}`);
      }
    }
  } catch (error) {
    problems.push(`解析 ${manifestPath} 失败：${error.message}`);
  }
}

if (problems.length > 0) {
  console.error("[frontend:audit] 官方 UI 逻辑解耦审计未通过：");
  for (const problem of problems) {
    console.error(`  - ${problem}`);
  }
  process.exit(1);
}

console.log(
  `[frontend:audit] 通过：业务内核命令 ${usedKernelMethods.size}/${kernelMethods.size}，宿主管理命令 ${usedHostCommands.size}/${hostCommandSet.size}，Tauri 直连仅限 ${[...ALLOWED_TAURI_FILES].join(", ")}。`,
);
