import { useCallback, useEffect, useLayoutEffect, useRef, useState } from "react";
import {
  applyUninstall as applyUninstallCommand,
  applyUpdates as applyUpdatesCommand,
  checkUpdates as checkUpdatesCommand,
  cloneInstance as cloneInstanceCommand,
  deleteMod as deleteModCommand,
  exportInstance as exportInstanceCommand,
  previewUninstall as previewUninstallCommand,
  previewUpdates as previewUpdatesCommand,
  readLaunchOptions as readLaunchOptionsCommand,
  saveLaunchOptions as saveLaunchOptionsCommand,
  toggleMod as toggleModCommand,
} from "../api/commands/instances";
import { launchGame } from "../api/commands/launch";
import { openFolder as openFolderCommand } from "../api/commands/settings";
import { selectInstance as selectInstanceCommand } from "../api/commands/state";
import Field from "../components/Field";
import { AppInput, AppSelect, AppTextarea } from "../components/AppInput";
import PathPrompt from "../components/PathPrompt";
import { LIMITS } from "../constants";
import { useFadeSwap } from "../shell/fade";
import { useLauncher } from "../state/launcher";
import type {
  ContentUpdatePreviewInfo,
  ContentUpdatesInfo,
  InstanceInfo,
  LaunchOptionsInfo,
  UninstallPreviewInfo,
} from "../types/launcher";
import "./InstancesPage.css";

interface LaunchForm {
  displayName: string;
  java: string;
  autoMemory: boolean;
  ram: string;
  width: string;
  height: string;
  fullScreen: boolean;
  autoChinese: boolean;
  windowTitle: string;
  server: string;
  jvmArguments: string;
}

const EMPTY_FORM: LaunchForm = {
  displayName: "",
  java: "",
  autoMemory: true,
  ram: "4096",
  width: "1280",
  height: "720",
  fullScreen: false,
  autoChinese: true,
  windowTitle: "",
  server: "",
  jvmArguments: "",
};

const TABS: (readonly [key: "content" | "launch" | "maintenance", label: string])[] = [
  ["content", "内容"],
  ["launch", "启动设置"],
  ["maintenance", "维护"],
];

function validInstances(items: InstanceInfo[]): InstanceInfo[] {
  return items.filter((item) => item.name.length > 0 && !item.isPlaceholder);
}

function formatSize(bytes: number): string {
  if (bytes <= 0) return "—";
  return bytes < 1024 * 1024 ? `${(bytes / 1024).toFixed(1)} KB` : `${(bytes / 1024 / 1024).toFixed(1)} MB`;
}

interface InstancesPageProps {
  /** 外部控制选中的实例；不传则用页面自己的选择状态（分区页面里这样用） */
  selectedId?: string;
  /** 选中实例变化时回调，供外层同步（例如实例管理器左栏的固定标签） */
  onSelectInstance?: (id: string) => void;
}

