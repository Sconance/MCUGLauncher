import type { VanillaVersionInfo } from "../types/launcher";

/**
 * 版本主题分组，依据中文 Minecraft Wiki 的「Java版版本记录」：
 * 正式版按主题更新归组（1.21.4 = 苍园觉醒、1.20.5 = 犰甲狼兵 …），
 * 快照归入所属主题的更新，远古版本按 Alpha / Beta 阶段划分。
 *
 * 规则从新到旧匹配，命中即止；未收录的版本回落到「x.y 系列 / 年份快照」。
 */

export interface ThemeGroup {
  theme: string;
  items: VanillaVersionInfo[];
}

/** Wiki 动态拉取的「版本号 → 主题名」映射 */
export type VanillaThemeMap = Record<string, string>;

const CACHE_KEY = "mcugl.vanillaThemesCache.v3";

/** 读取前端本地缓存的 Wiki 主题映射，用于首帧即时渲染。 */
export function loadCachedVanillaThemes(): VanillaThemeMap {
  try {
    const raw = window.localStorage.getItem(CACHE_KEY);
    if (!raw) return {};
    const parsed = JSON.parse(raw) as unknown;
    if (parsed && typeof parsed === "object" && !Array.isArray(parsed)) {
      return parsed as VanillaThemeMap;
    }
  } catch {
    // 缓存损坏时忽略，回退到内置表
  }
  return {};
}

/** 将后端从 Wiki 获取的主题映射写入前端本地缓存。 */
export function saveCachedVanillaThemes(themes: VanillaThemeMap): void {
  if (Object.keys(themes).length === 0) return;
  try {
    window.localStorage.setItem(CACHE_KEY, JSON.stringify(themes));
  } catch {
    // 忽略配额等存储异常
  }
}

/** 正式版：版本号 → 主题更新（离线/兜底表，顺序有意义，新版本在前） */
const RELEASE_THEMES: Array<[RegExp, string]> = [
  [/^26\.4(\.\d+)?$/, "至深寒意"],
  [/^26\.3(\.\d+)?$/, "奔赴荒野"],
  [/^26\.2(\.\d+)?$/, "混沌立方"],
  [/^26\.1(\.\d+)?$/, "小鬼当家"],
  [/^1\.21\.11$/, "群骑纷争"],
  [/^1\.21\.(9|10)$/, "铜器时代"],
  [/^1\.21\.(6|7|8)$/, "追逐天空"],
  [/^1\.21\.5$/, "春意盎然"],
  [/^1\.21\.4$/, "苍园觉醒"],
  [/^1\.21\.(2|3)$/, "勇气之袋"],
  [/^1\.21(\.1)?$/, "棘巧试炼"],
  [/^1\.20\.(5|6)$/, "犰甲狼兵"],
  [/^1\.20\.(3|4)$/, "蝙蝠与陶罐"],
  [/^1\.20(\.1|\.2)?$/, "足迹与故事"],
  [/^1\.19(\.\d+)?$/, "荒野更新"],
  [/^1\.18(\.\d+)?$/, "洞穴与山崖（第二部分）"],
  [/^1\.17(\.\d+)?$/, "洞穴与山崖（第一部分）"],
  [/^1\.16(\.\d+)?$/, "下界更新"],
  [/^1\.15(\.\d+)?$/, "嗡嗡蜂群"],
  [/^1\.14(\.\d+)?$/, "村庄与掠夺"],
  [/^1\.13(\.\d+)?$/, "水域更新"],
  [/^1\.12(\.\d+)?$/, "多彩世界更新"],
  [/^1\.11(\.\d+)?$/, "探险更新"],
  [/^1\.10(\.\d+)?$/, "霜炙更新"],
  [/^1\.9(\.\d+)?$/, "战斗更新"],
  [/^1\.8(\.\d+)?$/, "缤纷更新"],
  [/^1\.7(\.\d+)?$/, "改变世界的更新"],
  [/^1\.6(\.\d+)?$/, "马匹更新"],
  [/^1\.5(\.\d+)?$/, "红石更新"],
  [/^1\.4(\.\d+)?$/, "骇人更新"],
  [/^1\.3(\.\d+)?$/, "1.3 版本"],
  [/^1\.2(\.\d+)?$/, "1.2 版本"],
  [/^1\.1$/, "1.1 版本"],
  [/^1\.0(\.\d+)?$/, "冒险更新"],
];

