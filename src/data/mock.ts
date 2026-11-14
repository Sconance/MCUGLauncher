import type {
  AvatarInfo,
  CommunityData,
  ContentItem,
  ContentUpdateInfo,
  GameInfo,
  JavaInfo,
  LauncherState,
  ModInfo,
  SaveBackupInfo,
  TaskInfo,
  VanillaVersionInfo,
} from "../types/launcher";

/* 预览用假数据：字段形状与 Contracts 一致，接入后端后整体删除。 */

function skinAvatar(): string {
  const hair = "#472f22";
  const skin = "#bb8761";
  const eyeBlue = "#385b9f";
  const mouth = "#67402e";
  const cells: string[] = [];
  for (let y = 0; y < 8; y += 1) {
    for (let x = 0; x < 8; x += 1) {
      let color = y < 2 || (y === 2 && (x === 0 || x === 7)) ? hair : skin;
      if (y === 4 && [1, 2, 5, 6].includes(x)) color = x === 2 || x === 5 ? eyeBlue : "#ffffff";
      if (y === 6 && x >= 2 && x <= 5) color = mouth;
      cells.push(`<rect x="${x}" y="${y}" width="1" height="1" fill="${color}"/>`);
    }
  }
  const svg = `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 8 8" shape-rendering="crispEdges">${cells.join("")}</svg>`;
  return `data:image/svg+xml;charset=utf-8,${encodeURIComponent(svg)}`;
}

export const defaultAvatar = skinAvatar();

const banner = (() => {
  const svg = `<svg xmlns="http://www.w3.org/2000/svg" width="640" height="180">
    <defs><linearGradient id="g" x1="0" y1="0" x2="1" y2="1">
      <stop offset="0" stop-color="#3472ad"/><stop offset="1" stop-color="#193753"/>
    </linearGradient></defs>
    <rect width="640" height="180" fill="url(#g)"/>
    <circle cx="540" cy="40" r="90" fill="#ffffff" opacity="0.08"/>
    <circle cx="80" cy="160" r="70" fill="#ffffff" opacity="0.06"/>
  </svg>`;
  return `data:image/svg+xml;charset=utf-8,${encodeURIComponent(svg)}`;
})();

export const mods: ModInfo[] = [
  { id: "sodium", name: "Sodium", file: "sodium-fabric-0.6.13.jar", version: "0.6.13", enabled: true, path: "mods/sodium-fabric-0.6.13.jar", size: 1_238_272 },
  { id: "iris", name: "Iris Shaders", file: "iris-1.8.8.jar", version: "1.8.8", enabled: true, path: "mods/iris-1.8.8.jar", size: 2_470_016 },
  { id: "fabric-api", name: "Fabric API", file: "fabric-api-0.115.0.jar", version: "0.115.0", enabled: true, path: "mods/fabric-api-0.115.0.jar", size: 2_023_424 },
  { id: "lithium", name: "Lithium", file: "lithium-fabric-0.15.3.jar", version: "0.15.3", enabled: true, path: "mods/lithium-fabric-0.15.3.jar", size: 1_048_576 },
  { id: "jei", name: "JEI", file: "jei-1.21.1.jar", version: "19.21.0", enabled: false, path: "mods/jei-1.21.1.jar", size: 1_572_864 },
  { id: "xaero", name: "Xaero's Minimap", file: "xaeros-minimap-24.2.0.jar", version: "24.2.0", enabled: true, path: "mods/xaeros-minimap-24.2.0.jar", size: 1_887_436 },
];

