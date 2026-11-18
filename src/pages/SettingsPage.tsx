import { useEffect, useState } from "react";
import {
  getSystemInfo as getSystemInfoCommand,
  openFolder as openFolderCommand,
  readLog as readLogCommand,
  resetWindow as resetWindowCommand,
  saveCustomSources as saveCustomSourcesCommand,
  saveSettings as saveSettingsCommand,
  scanJava as scanJavaCommand,
  testDataSource as testDataSourceCommand,
  useDataSources as useDataSourcesCommand,
  verifyKey as verifyKeyCommand,
} from "../api/commands/settings";
import {
  backupSaves as backupSavesCommand,
  listSaveBackups as listSaveBackupsCommand,
  restoreSave as restoreSaveCommand,
} from "../api/commands/saves";
import {
  deleteServer as deleteServerCommand,
  pingServer as pingServerCommand,
  saveServer as saveServerCommand,
} from "../api/commands/servers";
import Field from "../components/Field";
import { AppInput, AppSelect, AppTextarea } from "../components/AppInput";
import PathPrompt from "../components/PathPrompt";
import SettingTag from "../components/SettingTag";
import SplitPage from "../components/SplitPage";
import { LIMITS } from "../constants";
import { useLauncher } from "../state/launcher";
import { LOCALE_LABELS, useI18n, type Locale } from "../i18n";
import type { DataSourceSet, JavaInfo, SaveBackupInfo, SavedServerInfo } from "../types/launcher";
import navAbout from "../assets/icons/nav/nav-about.svg";
import navAppearance from "../assets/icons/nav/nav-appearance.svg";
import navCommunity from "../assets/icons/nav/nav-community.svg";
import navDatasources from "../assets/icons/nav/nav-datasources.svg";
import navDiagnose from "../assets/icons/nav/nav-diagnose.svg";
import navJava from "../assets/icons/nav/nav-java.svg";
import navLaunch from "../assets/icons/nav/nav-launch.svg";
import navLogs from "../assets/icons/nav/nav-logs.svg";
import navRecovery from "../assets/icons/nav/nav-recovery.svg";
import navSaves from "../assets/icons/nav/nav-saves.svg";
import navServers from "../assets/icons/nav/nav-servers.svg";
import type { NavItem } from "../components/SectionNav";
import { isLiquidGlass, setLiquidGlass } from "../shell/liquid";
import "./SettingsPage.css";

const SECTIONS = [
  ["launch", "启动设置"],
  ["java", "Java 管理"],
  ["datasources", "数据源"],
  ["appearance", "外观"],
  ["servers", "服务器收藏"],
  ["saves", "存档保护"],
  ["recovery", "任务与恢复"],
  ["logs", "运行日志"],
  ["diagnose", "诊断与反馈"],
  ["community", "社区身份"],
  ["about", "关于与许可"],
] as const;

type SectionKey = (typeof SECTIONS)[number][0];

const NAV_GROUPS: NavItem[][] = [
  [
    { key: "launch", label: "启动设置", icon: navLaunch },
    { key: "java", label: "Java 管理", icon: navJava },
    { key: "datasources", label: "数据源", icon: navDatasources },
  ],
  [
    { key: "appearance", label: "外观", icon: navAppearance },
    { key: "servers", label: "服务器收藏", icon: navServers },
  ],
  [
    { key: "saves", label: "存档保护", icon: navSaves },
    { key: "recovery", label: "任务与恢复", icon: navRecovery },
    { key: "logs", label: "运行日志", icon: navLogs },
    { key: "diagnose", label: "诊断与反馈", icon: navDiagnose },
  ],
  [
    { key: "community", label: "社区身份", icon: navCommunity },
    { key: "about", label: "关于与许可", icon: navAbout },
  ],
];

const EMPTY_SOURCES: DataSourceSet = {
  announcements: "",
  servers: "",
  recommendations: "",
  quotes: "",
};

