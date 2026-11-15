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

function normalize(error: unknown): string {
  if (typeof error === "string") return error;
  if (error instanceof Error) return error.message;
  try {
    return JSON.stringify(error);
  } catch {
    return String(error);
  }
}

/**
 * 所有后端调用的唯一入口。
 * 命令名与参数约定见 docs/BACKEND_CONTRACT.md。
 */
export async function call<T>(command: string, args?: Record<string, unknown>): Promise<T> {
  if (!isTauri()) {
    throw new BackendError("后端未连接：请使用 npm run tauri dev 启动桌面应用。", command);
  }
  try {
    return await invoke<T>(command, args);
  } catch (error) {
    if (typeof error === "object" && error !== null &&
        "code" in error && "message" in error && "retryable" in error &&
        typeof error.code === "string" && typeof error.message === "string" &&
        typeof error.retryable === "boolean") {
      throw new BackendError(error.message, command, error as ApiError);
    }
    throw new BackendError(normalize(error), command);
  }
}
