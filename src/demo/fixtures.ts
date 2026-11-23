import type {
  AccountInfo,
  AnnouncementInfo,
  AvatarInfo,
  CommunityData,
  FavoriteInfo,
  GameSessionInfo,
  InstanceCatalogInfo,
  InstanceInfo,
  LauncherOverview,
  ModInfo,
  QuoteInfo,
  RecommendationInfo,
  SavedServerInfo,
  SettingsInfo,
  TaskInfo,
} from "../types/launcher";

/**
 * 演示后端的初始数据。
 *
 * 这里刻意不连真实目录、不读写磁盘：所有值都是假的，
 * 用来给没有 Rust 内核的环境（纯浏览器、或内核还没接上）展示完整界面。
 * 结构与 `src/types/generated.ts` 保持一致，改 schema 时 tsc 会提醒。
 */

export const EPOCH = "demo-epoch";

const demoInstances: InstanceInfo[] = [
  {
    id: "instance-mcug-1211",
    name: "MCUG 1.21.1",
    displayName: "MCUG · NeoForge 1.21.1",
    loader: "neoforge",
    minecraft: "1.21.1",
    mods: 12,
    updates: 0,
    isPlaceholder: false,
  },
  {
    id: "instance-vanilla-1204",
    name: "原版 1.20.4",
    displayName: "原版生存 · 1.20.4",
    loader: "vanilla",
    minecraft: "1.20.4",
    mods: 0,
    updates: 1,
    isPlaceholder: false,
  },
  {
    id: "instance-fabric-1215",
    name: "Fabric 1.21.5",
    displayName: "轻量 Fabric · 1.21.5",
    loader: "fabric",
    minecraft: "1.21.5",
    mods: 5,
    updates: 0,
    isPlaceholder: false,
  },
];

const demoMods: ModInfo[] = [
  { id: "mod-sodium", name: "Sodium", file: "sodium-0.6.9.jar", version: "0.6.9", enabled: true, path: "mods/sodium-0.6.9.jar", size: 1843200 },
  { id: "mod-iris", name: "Iris Shaders", file: "iris-1.8.1.jar", version: "1.8.1", enabled: true, path: "mods/iris-1.8.1.jar", size: 3211264 },
  { id: "mod-lithium", name: "Lithium", file: "lithium-0.15.3.jar", version: "0.15.3", enabled: false, path: "mods/lithium-0.15.3.jar", size: 786432 },
  { id: "mod-jei", name: "Just Enough Items", file: "jei-1.21.1.jar", version: "19.21.0", enabled: true, path: "mods/jei-1.21.1.jar", size: 1048576 },
];

const demoAnnouncements: AnnouncementInfo[] = [
  {
    id: "news-demo-1",
    title: "演示模式已开启",
    body: "当前界面使用本地假数据，不会读写真实游戏目录。",
    tag: "公告",
    author: "MCUG",
    publishedAt: "2026-11-20T10:00:00+08:00",
    imageUrl: "",
    markdown: "当前界面使用本地假数据，不会读写真实游戏目录。",
  },
  {
    id: "news-demo-2",
    title: "前端接口 v1 冻结",
    body: "第三方前端可以通过 MCUGL_FRONTEND 整体替换界面。",
    tag: "开发",
    author: "MCUG",
    publishedAt: "2026-11-18T10:00:00+08:00",
    imageUrl: "",
    markdown: "第三方前端可以通过 `MCUGL_FRONTEND` 整体替换界面。",
  },
];

const demoRecommendations: RecommendationInfo[] = [
  { id: "rec-demo-1", type: "整合包", name: "MCUG 轻量生存", author: "MCUG", description: "演示用整合包：Sodium + Iris，30 个模组。", tags: ["生存", "优化"], version: "1.21.1", size: "128 MB", url: "" },
  { id: "rec-demo-2", type: "模组", name: "Sodium", author: "Caffeine MC", description: "渲染优化，帧率翻倍。", tags: ["优化"], version: "1.21.1", size: "2 MB", url: "" },
  { id: "rec-demo-3", type: "服务器", name: "MCUG 生存服", author: "MCUG", description: "演示服务器地址，不会真正连接。", tags: ["生存"], version: "1.21.1", size: "", url: "play.mcug.fun:25565" },
];