/** 愚人节快照版本（2026 年起常规快照改用 26.x-snapshot-n，26w* 为愚人节版本） */
const APRIL_FOOLS_SNAPSHOTS =
  /^(?:26w\d+.*|25w14craftmine|24w14potato|23w13a_or_b|22w13oneBlockAtATime|20w14infinite|3D Shareware v1\.34|1\.RV-Pre1|15w14a)$/i;

/** 判断是否为愚人节版本。 */
export function isAprilFoolsVersion(item: VanillaVersionInfo): boolean {
  return item.type === "snapshot" && APRIL_FOOLS_SNAPSHOTS.test(item.id);
}

/**
 * 周常快照（YYwWW[a-z]）→ 其归属的目标正式版本号。
 * 使用 `yy * 100 + ww` 数值区间匹配，再统一通过目标正式版推导主题名，
 * 确保快照版与正式版的主题分类始终一致。
 */
const SNAPSHOT_WEEK_TARGETS: Array<[number, number, string]> = [
  [2541, 2553, "1.21.11"], // 25w41a+
  [2531, 2540, "1.21.9"], // 25w31a–25w37a
  [2515, 2530, "1.21.6"], // 25w15a–25w21a
  [2502, 2514, "1.21.5"], // 25w02a–25w10a
  [2444, 2452, "1.21.4"], // 24w44a–24w46a
  [2433, 2443, "1.21.2"], // 24w33a–24w40a
  [2418, 2432, "1.21"], // 24w18a–24w21b
  [2351, 2417, "1.20.5"], // 23w51a–24w14a
  [2340, 2350, "1.20.3"], // 23w40a–23w46a
  [2312, 2339, "1.20"], // 23w12a–23w35a
  [2211, 2311, "1.19"], // 22w11a–23w07a
  [2137, 2210, "1.18"], // 21w37a–22w07a
  [2045, 2136, "1.17"], // 20w45a–21w20a
  [2006, 2044, "1.16"], // 20w06a–20w30a
  [1934, 2005, "1.15"], // 19w34a–19w46b
  [1843, 1933, "1.14"], // 18w43a–19w14b
  [1743, 1842, "1.13"], // 17w43a–18w33a
  [1706, 1742, "1.12"], // 17w06a–17w31a
  [1632, 1705, "1.11"], // 16w32a–16w50a
  [1620, 1631, "1.10"], // 16w20a–16w21b
  [1531, 1619, "1.9"], // 15w31a–16w15b
  [1402, 1530, "1.8"], // 14w02a–14w34d
  [1336, 1401, "1.7.2"], // 13w36a–13w49a
  [1316, 1335, "1.6.1"], // 13w16a–13w26a
  [1301, 1315, "1.5"], // 13w01a–13w12~
  [1232, 1252, "1.4.2"], // 12w32a–12w50b
  [1215, 1231, "1.3.1"], // 12w15a–12w30e
  [1203, 1214, "1.2.1"], // 12w03a–12w08a
  [1147, 1202, "1.1"], // 11w47a–12w01a
];

function parseReleaseTime(raw: string): number {
  const ms = Date.parse(raw);
  return Number.isNaN(ms) ? 0 : ms;
}

/** 判断是否为无主题名的占位标签（如「26.4 版本」「1.21 系列」）。 */
function isGenericThemeLabel(theme: string): boolean {
  return /^\d+\.\d+(?:\.\d+)?(?:\s*(?:版本|系列))?$/.test(theme.trim());
}

/** 优先从 Wiki 缓存中查找版本号（完整号或主次版本如 26.4）对应的真实主题名。 */
function lookupWikiTheme(version: string, wikiThemes?: VanillaThemeMap): string | null {
  if (!wikiThemes) return null;
  const exact = wikiThemes[version];
  if (exact && !isGenericThemeLabel(exact)) return exact;
  const majorMinor = /^(\d+\.\d+)\.\d+$/.exec(version)?.[1];
  if (majorMinor) {
    const fallback = wikiThemes[majorMinor];
    if (fallback && !isGenericThemeLabel(fallback)) return fallback;
  }
  return null;
}