export const state: LauncherState = {
  appVersion: "0.22.3",
  player: "XiaoMu",
  accountType: "microsoft",
  root: "D:/Games/Minecraft/MCUG",
  gameRoots: ["D:/Games/Minecraft/MCUG", "E:/Minecraft/Backup"],
  java: "C:/Program Files/Eclipse Adoptium/jdk-21.0.4.7-hotspot/bin/javaw.exe",
  width: 1280,
  height: 720,
  ram: 8192,
  autoMemory: true,
  anonymous: false,
  hasCommunityKey: true,
  hasAdminKey: true,
  skinStation: "",
  cape: "",
  selectedVersion: "MCUG-1.21.1",
  selectedInstance: "MCUG-1.21.1",
  instances: [
    { name: "MCUG-1.21.1", loader: "Fabric 0.16.9", mods: 42, updates: 3, displayName: "MCUG 生存服", minecraft: "1.21.1", isPlaceholder: false },
    { name: "Create-1.20.1", loader: "Forge 47.2.0", mods: 18, updates: 0, displayName: "机械动力", minecraft: "1.20.1", isPlaceholder: false },
    { name: "Vanilla-1.20.6", loader: "原版", mods: 0, updates: 0, displayName: "原版 1.20.6", minecraft: "1.20.6", isPlaceholder: false },
  ],
  mods,
  servers: [
    { name: "MCUG 生存服", address: "play.mcug.cn:25565", instance: "MCUG-1.21.1" },
    { name: "创造服", address: "creative.example.com:25566", instance: "" },
  ],
  favorites: [
    { id: "sodium", kind: "mod", name: "Sodium", description: "渲染优化 · 大幅提升帧率" },
    { id: "iris", kind: "mod", name: "Iris Shaders", description: "光影加载器，兼容 OptiFine 光影包" },
    { id: "create", kind: "mod", name: "Create", description: "机械动力 · 自动化与齿轮" },
    { id: "terralith", kind: "mod", name: "Terralith", description: "地形生成扩展" },
  ],
  versionGroups: [
    { label: "1.21", count: 3, versions: [{ name: "1.21.1", meta: "正式版" }, { name: "1.21", meta: "正式版" }] },
    { label: "1.20", count: 4, versions: [{ name: "1.20.6", meta: "正式版" }, { name: "1.20.1", meta: "正式版" }] },
  ],
  dataSources: {
    announcements: "https://example.com/mcug/announcements.json",
    servers: "https://example.com/mcug/servers.json",
    recommendations: "https://example.com/mcug/recommendations.json",
    quotes: "https://example.com/mcug/quotes.json",
  },
  defaultDataSources: {
    announcements: "https://example.com/mcug/announcements.json",
    servers: "https://example.com/mcug/servers.json",
    recommendations: "https://example.com/mcug/recommendations.json",
    quotes: "https://example.com/mcug/quotes.json",
  },
  customDataSources: {
    announcements: "",
    servers: "",
    recommendations: "",
    quotes: "",
  },
  dataSourceMode: "default",
  autoChinese: true,
};

export const avatar: AvatarInfo = {
  image: defaultAvatar,
  label: "微软账号 · 在线验证",
  fallback: true,
  account: "microsoft",
  player: state.player,
};

export const community: CommunityData = {
  announcements: [
    {
      id: "a1",
      title: "0.22.3 原生界面已上线",
      body: "启动器已切换到原生 Avalonia 界面，账号、下载、实例与社区功能全部保留。遇到问题请在群里反馈。",
      tag: "公告",
      author: "MCUG 管理组",
      publishedAt: "2026-11-08T10:00:00+08:00",
      imageUrl: banner,
      markdown:
        "# 0.22.3 原生界面已上线\n\n启动器已切换到原生界面，**账号、下载、实例与社区功能全部保留**。\n\n- 微软 / MUA / 离线账号\n- 模组与整合包安装\n- 实例克隆、导出与卸载\n\n> 遇到问题请在群里反馈。",
    },
    {
      id: "a2",
      title: "周末生存服活动",
      body: "周六晚八点开启双倍经验，主城集合。",
      tag: "活动",
      author: "MCUG 管理组",
      publishedAt: "2026-11-06T18:30:00+08:00",
      imageUrl: "",
      markdown: "",
    },
  ],
  recommendations: [
    { id: "r1", type: "server", name: "MCUG 生存服", author: "群友推荐", description: "长期稳定的原版生存服，支持基岩版互通。", tags: ["生存", "1.21.1"], version: "1.21.1", size: "", url: "play.mcug.cn:25565" },
    { id: "r2", type: "server", name: "机械动力服", author: "群友推荐", description: "Create 主题服务器，适合喜欢自动化的玩家。", tags: ["Create", "1.20.1"], version: "1.20.1", size: "", url: "create.example.com:25565" },
    { id: "r3", type: "material", name: "Stay True", author: "群友推荐", description: "清爽的 1.21 材质包，保留原版风格。", tags: ["材质"], version: "1.21", size: "12 MB", url: "" },
    { id: "r4", type: "shader", name: "Complementary Reimagined", author: "群友推荐", description: "性能与画质平衡的光影包。", tags: ["光影"], version: "1.21", size: "8 MB", url: "" },
  ],
  quotes: [
    { id: "q1", type: "text", text: "来都来了，先挖三格。", author: "群友", source: "群聊", imageUrl: "" },
    { id: "q2", type: "text", text: "这箱子里的东西不是你的，是我的。", author: "服主", source: "服务器公告", imageUrl: "" },
    { id: "q3", type: "text", text: "今晚八点，主城集合。", author: "管理组", source: "活动通知", imageUrl: "" },
  ],
};