const demoQuotes: QuoteInfo[] = [
  { id: "quote-demo-1", type: "语录", text: "先跑起来，再跑得好看。", author: "MCUG", source: "开发手记", imageUrl: "" },
  { id: "quote-demo-2", type: "语录", text: "演示数据也是数据。", author: "MCUG", source: "开发手记", imageUrl: "" },
];

export interface DemoState {
  overview: LauncherOverview;
  settings: SettingsInfo;
  accounts: AccountInfo[];
  catalog: InstanceCatalogInfo;
  mods: Record<string, ModInfo[]>;
  servers: SavedServerInfo[];
  bookmarks: FavoriteInfo[];
  tasks: TaskInfo[];
  sessions: GameSessionInfo[];
  community: CommunityData;
  avatar: AvatarInfo;
  sequence: number;
}

export function createDemoState(): DemoState {
  return {
    overview: {
      appVersion: "0.22.3 (demo)",
      selectedInstanceId: "instance-mcug-1211",
      activeAccountId: "account-demo",
    },
    settings: {
      root: "C:\\Users\\Demo\\AppData\\Roaming\\.minecraft",
      gameRoots: ["C:\\Users\\Demo\\AppData\\Roaming\\.minecraft"],
      java: "C:\\Program Files\\Java\\jdk-21\\bin\\javaw.exe",
      width: 1280,
      height: 800,
      ram: 4096,
      autoMemory: true,
      anonymous: true,
      hasCommunityKey: false,
      hasAdminKey: false,
      skinStation: "",
      cape: "",
      dataSources: { announcements: "官方", servers: "官方", recommendations: "官方", quotes: "官方" },
      defaultDataSources: { announcements: "官方", servers: "官方", recommendations: "官方", quotes: "官方" },
      customDataSources: { announcements: "", servers: "", recommendations: "", quotes: "" },
      dataSourceMode: "official",
      autoChinese: true,
    },
    accounts: [
      { id: "account-demo", kind: "microsoft", player: "Steve_CN", status: "ready" },
      { id: "account-offline", kind: "offline", player: "Alex_Demo", status: "ready" },
    ],
    catalog: {
      items: demoInstances,
      versionGroups: [
        {
          label: "1.21.x",
          count: 2,
          versions: [
            { name: "1.21.1", meta: "NeoForge 21.1.72" },
            { name: "1.21.5", meta: "Fabric 0.16.10" },
          ],
        },
        { label: "1.20.x", count: 1, versions: [{ name: "1.20.4", meta: "原版" }] },
      ],
    },
    mods: {
      "instance-mcug-1211": demoMods,
      "instance-vanilla-1204": [],
      "instance-fabric-1215": demoMods.slice(0, 2),
    },
    servers: [
      { id: "server-demo-1", name: "MCUG 生存服", address: "play.mcug.fun:25565", instanceId: "instance-mcug-1211" },
      { id: "server-demo-2", name: "演示小游戏", address: "minigame.example.com", instanceId: null },
    ],
    bookmarks: [
      { id: "bookmark-demo-1", kind: "mod", name: "Sodium", description: "渲染优化" },
      { id: "bookmark-demo-2", kind: "pack", name: "MCUG 轻量生存", description: "演示整合包" },
    ],
    tasks: [],
    sessions: [],
    community: {
      announcements: demoAnnouncements,
      recommendations: demoRecommendations,
      quotes: demoQuotes,
    },
    avatar: {
      image: "",
      label: "Steve_CN",
      fallback: true,
      account: "account-demo",
      player: "Steve_CN",
    },
    sequence: 0,
  };
}