/** 设置页：左侧十一个分区，右侧表单与列表。 */
export default function SettingsPage() {
  const { overview, settings, accounts, servers, resourceVersions, refreshState } = useLauncher();
  const { locale, setLocale, t } = useI18n();
  const [section, setSection] = useState<SectionKey>("launch");
  const [statuses, setStatuses] = useState<Record<string, string>>({});
  const [hydrated, setHydrated] = useState(false);

  const [root, setRoot] = useState("");
  const [java, setJava] = useState("");
  const [javaPath, setJavaPath] = useState("");
  const [ram, setRam] = useState("4096");
  const [width, setWidth] = useState("1280");
  const [height, setHeight] = useState("720");
  const [player, setPlayer] = useState("");
  const [autoMemory, setAutoMemory] = useState(true);
  const [autoChinese, setAutoChinese] = useState(true);
  const [liquidGlass, setLiquidGlassState] = useState(isLiquidGlass);

  /** 液态玻璃：开关写入 localStorage，并同步到 body[data-liquid]，供后续样式接入 */
  const toggleLiquidGlass = (next: boolean) => {
    setLiquidGlassState(next);
    setLiquidGlass(next);
  };
  const [anonymous, setAnonymous] = useState(false);
  const [sources, setSources] = useState<DataSourceSet>(EMPTY_SOURCES);

  const [javaList, setJavaList] = useState<JavaInfo[]>([]);
  const [serverName, setServerName] = useState("");
  const [serverAddress, setServerAddress] = useState("");
  const [editingServer, setEditingServer] = useState("");
  const [pings, setPings] = useState<Record<string, string>>({});
  const [backups, setBackups] = useState<SaveBackupInfo[]>([]);
  const [log, setLog] = useState("");
  const [diagnostics, setDiagnostics] = useState("");
  const [communityKey, setCommunityKey] = useState("");
  const [adminKey, setAdminKey] = useState("");
  const [prompt, setPrompt] = useState<null | "root" | "java">(null);

  const say = (message: string) => setStatuses((previous) => ({ ...previous, [section]: message }));
  const status = statuses[section] ?? "";

  useEffect(() => {
    if (!settings || !accounts || hydrated) return;
    setRoot(settings.root);
    setJava(settings.java);
    setJavaPath(settings.java);
    setRam(String(settings.ram));
    setWidth(String(settings.width));
    setHeight(String(settings.height));
    setPlayer(accounts.find((account) => account.id === overview?.activeAccountId)?.player ?? "");
    setAutoMemory(settings.autoMemory);
    setAutoChinese(settings.autoChinese);
    setAnonymous(settings.anonymous);
    const custom = settings.customDataSources;
    const active = Object.values(custom).some((value) => value.length > 0);
    setSources(active ? custom : settings.dataSources);
    setHydrated(true);
  }, [settings, accounts, overview, hydrated]);

  const loadBackups = async () => setBackups(await listSaveBackupsCommand());
  const readLog = async () => setLog(await readLogCommand());
  const readSystemInfo = async () => {
    const info = await getSystemInfoCommand();
    setDiagnostics(
      [
        `MCUG Launcher ${info.app}`,
        `.NET ${info.dotnet}`,
        `WebView2 ${info.webview}`,
        `系统 ${info.os}`,
        `游戏目录 ${info.root}`,
        `数据目录 ${info.data}`,
      ].join("\n")
    );
  };

  useEffect(() => {
    if (section === "saves") void loadBackups();
    else if (section === "logs") void readLog();
    else if (section === "diagnose") void readSystemInfo();
    // 只在切换分区时加载
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [section]);

  if (!settings || !accounts || !servers || !overview) {
    return (
      <div className="page">
        <div className="empty">正在加载设置…</div>
      </div>
    );
  }

  const roots = Array.from(new Set([...settings.gameRoots, settings.root, root])).filter((value) => value.length > 0);
  const offline = accounts.find((account) => account.id === overview.activeAccountId)?.kind === "offline";

  const saveLaunch = async () => {
    if (ram.length === 0 || root.trim().length === 0) {
      say("请选择已有游戏目录。");
      return;
    }
    const ramValue = Number.parseInt(ram, 10);
    if (!Number.isFinite(ramValue) || ramValue < LIMITS.ramMin || ramValue > LIMITS.ramMax) {
      say(`最大内存需在 ${LIMITS.ramMin}–${LIMITS.ramMax} MB 之间。`);
      return;
    }
    const widthValue = Number.parseInt(width, 10);
    if (!Number.isFinite(widthValue) || widthValue < LIMITS.windowWidthMin || widthValue > LIMITS.windowWidthMax) {
      say(`窗口宽需在 ${LIMITS.windowWidthMin}–${LIMITS.windowWidthMax} 之间。`);
      return;
    }
    const heightValue = Number.parseInt(height, 10);
    if (!Number.isFinite(heightValue) || heightValue < LIMITS.windowHeightMin || heightValue > LIMITS.windowHeightMax) {
      say(`窗口高需在 ${LIMITS.windowHeightMin}–${LIMITS.windowHeightMax} 之间。`);
      return;
    }
    if (offline && player.trim().length === 0) {
      say("离线账号需要填写玩家名。");
      return;
    }
    say("正在保存…");
    const result = await saveSettingsCommand(
      {
        root: root.trim(),
        java: java.trim(),
        ram: ramValue,
        width: widthValue,
        height: heightValue,
        autoMemory,
        autoChinese,
        ...(offline ? { player: player.trim() } : {}),
      },
      resourceVersions.settings?.revision ?? 0
    );
    await refreshState();
    say(result.message);
  };

  const switchRoot = async (value: string) => {
    setRoot(value);
    say("正在切换游戏目录…");
    const result = await saveSettingsCommand({ root: value }, resourceVersions.settings?.revision ?? 0);
    await refreshState();
    say(result.message);
  };

  const saveJava = async () => {
    say("正在保存 Java 设置…");
    const result = await saveSettingsCommand({ java: javaPath.trim() }, resourceVersions.settings?.revision ?? 0);
    setJava(javaPath.trim());
    await refreshState();
    say(result.message);
  };

  const scanJava = async () => {
    say("正在扫描本机 Java…");
    const items = await scanJavaCommand();
    setJavaList(items);
    say(items.length === 0 ? "没有找到可用的 Java。" : `找到 ${items.length} 个 Java。`);
  };

  const testSource = async (label: string, url: string) => {
    if (url.trim().length === 0) {
      say(`${label}地址为空。`);
      return;
    }
    say(`正在测试${label}…`);
    const result = await testDataSourceCommand(label, url.trim());
    say(`${result.name}：${result.ok ? "可用" : "失败"} · ${result.message} · ${result.ms} ms`);
  };

  const saveSources = async (use: boolean) => {
    await saveCustomSourcesCommand(sources);
    if (use) await useDataSourcesCommand("custom");
    await refreshState();
    say(use ? "已保存并切换到自定义数据源。" : "已保存自定义数据源，可点击「使用自定义」切换。");
  };

  const saveServer = async () => {
    if (serverAddress.trim().length === 0) {
      say("请填写服务器地址。");
      return;
    }
    say("正在保存…");
    const result = await saveServerCommand(serverName.trim(), serverAddress.trim(), editingServer);
    await refreshState();
    say(result.message);
    setEditingServer("");
    setServerName("");
    setServerAddress("");
  };

  const beginEdit = (server: SavedServerInfo) => {
    setEditingServer(server.id);
    setServerName(server.name);
    setServerAddress(server.address);
    say(`正在编辑 ${server.address}`);
  };

  const pingServer = async (address: string) => {
    say(`正在查询 ${address}…`);
    const info = await pingServerCommand(address);
    setPings((previous) => ({
      ...previous,
      [address]: info.online
        ? `${info.players}/${info.max} 人 · ${info.ping} ms · ${info.motd}`
        : `离线 · ${info.error}`,
    }));
  };

  const deleteServer = async (id: string, address: string) => {
    const result = await deleteServerCommand(id);
    setPings((previous) => {
      const next = { ...previous };
      delete next[address];
      return next;
    });
    await refreshState();
    say(result.message);
  };

  const backupSaves = async () => {
    say("正在备份存档…");
    const result = await backupSavesCommand();
    await loadBackups();
    say(result.message);
  };

  const restoreSave = async (id: string, world: string) => {
    say("正在恢复存档…");
    const result = await restoreSaveCommand(id, world);
    say(result.message);
  };

  const verifyKey = async (role: string, key: string, clear?: () => void) => {
    if (key.trim().length === 0) {
      say("请输入密钥。");
      return;
    }
    say("正在验证…");
    const result = await verifyKeyCommand(role, key.trim());
    say(result.message);
    if (result.ok && clear) clear();
  };

  const copyDiagnostics = async () => {
    if (diagnostics.length === 0) {
      say("请先读取系统信息。");
      return;
    }
    try {
      await navigator.clipboard.writeText(diagnostics);
      say("诊断信息已复制。");
    } catch {
      say("剪贴板不可用。");
    }
  };

  return (
    <>
      <SplitPage
        title="设置"
        navLabel="设置分区"
        groups={NAV_GROUPS}
        active={section}
        onSelect={(key) => setSection(key as SectionKey)}
        contentClassName="settings-scroll"
      >
        {(shown) => (
          <>
            {shown === "launch" ? (
              <section className="card stack" style={{ gap: 10 }}>
                <h2 className="h2">启动设置</h2>
                <p className="muted">修改后写入本机配置，下次启动生效。</p>
                <Field label="游戏目录">
                  <div className="row" style={{ gap: 8 }}>
                    <AppInput placeholder="游戏根目录"
                      value={root}
                      onChange={(event) => setRoot(event.target.value)}
                    />
                    <button type="button" className="btn ghost" onClick={() => setPrompt("root")}>
                      选择
                    </button>
                  </div>
                </Field>
                <Field label="已保存游戏目录">
                  <div className="row" style={{ gap: 8 }}>
                    <AppSelect value={root} onChange={(event) => void switchRoot(event.target.value)}>
                      {roots.map((value) => (
                        <option key={value} value={value}>
                          {value}
                        </option>
                      ))}
                    </AppSelect>
                    <button type="button" className="btn ghost" onClick={() => setPrompt("root")}>
                      添加并切换
                    </button>
                  </div>
                </Field>
                <Field label="Java 路径（留空自动选择）">
                  <div className="row" style={{ gap: 8 }}>
                    <AppInput placeholder="留空时由启动核心选择"
                      value={java}
                      onChange={(event) => setJava(event.target.value)}
                    />
                    <button type="button" className="btn ghost" onClick={() => setPrompt("java")}>
                      选择
                    </button>
                    <button type="button" className="btn ghost" onClick={() => void scanJava()}>
                      扫描
                    </button>
                  </div>
                </Field>
                <div className="launch-numbers">
                  <label className="checkbox">
                    <input
                      type="checkbox"
                      checked={autoMemory}
                      onChange={(event) => setAutoMemory(event.target.checked)}
                    />
                    自动分配内存
                  </label>
                  <Field label="最大内存 MB">
                    <AppInput value={ram} onChange={(event) => setRam(event.target.value)} />
                  </Field>
                  <Field label="游戏窗口宽">
                    <AppInput value={width} onChange={(event) => setWidth(event.target.value)} />
                  </Field>
                  <Field label="游戏窗口高">
                    <AppInput value={height} onChange={(event) => setHeight(event.target.value)} />
                  </Field>
                </div>
                <label className="checkbox">
                  <input
                    type="checkbox"
                    checked={autoChinese}
                    onChange={(event) => setAutoChinese(event.target.checked)}
                  />
                  启动时自动设置简体中文（仅缺少语言选项时）
                </label>
                <Field label={offline ? "离线玩家名" : "玩家名（在线账号不可修改）"}>
                  <AppInput placeholder={offline ? "仅离线账号可修改" : ""}
                    value={player}
                    disabled={!offline}
                    onChange={(event) => setPlayer(event.target.value)}
                  />
                </Field>
                <div className="row" style={{ gap: 10 }}>
                  <button type="button" className="btn primary" onClick={() => void saveLaunch()}>
                    保存设置
                  </button>
                  <span className="status">{status}</span>
                </div>
              </section>
            ) : null}

            {shown === "java" ? (
              <section className="card stack" style={{ gap: 10 }}>
                <h2 className="h2">Java 管理</h2>
                <p className="muted">扫描本机常见位置中的 java.exe；指定后优先使用该路径。</p>
                <Field label="当前 Java 路径">
                  <div className="row" style={{ gap: 8 }}>
                    <AppInput placeholder="留空自动选择"
                      value={javaPath}
                      onChange={(event) => setJavaPath(event.target.value)}
                    />
                    <button type="button" className="btn ghost" onClick={() => setPrompt("java")}>
                      选择
                    </button>
                    <button type="button" className="btn ghost" onClick={() => void scanJava()}>
                      扫描本机 Java
                    </button>
                  </div>
                </Field>
                <div className="row" style={{ gap: 10 }}>
                  <button type="button" className="btn primary" onClick={() => void saveJava()}>
                    保存 Java 设置
                  </button>
                  <span className="status">{status}</span>
                </div>
                <span className="field-label">扫描结果</span>
                <div className="stack" style={{ gap: 6 }}>
                  {javaList.length === 0 ? (
                    <p className="muted">还没有扫描结果。点击「扫描本机 Java」。</p>
                  ) : (
                    javaList.map((item) => (
                      <div className="row" style={{ gap: 12 }} key={item.path}>
                        <div className="stack" style={{ gap: 2 }}>
                          <span>{`Java ${item.major} · ${item.arch} ${item.vendor}`.trim()}</span>
                          <span className="muted">{item.path}</span>
                          {item.error.length > 0 ? <span className="badge red">{item.error}</span> : null}
                        </div>
                        <span className="spacer" />
                        <button
                          type="button"
                          className="btn ghost"
                          onClick={() => {
                            setJavaPath(item.path);
                            setJava(item.path);
                            say("已填入 Java 路径，保存后生效。");
                          }}
                        >
                          使用
                        </button>
                      </div>
                    ))
                  )}
                </div>
              </section>
            ) : null}

            {shown === "datasources" ? (
              <section className="card stack" style={{ gap: 10 }}>
                <h2 className="h2">数据源</h2>
                <p className="muted">公告、服务器、推荐、语录分别读取独立 JSON；保存自定义地址后可切换默认与自定义。</p>
                {(
                  [
                    ["announcements", "公告地址"],
                    ["servers", "服务器地址"],
                    ["recommendations", "推荐地址"],
                    ["quotes", "语录地址"],
                  ] as const
                ).map(([key, label]) => (
                  <Field key={key} label={label}>
                    <div className="row" style={{ gap: 8 }}>
                      <AppInput value={sources[key]}
                        onChange={(event) => setSources({ ...sources, [key]: event.target.value })}
                      />
                      <button
                        type="button"
                        className="btn ghost"
                        onClick={() => void testSource(label.replace("地址", ""), sources[key])}
                      >
                        测试
                      </button>
                    </div>
                  </Field>
                ))}
                <div className="row" style={{ gap: 8 }}>
                  <button type="button" className="btn primary" onClick={() => void saveSources(false)}>
                    保存自定义
                  </button>
                  <button type="button" className="btn ghost" onClick={() => void saveSources(true)}>
                    使用自定义
                  </button>
                  <button
                    type="button"
                    className="btn ghost"
                    onClick={() =>
                      void useDataSourcesCommand("default").then(async (result) => {
                        await refreshState();
                        say(result.message);
                      })
                    }
                  >
                    使用默认
                  </button>
                </div>
                <p className="status">
                  {status || `当前使用${settings.dataSourceMode === "custom" ? "自定义" : "默认"}数据源。`}
                </p>
              </section>
            ) : null}

            {shown === "appearance" ? (
              <section className="card stack" style={{ gap: 10 }}>
                <h2 className="h2">外观</h2>
                <p className="muted">固定蓝白主题；可恢复默认窗口尺寸。</p>

                <div className="setting-row">
                  <div className="stack" style={{ gap: 2 }}>
                    <span className="setting-title">{t("settings.language")}</span>
                    <span className="muted">{t("settings.languageHint")}</span>
                  </div>
                  <AppSelect
                    style={{ width: 160 }}
                    value={locale}
                    onChange={(event) => setLocale(event.target.value as Locale)}
                  >
                    {(Object.keys(LOCALE_LABELS) as Locale[]).map((code) => (
                      <option key={code} value={code}>
                        {LOCALE_LABELS[code]}
                      </option>
                    ))}
                  </AppSelect>
                </div>

                <div className="setting-row">
                  <div className="stack" style={{ gap: 2 }}>
                    <span className="setting-title">
                      液态玻璃效果
                      <SettingTag kind="dev" />
                    </span>
                    <span className="muted">胶囊栏与内容栏改用折射玻璃；关闭时保持当前的磨砂玻璃。</span>
                    <span className="muted">手势与折射都走 GPU，低配设备按住导航按钮时可能出现掉帧。</span>
                  </div>
                  <button
                    type="button"
                    role="switch"
                    aria-checked={liquidGlass}
                    aria-label="液态玻璃效果"
                    className={`switch${liquidGlass ? " on" : ""}`}
                    onClick={() => toggleLiquidGlass(!liquidGlass)}
                  >
                    <span className="switch-thumb" />
                  </button>
                </div>

                <div className="theme-preview">
                  <span className="theme-name">蓝白 · 默认</span>
                  <span className="muted">#326EA7 强调色 / #F3F7F6 页面底色</span>
                </div>
                <button
                  type="button"
                  className="btn ghost"
                  style={{ alignSelf: "flex-start" }}
                  onClick={() => void resetWindowCommand().then((result) => say(result.message))}
                >
                  恢复默认窗口 1200 × 780
                </button>
              </section>
            ) : null}

            {shown === "servers" ? (
              <section className="card stack" style={{ gap: 10 }}>
                <h2 className="h2">服务器收藏</h2>
                <p className="muted">收藏的服务器会出现在主页；状态由服务器列表协议实时查询。</p>
                <div className="row" style={{ gap: 8 }}>
                  <AppInput placeholder="名称（可留空）"
                    value={serverName}
                    onChange={(event) => setServerName(event.target.value)}
                  />
                  <AppInput placeholder="地址，例如 play.example.com"
                    value={serverAddress}
                    onChange={(event) => setServerAddress(event.target.value)}
                  />
                  <button type="button" className="btn primary" onClick={() => void saveServer()}>
                    {editingServer.length > 0 ? "更新" : "添加"}
                  </button>
                </div>
                <div className="row" style={{ gap: 8 }}>
                  {editingServer.length > 0 ? (
                    <button
                      type="button"
                      className="btn ghost"
                      onClick={() => {
                        setEditingServer("");
                        setServerName("");
                        setServerAddress("");
                        say("");
                      }}
                    >
                      取消编辑
                    </button>
                  ) : null}
                  <span className="status">{status}</span>
                </div>
                <div className="stack" style={{ gap: 8 }}>
                  {servers.length === 0 ? (
                    <p className="muted">还没有收藏服务器。</p>
                  ) : (
                    servers.map((server) => (
                      <div
                        className="row"
                        style={{ gap: 12, padding: "6px 0", borderBottom: "1px solid #e3eaf0" }}
                        key={server.address}
                      >
                        <div className="stack" style={{ gap: 2 }}>
                          <span>{server.name.length > 0 ? server.name : server.address}</span>
                          <span className="muted">{server.address}</span>
                          <span className="muted">{pings[server.address] ?? "尚未查询"}</span>
                        </div>
                        <span className="spacer" />
                        <button type="button" className="btn ghost" onClick={() => void pingServer(server.address)}>
                          查询
                        </button>
                        <button type="button" className="btn ghost" onClick={() => beginEdit(server)}>
                          编辑
                        </button>
                        <button
                          type="button"
                          className="btn ghost"
                          onClick={() => void deleteServer(server.id, server.address)}
                        >
                          删除
                        </button>
                      </div>
                    ))
                  )}
                </div>
              </section>
            ) : null}

            {shown === "saves" ? (
              <section className="card stack" style={{ gap: 10 }}>
                <h2 className="h2">存档保护</h2>
                <p className="muted">备份写到本地数据目录的 backups 文件夹，不修改原存档；恢复会创建新存档。</p>
                <div className="row" style={{ gap: 8 }}>
                  <button type="button" className="btn primary" onClick={() => void backupSaves()}>
                    立即备份
                  </button>
                  <button
                    type="button"
                    className="btn ghost"
                    onClick={() => void openFolderCommand("saves").then((result) => say(result.message))}
                  >
                    打开存档文件夹
                  </button>
                  <span className="status">{status}</span>
                </div>
                <div className="stack" style={{ gap: 8 }}>
                  {backups.length === 0 ? (
                    <p className="muted">还没有存档备份。</p>
                  ) : (
                    backups.map((backup) => (
                      <div className="stack" style={{ gap: 4 }} key={backup.id}>
                        <span>{`${backup.created} · ${backup.worlds.length} 个存档`}</span>
                        {backup.worlds.map((world) => (
                          <button
                            key={world}
                            type="button"
                            className="btn ghost"
                            style={{ alignSelf: "flex-start" }}
                            onClick={() => void restoreSave(backup.id, world)}
                          >
                            恢复为 {world}
                          </button>
                        ))}
                      </div>
                    ))
                  )}
                </div>
              </section>
            ) : null}

            {shown === "recovery" ? (
              <section className="card stack" style={{ gap: 10 }}>
                <h2 className="h2">任务与恢复</h2>
                <p className="muted">窗口尺寸异常时可恢复；打开配置目录查看账号与队列数据。</p>
                <div className="row" style={{ gap: 8 }}>
                  <button
                    type="button"
                    className="btn ghost"
                    onClick={() => void resetWindowCommand().then((result) => say(result.message))}
                  >
                    恢复窗口尺寸
                  </button>
                  <button
                    type="button"
                    className="btn ghost"
                    onClick={() => void openFolderCommand("config").then((result) => say(result.message))}
                  >
                    打开配置目录
                  </button>
                  <button
                    type="button"
                    className="btn ghost"
                    onClick={() => void openFolderCommand("root").then((result) => say(result.message))}
                  >
                    打开游戏目录
                  </button>
                </div>
                <p className="status">{status}</p>
              </section>
            ) : null}

            {shown === "logs" ? (
              <section className="card stack" style={{ gap: 10 }}>
                <h2 className="h2">运行日志</h2>
                <p className="muted">最近 400 行启动与启动游戏记录。</p>
                <div className="row" style={{ gap: 8 }}>
                  <button type="button" className="btn primary" onClick={() => void readLog()}>
                    刷新日志
                  </button>
                  <button
                    type="button"
                    className="btn ghost"
                    onClick={() => void openFolderCommand("config").then((result) => say(result.message))}
                  >
                    打开日志目录
                  </button>
                  <span className="status">{status}</span>
                </div>
                <AppTextarea className="log-box" readOnly value={log} />
              </section>
            ) : null}

            {shown === "diagnose" ? (
              <section className="card stack" style={{ gap: 10 }}>
                <h2 className="h2">诊断与反馈</h2>
                <p className="muted">反馈问题时请附上运行环境信息。</p>
                <div className="row" style={{ gap: 8 }}>
                  <button type="button" className="btn primary" onClick={() => void readSystemInfo()}>
                    读取系统信息
                  </button>
                  <button type="button" className="btn ghost" onClick={() => void copyDiagnostics()}>
                    复制
                  </button>
                  <span className="status">{status}</span>
                </div>
                <AppTextarea className="log-box" readOnly value={diagnostics} />
              </section>
            ) : null}

            {shown === "community" ? (
              <section className="card stack" style={{ gap: 10 }}>
                <h2 className="h2">社区身份</h2>
                <p className="muted">匿名模式影响主页身份展示；密钥只保存在本机。</p>
                <label className="checkbox">
                  <input type="checkbox" checked={anonymous} onChange={(event) => setAnonymous(event.target.checked)} />
                  匿名模式
                </label>
                <button
                  type="button"
                  className="btn ghost"
                  style={{ alignSelf: "flex-start" }}
                  onClick={() =>
                    void saveSettingsCommand({ anonymous }, resourceVersions.settings?.revision ?? 0).then(
                      async (result) => {
                        await refreshState();
                        say(result.message);
                      }
                    )
                  }
                >
                  保存匿名设置
                </button>
                <Field label="社区身份密钥">
                  <div className="row" style={{ gap: 8 }}>
                    <AppInput type="password"
                      placeholder="向群管理员获取"
                      value={communityKey}
                      onChange={(event) => setCommunityKey(event.target.value)}
                    />
                    <button
                      type="button"
                      className="btn ghost"
                      onClick={() => void verifyKey("community", communityKey, () => setCommunityKey(""))}
                    >
                      验证
                    </button>
                  </div>
                </Field>
                <Field label="管理员密钥">
                  <div className="row" style={{ gap: 8 }}>
                    <AppInput type="password"
                      placeholder="首次输入即设置本机管理员密钥"
                      value={adminKey}
                      onChange={(event) => setAdminKey(event.target.value)}
                    />
                    <button type="button" className="btn ghost" onClick={() => void verifyKey("admin", adminKey)}>
                      验证
                    </button>
                  </div>
                </Field>
                <p className="status">
                  {status ||
                    `身份密钥：${settings.hasCommunityKey ? "已设置" : "未设置"} · 管理员密钥：${
                      settings.hasAdminKey ? "已设置" : "未设置"
                    }`}
                </p>
              </section>
            ) : null}

            {shown === "about" ? (
              <section className="card stack" style={{ gap: 10 }}>
                <h2 className="h2">关于与许可</h2>
                <span className="about-version">MCUG Launcher {overview.appVersion}</span>
                <p className="muted">面向 Windows 的 Minecraft Java 版启动器。</p>
                <p className="muted">社区数据仓库：https://github.com/waibibabu1239/MCUGL-storage</p>
                <p className="muted">界面：Avalonia 原生控件；微软登录仍使用 WebView2 完成 OAuth。</p>
                <p className="muted">许可：沿用 MCUG Launcher 原仓库策略。</p>
              </section>
            ) : null}
          </>
        )}
      </SplitPage>

      {prompt === "root" ? (
        <PathPrompt
          title="选择游戏目录"
          label="游戏根目录"
          initial={root}
          hint="目录必须已存在。"
          onCancel={() => setPrompt(null)}
          onConfirm={(path) => {
            setPrompt(null);
            void switchRoot(path);
          }}
        />
      ) : null}

      {prompt === "java" ? (
        <PathPrompt
          title="选择 Java"
          label="javaw.exe 路径"
          initial={javaPath}
          hint="留空表示自动选择。"
          onCancel={() => setPrompt(null)}
          onConfirm={(path) => {
            setPrompt(null);
            setJavaPath(path);
            setJava(path);
            say("已选择 Java 路径，保存后生效。");
          }}
        />
      ) : null}
    </>
  );
}