export const tasks: TaskInfo[] = [
  { id: "t1", name: "下载 Sodium 0.6.13", state: "Running", percent: 46, message: "下载中 12.4 MB / 26.9 MB", committing: false },
  { id: "t2", name: "安装 Fabric 0.16.9", state: "Queued", percent: 0, message: "等待中", committing: false },
  { id: "t3", name: "更新 Create 6.0.4", state: "Paused", percent: 72, message: "已暂停", committing: false },
];

export const games: GameInfo[] = [
  { id: "g1", version: "MCUG-1.21.1", player: "XiaoMu", pid: 24188 },
];

export const javaList: JavaInfo[] = [
  { path: "C:/Program Files/Eclipse Adoptium/jdk-21.0.4.7-hotspot/bin/javaw.exe", major: 21, arch: "x64", vendor: "Eclipse Adoptium", error: "" },
  { path: "C:/Program Files/Java/jdk-17/bin/javaw.exe", major: 17, arch: "x64", vendor: "Oracle", error: "" },
  { path: "C:/Program Files (x86)/Java/jre1.8/bin/javaw.exe", major: 8, arch: "x86", vendor: "Oracle", error: "" },
];

export const backups: SaveBackupInfo[] = [
  { id: "b1", created: "2026-11-12 21:04", worlds: ["新世界", "SkyBlock"] },
  { id: "b2", created: "2026-11-10 19:22", worlds: ["新世界"] },
];

export const contentItems: ContentItem[] = [
  { id: "sodium", name: "Sodium", description: "现代渲染引擎，显著提升帧率并减少卡顿。", author: "CaffeineMC", icon: "", downloads: "38.2M", tags: ["优化", "Fabric"] },
  { id: "iris", name: "Iris Shaders", description: "兼容 OptiFine 光影包的光影加载器。", author: "IrisTeam", icon: "", downloads: "12.7M", tags: ["光影", "Fabric"] },
  { id: "create", name: "Create", description: "以旋转动力为核心的机械自动化模组。", author: "simibubi", icon: "", downloads: "21.4M", tags: ["科技", "Forge"] },
  { id: "jei", name: "Just Enough Items", description: "查看物品配方与用途。", author: "mezz", icon: "", downloads: "45.1M", tags: ["辅助"] },
  { id: "lithium", name: "Lithium", description: "服务端与客户端通用性能优化。", author: "CaffeineMC", icon: "", downloads: "9.8M", tags: ["优化"] },
  { id: "terralith", name: "Terralith", description: "超过 100 种新生物群系的地形扩展。", author: "Starmute", icon: "", downloads: "6.3M", tags: ["地形", "数据包"] },
];

export const vanillaVersions: VanillaVersionInfo[] = [
  { id: "1.21.1", type: "release", released: "2024-08-08" },
  { id: "1.21", type: "release", released: "2024-06-13" },
  { id: "1.20.6", type: "release", released: "2024-04-29" },
  { id: "1.20.4", type: "release", released: "2023-12-07" },
  { id: "1.20.1", type: "release", released: "2023-06-12" },
  { id: "1.19.4", type: "release", released: "2023-03-14" },
  { id: "24w45a", type: "snapshot", released: "2024-11-06" },
  { id: "24w44a", type: "snapshot", released: "2024-10-30" },
  { id: "b1.7.3", type: "old_beta", released: "2011-07-08" },
  { id: "a1.2.6", type: "old_alpha", released: "2010-12-02" },
];

export const loaderVersions: Record<string, string[]> = {
  Fabric: ["0.16.9", "0.16.8", "0.16.5", "0.15.11"],
  Quilt: ["0.24.0", "0.23.2", "0.23.1"],
  Forge: ["47.2.0", "47.1.3", "47.0.19"],
  NeoForge: ["21.1.72", "21.1.66", "21.1.59"],
};

export const updates: ContentUpdateInfo[] = [
  { id: "sodium", name: "Sodium", before: "0.6.11", after: "0.6.13", available: true, error: "" },
  { id: "iris", name: "Iris Shaders", before: "1.8.5", after: "1.8.8", available: true, error: "" },
  { id: "fabric-api", name: "Fabric API", before: "0.114.0", after: "0.115.0", available: true, error: "" },
];

export const systemInfo = {
  app: "0.22.3",
  dotnet: ".NET 8.0.11",
  webview: "WebView2 130.0.2849.68",
  os: "Windows 11 23H2 (22631.4460)",
  root: state.root,
  data: "C:/Users/XiaoMu/AppData/Roaming/MCUGLauncher",
};

export const announcementImage = banner;
