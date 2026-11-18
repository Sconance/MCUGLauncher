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

/** 正式版：版本号 → 主题更新（顺序有意义，新版本在前） */
const RELEASE_THEMES: Array<[RegExp, string]> = [
  [/^26\.4(\.\d+)?$/, "奔赴荒野"],
  [/^26\.3(\.\d+)?$/, "混沌立方"],
  [/^26\.2(\.\d+)?$/, "小鬼当家"],
  [/^26\.1(\.\d+)?$/, "26.1 版本"],
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

/** 快照：只收录几个能确定的主题区间，其余按年份归组 */
const SNAPSHOT_THEMES: Array<[RegExp, string]> = [
  [/^24w(4[4-9]|[5-9]\d)[a-z]$/, "苍园觉醒"],
  [/^24w(3[3-9]|4[0-2])[a-z]$/, "勇气之袋"],
  [/^23w(4\d|5[0-2])[a-z]$/, "蝙蝠与陶罐"],
];

/** 单个版本所属的主题分组名。 */
export function versionTheme(item: VanillaVersionInfo): string {
  if (item.type === "old_alpha") return "Alpha 阶段";
  if (item.type === "old_beta") return "Beta 阶段";

  if (item.type === "snapshot") {
    for (const [pattern, theme] of SNAPSHOT_THEMES) {
      if (pattern.test(item.id)) return theme;
    }
    const year = /^(\d{2})w/.exec(item.id);
    return year ? `20${year[1]} 年快照` : "其他开发版本";
  }

  for (const [pattern, theme] of RELEASE_THEMES) {
    if (pattern.test(item.id)) return theme;
  }
  const series = /^(\d+\.\d+)/.exec(item.id);
  return series ? `${series[1]} 系列` : "其他版本";
}

/**
 * 按主题分组；输入按发布时间倒序，所以分组顺序天然是「新主题在前」，
 * 组内顺序也保持倒序。
 */
export function groupVersions(items: VanillaVersionInfo[]): ThemeGroup[] {
  const groups = new Map<string, ThemeGroup>();
  for (const item of items) {
    const theme = versionTheme(item);
    const group = groups.get(theme);
    if (group) group.items.push(item);
    else groups.set(theme, { theme, items: [item] });
  }
  return [...groups.values()];
}