/** 实例页：内容（模组）、启动设置与维护（克隆/导出/卸载/更新）。 */
export default function InstancesPage({ selectedId, onSelectInstance }: InstancesPageProps) {
  const { overview, accounts, instances: instanceCatalog, mods, refreshState } = useLauncher();
  const [internalSelected, setInternalSelected] = useState("");
  const selected = selectedId !== undefined ? selectedId : internalSelected;
  /** 被嵌入左右栏页面（实例管理器）时隐藏实例选择行，标签改为胶囊样式 */
  const embedded = selectedId !== undefined;
  const [tab, setTab] = useState<"content" | "launch" | "maintenance">("content");
  const tabRef = useRef<HTMLDivElement>(null);
  const [indicator, setIndicator] = useState({ left: 0, width: 0 });
  /** 标签内容跟随 fade 后的 shownTab，切换才有淡出淡入 */
  const { shown: shownTab, out: tabOut } = useFadeSwap(tab);

  useLayoutEffect(() => {
    const active = tabRef.current?.querySelector<HTMLButtonElement>(`button[data-tab="${tab}"]`);
    if (!active) return;
    setIndicator({ left: active.offsetLeft, width: active.offsetWidth });
  }, [tab]);
  const [status, setStatus] = useState("");
  const [options, setOptions] = useState<LaunchOptionsInfo | null>(null);
  const [optionsStatus, setOptionsStatus] = useState("");
  const [form, setForm] = useState<LaunchForm>(EMPTY_FORM);
  const [inherit, setInherit] = useState(false);
  const [uninstall, setUninstall] = useState<UninstallPreviewInfo | null>(null);
  const [confirmed, setConfirmed] = useState(false);
  const [updates, setUpdates] = useState<ContentUpdatesInfo | null>(null);
  const [selectedUpdates, setSelectedUpdates] = useState<string[]>([]);
  const [updatePreview, setUpdatePreview] = useState<ContentUpdatePreviewInfo | null>(null);
  const [cloneName, setCloneName] = useState("");
  const [cloneSaves, setCloneSaves] = useState(false);
  const [cloneServers, setCloneServers] = useState(false);
  const [exportSaves, setExportSaves] = useState(false);
  const [exportServers, setExportServers] = useState(false);
  const [exportPrompt, setExportPrompt] = useState(false);
  const [javaPrompt, setJavaPrompt] = useState(false);

  useEffect(() => {
    if (!instanceCatalog || !overview) return;
    const items = validInstances(instanceCatalog.items);
    if (selected.length === 0 || !items.some((item) => item.id === selected)) {
      setInternalSelected(overview.selectedInstanceId ?? items[0]?.id ?? "");
    }
  }, [instanceCatalog, overview, selected]);

  const loadOptions = useCallback(async (instanceId: string) => {
    if (instanceId.length === 0) return;
    setOptionsStatus("正在读取启动设置…");
    const info = await readLaunchOptionsCommand(instanceId);
    setOptions(info);
    setInherit(info.inherited);
    setForm({
      displayName: info.displayName.length > 0 ? info.displayName : instanceId,
      java: info.settings.java,
      autoMemory: info.settings.autoMemory,
      ram: String(info.settings.ramMb),
      width: String(info.settings.width),
      height: String(info.settings.height),
      fullScreen: info.settings.fullScreen,
      autoChinese: info.settings.autoChinese,
      windowTitle: info.settings.windowTitle,
      server: info.settings.server,
      jvmArguments: info.settings.jvmArguments,
    });
    setOptionsStatus("");
  }, []);

  useEffect(() => {
    if (selected.length === 0) return;
    void loadOptions(selected);
  }, [selected, loadOptions]);

  const refresh = async () => {
    await refreshState();
    setStatus("状态已刷新。");
  };

  const changeInstance = async (instanceId: string) => {
    setInternalSelected(instanceId);
    onSelectInstance?.(instanceId);
    setUninstall(null);
    setUpdates(null);
    setUpdatePreview(null);
    setConfirmed(false);
    setStatus("正在切换实例…");
    const result = await selectInstanceCommand(instanceId);
    await refreshState();
    setStatus(result.message);
  };

  const launch = async () => {
    if (selected.length === 0) return;
    setStatus("正在准备启动…");
    const result = await launchGame(selected, overview?.activeAccountId ?? accounts?.[0]?.id ?? "");
    setStatus(`启动请求已接受：${result.sessionId}`);
  };

  const toggleMod = async (modId: string, enabled: boolean) => {
    const result = await toggleModCommand(selected, modId, enabled);
    await refreshState();
    setStatus(result.message);
  };

  const deleteMod = async (modId: string, name: string) => {
    const result = await deleteModCommand(selected, modId);
    await refreshState();
    setStatus(`${result.message} ${name}`);
  };

  const saveOptions = async () => {
    if (!options) {
      setOptionsStatus("请先重新读取此实例的启动设置。");
      return;
    }
    const name = form.displayName.trim();
    if (name.length === 0 || name.length > LIMITS.displayNameMax) {
      setOptionsStatus(`实例显示名需为 1–${LIMITS.displayNameMax} 个字符。`);
      return;
    }
    const ram = Number.parseInt(form.ram, 10);
    if (!Number.isFinite(ram) || ram < LIMITS.ramMin || ram > LIMITS.ramMax) {
      setOptionsStatus(`内存需在 ${LIMITS.ramMin}–${LIMITS.ramMax} MB 之间。`);
      return;
    }
    const width = Number.parseInt(form.width, 10);
    if (!Number.isFinite(width) || width < LIMITS.windowWidthMin || width > LIMITS.windowWidthMax) {
      setOptionsStatus(`窗口宽需在 ${LIMITS.windowWidthMin}–${LIMITS.windowWidthMax} 之间。`);
      return;
    }
    const height = Number.parseInt(form.height, 10);
    if (!Number.isFinite(height) || height < LIMITS.windowHeightMin || height > LIMITS.windowHeightMax) {
      setOptionsStatus(`窗口高需在 ${LIMITS.windowHeightMin}–${LIMITS.windowHeightMax} 之间。`);
      return;
    }
    setOptionsStatus("正在保存…");
    const result = await saveLaunchOptionsCommand(
      options.token,
      selected,
      name,
      {
        autoMemory: form.autoMemory,
        fullScreen: form.fullScreen,
        ramMb: ram,
        width,
        height,
        java: form.java.trim(),
        jvmArguments: form.jvmArguments,
        windowTitle: form.windowTitle.trim(),
        server: form.server.trim(),
        autoChinese: form.autoChinese,
      },
      inherit,
    );
    await refreshState();
    setOptionsStatus(result.message);
  };

  const clone = async () => {
    const name = cloneName.trim();
    if (name.length === 0) {
      setStatus("请填写新实例名称。");
      return;
    }
    await cloneInstanceCommand(selected, name);
    setStatus("克隆请求已提交，请查看任务列表。");
  };

  const previewUninstall = async () => {
    const preview = await previewUninstallCommand(selected);
    setUninstall(preview);
    setConfirmed(false);
  };

  const applyUninstall = async () => {
    if (!uninstall) return;
    const planId = uninstall.planId;
    setUninstall(null);
    setConfirmed(false);
    await applyUninstallCommand(planId);
    await refreshState();
    setStatus("卸载请求已提交，请在系统确认框核对。");
  };

  const checkUpdates = async () => {
    setUpdates(null);
    setUpdatePreview(null);
    setStatus("正在检查内容更新…");
    const info = await checkUpdatesCommand(selected);
    setUpdates(info);
    setSelectedUpdates(info.items.filter((item) => item.available).map((item) => item.id));
  };

  const previewUpdates = async () => {
    if (!updates) return;
    if (selectedUpdates.length === 0) {
      setStatus("请先选择更新项目。");
      return;
    }
    setUpdatePreview(null);
    setStatus("正在解析更新及必需依赖…");
    const preview = await previewUpdatesCommand(updates.checkId);
    setUpdatePreview(preview);
  };

  const applyUpdates = async () => {
    if (!updatePreview) return;
    const planId = updatePreview.planId;
    setUpdatePreview(null);
    await applyUpdatesCommand(planId);
    await refreshState();
    setStatus("更新任务已加入队列。");
  };

  const instances = instanceCatalog ? validInstances(instanceCatalog.items) : [];
  const current = instances.find((item) => item.id === selected);
  const inheritHint = inherit
    ? "当前继承全局启动设置；取消勾选后可保存独立设置。"
    : "当前使用独立设置；勾选“继承”并保存会恢复全局设置。";
  const disabled = inherit;

  return (
    <div className="subpage">
      {embedded ? null : (
      <div className="stack" style={{ gap: 8 }}>
        <h1 className="page-title">实例</h1>
        <div className="row" style={{ gap: 8 }}>
          <AppSelect value={selected}
            style={{ flex: 1 }}
            onChange={(event) => void changeInstance(event.target.value)}
          >
            {instances.length === 0 ? <option value="">尚未安装实例</option> : null}
            {instances.map((item) => (
              <option key={item.id} value={item.id}>
                {item.displayName}
                {item.minecraft.length > 0 ? ` · ${item.minecraft}` : ""} · {item.loader}
              </option>
            ))}
          </AppSelect>
          <button type="button" className="btn primary" disabled={!current} onClick={() => void launch()}>
            启动
          </button>
          <button
            type="button"
            className="btn ghost"
            disabled={!current}
            onClick={() => void openFolderCommand("version").then((result) => setStatus(result.message))}
          >
            版本文件夹
          </button>
          <button type="button" className="btn ghost" style={{ marginRight: 20 }} onClick={() => void refresh()}>
            刷新
          </button>
        </div>
        <p className="muted">
          {instances.length === 0
            ? "尚未找到已安装实例。请先在下载页安装或导入整合包。"
            : `共 ${instances.length} 个实例 · 当前 ${current?.displayName ?? selected} · ${current?.minecraft ?? ""} · ${current?.loader ?? ""}`}
        </p>
      </div>
      )}

      {embedded ? (
        <div className="tab-pill" ref={tabRef}>
          <span
            className="tab-indicator"
            style={{ transform: `translateX(${indicator.left}px)`, width: indicator.width }}
            aria-hidden="true"
          />
          {TABS.map(([key, label]) => (
            <button
              key={key}
              type="button"
              data-tab={key}
              className={`tab-pill-item${tab === key ? " active" : ""}`}
              onClick={() => setTab(key)}
            >
              {label}
            </button>
          ))}
        </div>
      ) : (
        <div className="tabs" style={{ marginTop: 12 }}>
          {TABS.map(([key, label]) => (
            <button
              key={key}
              type="button"
              className={`tab ${tab === key ? "active" : ""}`}
              onClick={() => setTab(key)}
            >
              {label}
            </button>
          ))}
        </div>
      )}

      <div className={`instance-tab-body${tabOut ? " is-fading" : ""}`}>
        {shownTab === "content" ? (
          <section className="card instance-card">
            <div className="instance-scroll">
              <p className="muted">
                {current
                  ? `${current.displayName} · ${current.loader} · ${current.minecraft} · ${current.mods} 个模组，${current.updates} 个可更新`
                  : "请选择实例。"}
              </p>
              <div className="mod-list">
                {mods?.length ? (
                  mods.map((mod) => (
                    <div className="mod-row" key={mod.id}>
                      <div className="stack" style={{ gap: 2 }}>
                        <label className="checkbox">
                          <input
                            type="checkbox"
                            checked={mod.enabled}
                            onChange={(event) => void toggleMod(mod.id, event.target.checked)}
                          />
                          {mod.name}
                        </label>
                        <span className="muted">
                          {mod.file}
                          {mod.version.length > 0 ? ` · ${mod.version}` : ""} · {formatSize(mod.size)}
                        </span>
                      </div>
                      <button
                        type="button"
                        className="btn ghost"
                        onClick={() => void deleteMod(mod.id, mod.name)}
                      >
                        删除
                      </button>
                    </div>
                  ))
                ) : (
                  <p className="muted">当前实例没有可管理的模组。</p>
                )}
              </div>
            </div>
          </section>
        ) : null}

        {shownTab === "launch" ? (
          <section className="card instance-card">
            <div className="instance-scroll">
              <div className="stack" style={{ gap: 10, paddingTop: 8 }}>
                <Field label="实例显示名">
                  <AppInput placeholder="1–120 个字符"
                    value={form.displayName}
                    onChange={(event) => setForm({ ...form, displayName: event.target.value })}
                  />
                </Field>
                <label className="checkbox">
                  <input
                    type="checkbox"
                    checked={inherit}
                    onChange={(event) => setInherit(event.target.checked)}
                  />
                  继承全局启动设置
                </label>
                <p className="muted">{inheritHint}</p>

                <Field label="Java 路径（留空自动选择）">
                  <div className="row" style={{ gap: 8 }}>
                    <AppInput placeholder="留空自动选择"
                      value={form.java}
                      disabled={disabled}
                      onChange={(event) => setForm({ ...form, java: event.target.value })}
                    />
                    <button type="button" className="btn ghost" disabled={disabled} onClick={() => setJavaPrompt(true)}>
                      选择
                    </button>
                  </div>
                </Field>

                <div className="launch-numbers">
                  <label className="checkbox">
                    <input
                      type="checkbox"
                      checked={form.autoMemory}
                      disabled={disabled}
                      onChange={(event) => setForm({ ...form, autoMemory: event.target.checked })}
                    />
                    自动分配内存
                  </label>
                  <Field label="内存 MB">
                    <AppInput value={form.ram}
                      disabled={disabled}
                      onChange={(event) => setForm({ ...form, ram: event.target.value })}
                    />
                  </Field>
                  <Field label="游戏窗口宽">
                    <AppInput value={form.width}
                      disabled={disabled}
                      onChange={(event) => setForm({ ...form, width: event.target.value })}
                    />
                  </Field>
                  <Field label="游戏窗口高">
                    <AppInput value={form.height}
                      disabled={disabled}
                      onChange={(event) => setForm({ ...form, height: event.target.value })}
                    />
                  </Field>
                </div>

                <label className="checkbox">
                  <input
                    type="checkbox"
                    checked={form.fullScreen}
                    disabled={disabled}
                    onChange={(event) => setForm({ ...form, fullScreen: event.target.checked })}
                  />
                  全屏启动
                </label>
                <label className="checkbox">
                  <input
                    type="checkbox"
                    checked={form.autoChinese}
                    disabled={disabled}
                    onChange={(event) => setForm({ ...form, autoChinese: event.target.checked })}
                  />
                  自动设置简体中文（仅缺少语言选项时）
                </label>

                <Field label="窗口标题">
                  <AppInput placeholder="可留空"
                    value={form.windowTitle}
                    disabled={disabled}
                    onChange={(event) => setForm({ ...form, windowTitle: event.target.value })}
                  />
                </Field>
                <Field label="自动进入服务器">
                  <AppInput placeholder="可留空，例如 play.example.com"
                    value={form.server}
                    disabled={disabled}
                    onChange={(event) => setForm({ ...form, server: event.target.value })}
                  />
                </Field>
                <Field label="额外 JVM 参数" hint="只接受 - 开头的参数；内存请使用上方设置">
                  <AppTextarea
                    rows={3}
                    value={form.jvmArguments}
                    disabled={disabled}
                    onChange={(event) => setForm({ ...form, jvmArguments: event.target.value })}
                  />
                </Field>

                <div className="row" style={{ gap: 10 }}>
                  <button type="button" className="btn primary" onClick={() => void saveOptions()}>
                    保存此实例设置
                  </button>
                  <button type="button" className="btn ghost" onClick={() => void loadOptions(selected)}>
                    重新读取
                  </button>
                  <span className="status">{optionsStatus}</span>
                </div>
              </div>
            </div>
          </section>
        ) : null}

        {shownTab === "maintenance" ? (
          <section className="card instance-card">
            <div className="instance-scroll">
              <div className="stack" style={{ gap: 10, paddingTop: 8 }}>
                <span className="field-label">克隆为独立实例</span>
                <div className="row" style={{ gap: 8 }}>
                  <AppInput placeholder="新实例名称"
                    value={cloneName}
                    onChange={(event) => setCloneName(event.target.value)}
                  />
                  <button type="button" className="btn primary" onClick={() => void clone()}>
                    克隆
                  </button>
                </div>
                <div className="row" style={{ gap: 14 }}>
                  <label className="checkbox">
                    <input type="checkbox" checked={cloneSaves} onChange={(event) => setCloneSaves(event.target.checked)} />
                    包含存档
                  </label>
                  <label className="checkbox">
                    <input
                      type="checkbox"
                      checked={cloneServers}
                      onChange={(event) => setCloneServers(event.target.checked)}
                    />
                    包含服务器列表
                  </label>
                </div>

                <span className="field-label">导出整合包 ZIP</span>
                <div className="row" style={{ gap: 14 }}>
                  <label className="checkbox">
                    <input type="checkbox" checked={exportSaves} onChange={(event) => setExportSaves(event.target.checked)} />
                    包含存档
                  </label>
                  <label className="checkbox">
                    <input
                      type="checkbox"
                      checked={exportServers}
                      onChange={(event) => setExportServers(event.target.checked)}
                    />
                    包含服务器列表
                  </label>
                  <button
                    type="button"
                    className="btn ghost"
                    disabled={!current}
                    onClick={() => setExportPrompt(true)}
                  >
                    选择导出位置
                  </button>
                </div>

                <span className="field-label">卸载 / 删除版本</span>
                <p className="muted">删除前会列出完整路径，再由原生确认框确认；默认保留共享根目录数据。</p>
                <button
                  type="button"
                  className="btn ghost"
                  style={{ alignSelf: "flex-start" }}
                  disabled={!current}
                  onClick={() => void previewUninstall()}
                >
                  预览卸载范围
                </button>
                <div className="stack" style={{ gap: 6 }}>
                  {uninstall ? (
                    <div className="stack" style={{ gap: 6 }}>
                      <span className="field-label">卸载 {uninstall.name}</span>
                      <p className="muted">{uninstall.notice}</p>
                      {uninstall.paths.map((path) => (
                        <span className="muted" key={path}>
                          · {path}
                        </span>
                      ))}
                      <label className="checkbox">
                        <input
                          type="checkbox"
                          checked={confirmed}
                          onChange={(event) => setConfirmed(event.target.checked)}
                        />
                        我已检查删除范围并确认备份
                      </label>
                      <button
                        type="button"
                        className="btn ghost"
                        style={{ alignSelf: "flex-start" }}
                        disabled={!confirmed}
                        onClick={() => void applyUninstall()}
                      >
                        确认卸载
                      </button>
                    </div>
                  ) : (
                    <p className="muted">尚未生成卸载预览。</p>
                  )}
                </div>

                <span className="field-label">内容更新</span>
                <p className="muted">只检查通过启动器管理的内容；执行前会再次核对本地指纹。</p>
                <button
                  type="button"
                  className="btn ghost"
                  style={{ alignSelf: "flex-start" }}
                  disabled={!current}
                  onClick={() => void checkUpdates()}
                >
                  检查内容更新
                </button>
                <div className="stack" style={{ gap: 6 }}>
                  {updates ? (
                    updates.items.length === 0 ? (
                      <p className="muted">当前实例没有启动器管理的内容。</p>
                    ) : (
                      <>
                        {updates.items.map((item) => (
                          <label className="checkbox" key={item.id}>
                            <input
                              type="checkbox"
                              disabled={!item.available}
                              checked={selectedUpdates.includes(item.id)}
                              onChange={(event) =>
                                setSelectedUpdates((previous) =>
                                  event.target.checked
                                    ? [...previous, item.id]
                                    : previous.filter((id) => id !== item.id),
                                )
                              }
                            />
                            {item.name} · {item.before} → {item.after.length > 0 ? item.after : item.error}
                          </label>
                        ))}
                        <button
                          type="button"
                          className="btn ghost"
                          style={{ alignSelf: "flex-start" }}
                          onClick={() => void previewUpdates()}
                        >
                          生成所选更新预览
                        </button>
                      </>
                    )
                  ) : (
                    <p className="muted">尚未检查内容更新。</p>
                  )}
                </div>
                <div className="stack" style={{ gap: 6 }}>
                  {updatePreview ? (
                    <>
                      <span className="field-label">
                        更新预览 · {(updatePreview.bytes / 1048576).toFixed(2)} MiB
                      </span>
                      {updatePreview.changes.map((change) => (
                        <span className="muted" key={`${change.project}-${change.path}`}>
                          {change.project}: {change.before} → {change.after}
                          {"\n"}
                          {change.path}
                        </span>
                      ))}
                      <button
                        type="button"
                        className="btn ghost"
                        style={{ alignSelf: "flex-start" }}
                        onClick={() => void applyUpdates()}
                      >
                        按预览加入更新队列
                      </button>
                    </>
                  ) : (
                    <p className="muted">尚未生成更新预览。</p>
                  )}
                </div>
                <p className="status">{status}</p>
              </div>
            </div>
          </section>
        ) : null}
      </div>

      {javaPrompt ? (
        <PathPrompt
          title="选择 Java"
          label="javaw.exe 路径"
          initial={form.java}
          hint="留空表示自动选择。"
          onCancel={() => setJavaPrompt(false)}
          onConfirm={(path) => {
            setForm({ ...form, java: path });
            setOptionsStatus("已选择 Java 路径，保存后生效。");
            setJavaPrompt(false);
          }}
        />
      ) : null}

      {exportPrompt ? (
        <PathPrompt
          title="导出整合包"
          label="导出文件路径"
          initial={`D:/Exports/${selected || "instance"}.zip`}
          hint="导出为 ZIP 整合包，包含所选内容。"
          onCancel={() => setExportPrompt(false)}
          onConfirm={(path) => {
            setExportPrompt(false);
            void exportInstanceCommand(selected, path, exportServers, exportSaves).then((result) =>
              setStatus(result.message),
            );
          }}
        />
      ) : null}
    </div>
  );
}
