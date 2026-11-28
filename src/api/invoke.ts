import {
  createKernel,
  HOST_COMMANDS,
  kernelAvailable,
  KernelError,
  kernelRuntime,
  kernelTransport,
  type KernelTransport,
} from "@mcugl/frontpack-sdk";
import type { ApiError } from "../types/generated";

export { KernelError };

/** 宿主特权命令集合（不在对第三方公开的业务内核方法表内）。 */
export type HostCommand = (typeof HOST_COMMANDS)[number];

const HOST_COMMAND_SET = new Set<string>(HOST_COMMANDS);

/** 后端命令失败。继承自 `KernelError`，保留 `command` / `detail` 兼容属性。 */
export class BackendError extends KernelError {
  declare readonly detail: ApiError | undefined;

  constructor(
    message: string,
    command: string,
    detail?: ApiError,
  ) {
    super(message, {
      code: detail?.code ?? "UNKNOWN",
      command,
      retryable: detail?.retryable ?? false,
      fieldErrors: detail?.fieldErrors,
      detail,
    });
    this.name = "BackendError";
  }
}

export function isTauri(): boolean {
  return kernelAvailable();
}

/**
 * `npm run dev:demo` / `npm run build:demo`（`vite --mode demo`）时为 true。
 * 标记由 vite 插件写进 index.html，dev 与 build 行为一致。
 */
export function isDemoMode(): boolean {
  return typeof window !== "undefined" && window.__MCUGL_DEMO__ === true;
}

/** 是否有一条可用的后端（真实内核 IPC 或演示传输）。 */
export function backendAvailable(): boolean {
  return isDemoMode() || isTauri();
}

/** 读取宿主注入的运行时信息（内置官方界面为 `null`，以 `.mcuglf` 加载时为包元数据）。 */
export function getFrontendRuntime() {
  return kernelRuntime();
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

function isApiError(error: unknown): error is ApiError {
  return (
    typeof error === "object" &&
    error !== null &&
    "code" in error &&
    "message" in error &&
    "retryable" in error &&
    typeof error.code === "string" &&
    typeof error.message === "string" &&
    typeof error.retryable === "boolean"
  );
}

/** 把 SDK / 传输层错误统一包装成 BackendError（兼容现有错误处理）。 */
function asBackendError(error: unknown, command: string): BackendError {
  if (error instanceof BackendError) return error;
  if (error instanceof KernelError) {
    const detail = isApiError(error.detail) ? error.detail : undefined;
    return new BackendError(error.message, command, detail);
  }
  if (isApiError(error)) {
    return new BackendError(error.message, command, error);
  }
  return new BackendError(normalize(error), command);
}

/**
 * 演示模式传输层：按需加载 `src/demo/backend`（独立 chunk），以标准
 * `KernelTransport` 形态注入 `@mcugl/frontpack-sdk`。
 */
const demoTransport: KernelTransport = {
  invoke: async <T>(command: string, args?: Record<string, unknown>): Promise<T> => {
    const { demoInvoke } = await import("../demo/backend");
    return demoInvoke<T>(command, args);
  },
};

function createClient() {
  if (isDemoMode()) {
    return createKernel({ transport: demoTransport });
  }
  return createKernel({ transport: kernelTransport() ?? undefined });
}

async function invokeViaKernel<T>(command: string, args?: Record<string, unknown>): Promise<T> {
  if (!backendAvailable()) {
    throw new BackendError(
      "后端未连接：请使用 npm run tauri dev 启动桌面应用，或用 npm run dev:demo 打开演示模式。",
      command,
    );
  }
  try {
    const client = createClient();
    const rawCall = client.call as (name: string, payload?: Record<string, unknown>) => Promise<T>;
    return await rawCall(command, args);
  } catch (error) {
    throw asBackendError(error, command);
  }
}

/**
 * 业务内核命令唯一入口（对应 `mcugl.kernel/v1` 公开方法表）。
 * 命令名与参数约定见 docs/KERNEL_API_V1.md 与 docs/BACKEND_CONTRACT.md。
 */
export async function call<T>(command: string, args?: Record<string, unknown>): Promise<T> {
  if (HOST_COMMAND_SET.has(command)) {
    throw new BackendError(
      `命令 "${command}" 属于宿主管理接口，请改用 callHost() 调用以保持内核与宿主边界清晰。`,
      command,
    );
  }
  return invokeViaKernel<T>(command, args);
}

/**
 * 宿主特权管理命令唯一入口（仅用于前端包管理与内核自描述 `HOST_COMMANDS`）。
 */
export async function callHost<T>(command: HostCommand, args?: Record<string, unknown>): Promise<T> {
  if (!HOST_COMMAND_SET.has(command)) {
    throw new BackendError(`"${command}" 不是已声明的宿主管理命令。`, command);
  }
  return invokeViaKernel<T>(command, args);
}
