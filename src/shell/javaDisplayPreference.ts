import { useEffect, useState } from "react";
import type { JavaInfo } from "../types/launcher";

/** 开发用：扫描到的本机 Java 列表是否显示架构（如 x64 / aarch64）。 */
const KEY = "mcugl.javaShowArch";

const listeners = new Set<() => void>();

export function isJavaArchShown(): boolean {
  return localStorage.getItem(KEY) === "1";
}

export function setJavaArchShown(value: boolean): void {
  if (value) localStorage.setItem(KEY, "1");
  else localStorage.removeItem(KEY);
  listeners.forEach((listener) => listener());
}

/** 订阅「显示本机 Java 架构」开关；设置页改动后列表即时生效。 */
export function useJavaArchShown(): boolean {
  const [value, setValue] = useState(() => isJavaArchShown());

  useEffect(() => {
    const listener = () => setValue(isJavaArchShown());
    listeners.add(listener);
    return () => {
      listeners.delete(listener);
    };
  }, []);

  return value;
}

/** 去除 Windows 规范化路径自带的 `\\?\`（及 `\\?\UNC\`）前缀。 */
export function cleanJavaPath(path: string): string {
  const trimmed = path.trim();
  if (/^\\\\\?\\UNC\\/i.test(trimmed)) return `\\\\${trimmed.slice(8)}`;
  if (trimmed.startsWith("\\\\?\\")) return trimmed.slice(4);
  return trimmed;
}

/** 简化常见 Java 发行商名称。 */
export function simplifyJavaVendor(vendor: string): string {
  const trimmed = vendor.trim();
  if (/^microsoft$/i.test(trimmed)) return "MS";
  if (/^oracle\s+corporation$/i.test(trimmed)) return "Oracle";
  return trimmed;
}

/**
 * 格式化本机 Java 显示名称：
 * - 默认（showArch = false）：`Java <major> (<vendor>)`
 * - 开启架构（showArch = true）：`Java <major> (<vendor> , <arch>)`
 */
export function formatJavaLabel(
  item: Pick<JavaInfo, "major" | "vendor" | "arch">,
  showArch: boolean,
): string {
  const vendor = simplifyJavaVendor(item.vendor);
  const arch = showArch ? item.arch.trim() : "";
  const meta = [vendor, arch].filter((part) => part.length > 0).join(" , ");
  return meta.length > 0 ? `Java ${item.major} (${meta})` : `Java ${item.major}`;
}
