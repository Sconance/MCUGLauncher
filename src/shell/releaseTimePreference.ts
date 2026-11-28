import { useEffect, useState } from "react";

/** 开发用：原版游戏版本列表是否显示发布时间的时分秒。 */
const KEY = "mcugl.vanillaReleaseShowTime";

const listeners = new Set<() => void>();

export function isVanillaReleaseTimeShown(): boolean {
  return localStorage.getItem(KEY) === "1";
}

export function setVanillaReleaseTimeShown(value: boolean): void {
  if (value) localStorage.setItem(KEY, "1");
  else localStorage.removeItem(KEY);
  listeners.forEach((listener) => listener());
}

/** 订阅原版列表发布时间显示开关；设置页改动后列表即时生效。 */
export function useVanillaReleaseTimeShown(): boolean {
  const [value, setValue] = useState(() => isVanillaReleaseTimeShown());

  useEffect(() => {
    const listener = () => setValue(isVanillaReleaseTimeShown());
    listeners.add(listener);
    return () => {
      listeners.delete(listener);
    };
  }, []);

  return value;
}

/** Windows/ICU 下部分时区 short 名称会回退为 GMT±N，这里补齐常见 IANA 时区的字母代号。 */
const IANA_TZ_ABBR: Record<string, string> = {
  "Asia/Shanghai": "CST",
  "Asia/Chongqing": "CST",
  "Asia/Harbin": "CST",
  "Asia/Urumqi": "CST",
  "Asia/Taipei": "CST",
  "Asia/Hong_Kong": "HKT",
  "Asia/Macau": "CST",
  "Asia/Tokyo": "JST",
  "Asia/Seoul": "KST",
  "Asia/Singapore": "SGT",
  "Asia/Kolkata": "IST",
  "Asia/Bangkok": "ICT",
  "Asia/Jakarta": "WIB",
  "Asia/Manila": "PHT",
  "Europe/Moscow": "MSK",
  "Etc/UTC": "UTC",
  UTC: "UTC",
};

function resolveTimeZoneAbbr(date: Date): string {
  const parts = new Intl.DateTimeFormat("en-US", { timeZoneName: "short" }).formatToParts(date);
  const shortName = parts.find((part) => part.type === "timeZoneName")?.value ?? "";
  if (/^[A-Z]{2,5}$/.test(shortName)) return shortName;
  const iana = Intl.DateTimeFormat().resolvedOptions().timeZone;
  if (iana && IANA_TZ_ABBR[iana]) return IANA_TZ_ABBR[iana];
  return shortName || "UTC";
}

function pad2(value: number): string {
  return String(value).padStart(2, "0");
}

/**
 * 格式化原版游戏版本发布时间：
 * - 默认（showTime = false）：`YYYY-MM-DD`
 * - 开启时分秒（showTime = true）：`YYYY-MM-DD  <时区字母代号>HH:MM:SS`
 */
export function formatVanillaReleaseTime(raw: string, showTime: boolean): string {
  const trimmed = raw.trim();
  if (!trimmed) return "";
  if (/^\d{4}-\d{2}-\d{2}$/.test(trimmed)) return trimmed;

  const date = new Date(trimmed);
  if (Number.isNaN(date.getTime())) return trimmed;

  const datePart = `${date.getFullYear()}-${pad2(date.getMonth() + 1)}-${pad2(date.getDate())}`;
  if (!showTime) return datePart;

  const timePart = `${pad2(date.getHours())}:${pad2(date.getMinutes())}:${pad2(date.getSeconds())}`;
  const tz = resolveTimeZoneAbbr(date);
  return `${datePart}  ${tz}${timePart}`;
}