/** 按正式版主题规则匹配版本号（优先 Wiki 缓存，其次内置兜底表）。 */
function matchReleaseTheme(version: string, wikiThemes?: VanillaThemeMap): string | null {
  const fromWiki = lookupWikiTheme(version, wikiThemes);
  if (fromWiki) return fromWiki;
  for (const [pattern, theme] of RELEASE_THEMES) {
    if (pattern.test(version)) return theme;
  }
  const series = /^(\d+\.\d+)/.exec(version);
  return series ? `${series[1]} 系列` : null;
}

/**
 * 推导快照版归属的目标正式版本号：
 * 1. 带目标版本号前缀的快照/预发布/候选版（如 26.4-snapshot-1、1.21.11-rc3）直接取前缀；
 * 2. 传统周常快照（YYwWW[a-z]）按开发周期映射到对应正式版；
 * 3. 其他未收录快照按发布时间归属到其后首个正式版。
 */
function snapshotTargetRelease(
  item: VanillaVersionInfo,
  releasesAsc?: Array<{ id: string; time: number }>,
): string | null {
  const prefixed = /^(\d+\.\d+(?:\.\d+)?)(?:[-_ ]|$)/.exec(item.id);
  if (prefixed) return prefixed[1];

  const weekly = /^(\d{2})w(\d{2})/.exec(item.id);
  if (weekly) {
    const code = Number.parseInt(weekly[1], 10) * 100 + Number.parseInt(weekly[2], 10);
    for (const [start, end, targetRelease] of SNAPSHOT_WEEK_TARGETS) {
      if (code >= start && code <= end) return targetRelease;
    }
  }

  if (releasesAsc && releasesAsc.length > 0) {
    const time = parseReleaseTime(item.released);
    if (time > 0) {
      const nextRelease = releasesAsc.find((release) => release.time >= time);
      if (nextRelease) return nextRelease.id;
    }
  }

  return null;
}

/** 单个版本所属的主题分组名。 */
export function versionTheme(
  item: VanillaVersionInfo,
  wikiThemes?: VanillaThemeMap,
  releasesAsc?: Array<{ id: string; time: number }>,
): string {
  if (item.type === "old_alpha") return "Alpha 阶段";
  if (item.type === "old_beta") return "Beta 阶段";

  if (item.type === "snapshot") {
    if (APRIL_FOOLS_SNAPSHOTS.test(item.id)) return "愚人节版本";

    const targetRelease = snapshotTargetRelease(item, releasesAsc);
    if (targetRelease) {
      const theme = matchReleaseTheme(targetRelease, wikiThemes);
      if (theme) return theme;
    }

    const weekly = /^(\d{2})w/.exec(item.id);
    return weekly ? `20${weekly[1]} 年快照` : "其他开发版本";
  }

  return matchReleaseTheme(item.id, wikiThemes) ?? "其他版本";
}

/**
 * 按发布时间严格倒序排列并按主题分组：
 * - 组内版本按发布时间从新到旧排序；
 * - 分组顺序也按各组最新版本发布时间从新到旧排列。
 */
export function groupVersions(
  items: VanillaVersionInfo[],
  wikiThemes?: VanillaThemeMap,
  allReleases?: VanillaVersionInfo[],
): ThemeGroup[] {
  const releasesAsc = allReleases
    ?.map((release) => ({ id: release.id, time: parseReleaseTime(release.released) }))
    .filter((release) => release.time > 0)
    .sort((left, right) => left.time - right.time);

  const sorted = [...items].sort(
    (left, right) => parseReleaseTime(right.released) - parseReleaseTime(left.released),
  );

  const groups = new Map<string, ThemeGroup>();
  for (const item of sorted) {
    const theme = versionTheme(item, wikiThemes, releasesAsc);
    const group = groups.get(theme);
    if (group) group.items.push(item);
    else groups.set(theme, { theme, items: [item] });
  }
  return [...groups.values()];
}
