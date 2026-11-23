import { invoke } from "@tauri-apps/api/core";
import type { ApiError } from "../types/generated";

/** 后端命令失败。`command` 保留原始命令名，便于定位。 */
export class BackendError extends Error {
  constructor(
    message: string,
    readonly command: string,
    readonly detail?: ApiError,
  ) {
    super(message);
    this.name = "BackendError";
  }
}

export function isTauri(): boolean {
  return typeof window !== "undefined" && "__TAURI_INTERNALS__" in window;
}

/**
 * `npm run dev:demo` / `npm run build:demo`（`vite --mode demo`）时为 true。
 * 标记由 vite 插件写进 index.html，dev 与 build 行为一致。
 */
export function isDemoMode(): boolean {
  return typeof window !== "undefined" && window.__MCUGL_DEMO__ === true;
}

/** 是否有一条可用的后端（真实 IPC 或演示后端）。 */
export function backendAvailable(): boolean {
  return isDemoMode() || isTauri();
}

function normalize(error: unknown): string {
  if (typeof error === "string") return error;
  if (error instanceof Error) return error.message;
  try {
    return JSON.stringify(error);
  } catch {
    return String(error);
  }
}

/** 把 Tauri 或演示后端的错误统一包装成 BackendError。 */
function asBackendError(error: unknown, command: string): BackendError {
  if (error instanceof BackendError) return error;
  if (typeof error === "object" && error !== null &&
      "code" in error && "message" in error && "retryable" in error &&
      typeof error.code === "string" && typeof error.message === "string" &&
      typeof error.retryable === "boolean") {
    return new BackendError(error.message, command, error as ApiError);
  }
  return new BackendError(normalize(error), command);
}

/**
 * 所有后端调用的唯一入口。
 * 命令名与参数约定见 docs/BACKEND_CONTRACT.md。
 *
 * 演示模式下按需载入 `src/demo/backend`（独立 chunk），正式构建只在
 * index.html 没有标记时使用真实 IPC。
 */
export async function call<T>(command: string, args?: Record<string, unknown>): Promise<T> {
  if (isDemoMode()) {
    try {
      const { demoInvoke } = await import("../demo/backend");
      return await demoInvoke<T>(command, args);
    } catch (error) {
      throw asBackendError(error, command);
    }
  }
  if (!isTauri()) {
    throw new BackendError(
      "后端未连接：请使用 npm run tauri dev 启动桌面应用，或用 npm run dev:demo 打开演示模式。",
      command,
    );
  }
  try {
    return await invoke<T>(command, args);
  } catch (error) {
    throw asBackendError(error, command);
  }
}
