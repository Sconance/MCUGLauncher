import { useEffect, useRef, useState } from "react";
import { openPathInExplorer, pickFile } from "../../api/commands/pathPicker";
import { scanJava as scanJavaCommand } from "../../api/commands/settings";
import { AppInput, AppTextarea } from "../../components/AppInput";
import FadeScrollbar from "../../components/FadeScrollbar";
import { LIMITS } from "../../constants";
import { cleanJavaPath, formatJavaLabel, useJavaArchShown } from "../../shell/javaDisplayPreference";
import SettingRow from "../settings/SettingRow";
import SettingsCard from "../settings/SettingsCard";
import "../SettingsPage.css";

export interface InstanceLaunchForm {
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

interface InstanceLaunchSettingsProps {
  form: InstanceLaunchForm;
  inherit: boolean;
  optionsStatus: string;
  onFormChange: (next: InstanceLaunchForm) => void;
  onInheritChange: (next: boolean) => void;
  onStatusChange: (message: string) => void;
  onSave: () => void;
  onReload: () => void;
  onFallbackPickJava: () => void;
}

/**
 * 单个实例的启动设置：与「设置 - 启动」保持一致的卡片分组、设置行、开关与内联 Java 扫描样式。
 */
export default function InstanceLaunchSettings({
  form,
  inherit,
  optionsStatus,
  onFormChange,
  onInheritChange,
  onStatusChange,
  onSave,
  onReload,
  onFallbackPickJava,
}: InstanceLaunchSettingsProps) {
  const showJavaArch = useJavaArchShown();
  const scrollRef = useRef<HTMLDivElement>(null);
  const [javaItems, setJavaItems] = useState<Awaited<ReturnType<typeof scanJavaCommand>>>([]);
  const [javaScanning, setJavaScanning] = useState(false);
  const [javaScanMessage, setJavaScanMessage] = useState("");
  const [javaScannedOnce, setJavaScannedOnce] = useState(false);

  const scanJava = async () => {
    setJavaScanning(true);
    setJavaScanMessage("正在扫描本机 Java…");
    try {
      const found = await scanJavaCommand();
      setJavaItems(found);
      setJavaScanMessage(found.length === 0 ? "没有找到可用的 Java。" : `找到 ${found.length} 个 Java。`);
    } catch (error) {
      setJavaScanMessage(error instanceof Error ? error.message : "扫描失败。");
    } finally {
      setJavaScanning(false);
    }
  };

  useEffect(() => {
    if (javaScannedOnce) return;
    setJavaScannedOnce(true);
    void scanJava();
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [javaScannedOnce]);

  const disabled = inherit;
  const inheritHint = inherit
    ? "当前继承全局启动设置；关闭后可保存独立设置。"
    : "当前使用独立设置；开启并保存后会恢复继承全局设置。";

  const browseJava = async () => {
    try {
      const picked = await pickFile({ title: "选择 Java 可执行文件（javaw.exe / java.exe）" });
      if (!picked) return;
      onFormChange({ ...form, java: cleanJavaPath(picked) });
      onStatusChange("已选择 Java 路径，保存后生效。");
    } catch {
      onFallbackPickJava();
    }
  };

  const openJava = async () => {
    const message = await openPathInExplorer(form.java);
    onStatusChange(message);
  };

  return (
    <div className="fade-scroll-shell instance-launch-shell">
      <div ref={scrollRef} className="instance-launch-scroll fade-scroll-target">
        <div className="settings-cards">
          <SettingsCard title="启动">
            <SettingRow
              title="实例显示名"
              control={
                <AppInput
                  className="setting-control-input-wide"
                  placeholder="1–120 个字符"
                  value={form.displayName}
                  onChange={(event) => onFormChange({ ...form, displayName: event.target.value })}
                />
              }
            >
              <span className="setting-subtitle">用于启动页与实例列表展示的名称</span>
            </SettingRow>

            <SettingRow
              title="继承全局启动设置"
              control={
                <button
                  type="button"
                  role="switch"
                  aria-checked={inherit}
                  aria-label="继承全局启动设置"
                  className={`switch${inherit ? " on" : ""}`}
                  onClick={() => onInheritChange(!inherit)}
                >
                  <span className="switch-thumb" />
                </button>
              }
            >
              <span className="setting-subtitle">{inheritHint}</span>
            </SettingRow>

            <SettingRow
              title="自动分配内存"
              control={
                <button
                  type="button"
                  role="switch"
                  aria-checked={form.autoMemory}
                  aria-label="自动分配内存"
                  disabled={disabled}
                  className={`switch${form.autoMemory ? " on" : ""}`}
                  onClick={() => onFormChange({ ...form, autoMemory: !form.autoMemory })}
                >
                  <span className="switch-thumb" />
                </button>
              }
            >
              <span className="setting-subtitle">根据系统可用内存自动计算分配大小</span>
            </SettingRow>

            <SettingRow
              title="最大内存"
              control={
                <div className="setting-control">
                  <AppInput
                    className="setting-control-num"
                    value={form.ram}
                    disabled={disabled}
                    onChange={(event) => onFormChange({ ...form, ram: event.target.value })}
                  />
                  <span className="muted">MB</span>
                </div>
              }
            >
              <span className="setting-subtitle">
                允许范围 {LIMITS.ramMin}–{LIMITS.ramMax} MB
              </span>
            </SettingRow>

            <SettingRow
              title="游戏分辨率"
              control={
                <div className="setting-control">
                  <AppInput
                    className="setting-control-num"
                    aria-label="游戏窗口宽"
                    value={form.width}
                    disabled={disabled}
                    onChange={(event) => onFormChange({ ...form, width: event.target.value })}
                  />
                  <span className="setting-control-x">×</span>
                  <AppInput
                    className="setting-control-num"
                    aria-label="游戏窗口高"
                    value={form.height}
                    disabled={disabled}
                    onChange={(event) => onFormChange({ ...form, height: event.target.value })}
                  />
                </div>
              }
            >
              <span className="setting-subtitle">启动游戏时的默认窗口宽高</span>
            </SettingRow>

            <SettingRow
              title="全屏启动"
              control={
                <button
                  type="button"
                  role="switch"
                  aria-checked={form.fullScreen}
                  aria-label="全屏启动"
                  disabled={disabled}
                  className={`switch${form.fullScreen ? " on" : ""}`}
                  onClick={() => onFormChange({ ...form, fullScreen: !form.fullScreen })}
                >
                  <span className="switch-thumb" />
                </button>
              }
            >
              <span className="setting-subtitle">启动游戏后直接进入全屏模式</span>
            </SettingRow>

            <SettingRow
              title="启动时自动设置简体中文"
              control={
                <button
                  type="button"
                  role="switch"
                  aria-checked={form.autoChinese}
                  aria-label="启动时自动设置简体中文"
                  disabled={disabled}
                  className={`switch${form.autoChinese ? " on" : ""}`}
                  onClick={() => onFormChange({ ...form, autoChinese: !form.autoChinese })}
                >
                  <span className="switch-thumb" />
                </button>
              }
            >
              <span className="setting-subtitle">仅在实例缺少语言选项时生效</span>
            </SettingRow>

            <SettingRow
              title="窗口标题"
              control={
                <AppInput
                  className="setting-control-input"
                  placeholder="可留空"
                  value={form.windowTitle}
                  disabled={disabled}
                  onChange={(event) => onFormChange({ ...form, windowTitle: event.target.value })}
                />
              }
            >
              <span className="setting-subtitle">留空则使用游戏默认标题</span>
            </SettingRow>

            <SettingRow
              title="自动进入服务器"
              control={
                <AppInput
                  className="setting-control-input"
                  placeholder="例如 play.example.com"
                  value={form.server}
                  disabled={disabled}
                  onChange={(event) => onFormChange({ ...form, server: event.target.value })}
                />
              }
            >
              <span className="setting-subtitle">留空则正常进入游戏主界面</span>
            </SettingRow>

            <div className="setting-row-block">
              <div className="setting-copy">
                <span className="setting-title">额外 JVM 参数</span>
                <span className="setting-subtitle">只接受 - 开头的参数；内存请使用上方设置</span>
              </div>
              <AppTextarea
                rows={3}
                value={form.jvmArguments}
                disabled={disabled}
                onChange={(event) => onFormChange({ ...form, jvmArguments: event.target.value })}
              />
            </div>

            <div className="settings-actions">
              <button type="button" className="btn primary" onClick={onSave}>
                保存此实例设置
              </button>
              <button type="button" className="btn ghost" onClick={onReload}>
                重新读取
              </button>
              <span className="status">{optionsStatus}</span>
            </div>
          </SettingsCard>

          <SettingsCard title="Java 路径">
            <SettingRow
              title="Java 路径"
              control={
                <div className="setting-control">
                  <button
                    type="button"
                    className="btn ghost"
                    disabled={disabled}
                    onClick={() => void browseJava()}
                  >
                    修改
                  </button>
                  <button
                    type="button"
                    className="btn ghost"
                    disabled={disabled || form.java.trim().length === 0}
                    onClick={() => void openJava()}
                  >
                    打开
                  </button>
                  {form.java.trim().length > 0 ? (
                    <button
                      type="button"
                      className="btn ghost"
                      disabled={disabled}
                      onClick={() => {
                        onFormChange({ ...form, java: "" });
                        onStatusChange("已清空 Java 路径，保存后将自动选择。");
                      }}
                    >
                      清空
                    </button>
                  ) : null}
                </div>
              }
            >
              <span className="setting-subtitle">
                {form.java.trim() || "留空时由启动核心自动选择"}
              </span>
            </SettingRow>

            <div className="java-inline-picker">
              <div className="settings-actions">
                <button
                  type="button"
                  className="btn ghost"
                  disabled={disabled || javaScanning}
                  onClick={() => void scanJava()}
                >
                  {javaScanning ? "正在扫描…" : "扫描本机 Java"}
                </button>
                <span className="muted">{javaScanMessage}</span>
              </div>

              <div className="stack java-inline-results">
                <span className="field-label">扫描结果</span>
                {javaItems.length === 0 ? (
                  <p className="muted">
                    {javaScanning ? "正在查找可用的 Java…" : "还没有扫描结果。"}
                  </p>
                ) : (
                  <div className="section-group java-list-group">
                    {javaItems.map((item) => {
                      const displayPath = cleanJavaPath(item.path);
                      const selected = cleanJavaPath(form.java) === displayPath;
                      return (
                        <button
                          type="button"
                          key={item.path}
                          disabled={disabled}
                          className={`section-item java-list-item${selected ? " selected" : ""}`}
                          onClick={() => {
                            onFormChange({ ...form, java: displayPath });
                            onStatusChange("已选择 Java 路径，保存后生效。");
                          }}
                        >
                          <div className="java-list-copy">
                            <span className="java-list-title">{formatJavaLabel(item, showJavaArch)}</span>
                            <span className="muted java-list-path">{displayPath}</span>
                            {item.error.length > 0 ? <span className="badge red">{item.error}</span> : null}
                          </div>
                        </button>
                      );
                    })}
                  </div>
                )}
              </div>
            </div>

            <div className="settings-actions">
              <button type="button" className="btn primary" onClick={onSave}>
                保存此实例设置
              </button>
              <span className="status">{optionsStatus}</span>
            </div>
          </SettingsCard>
        </div>
      </div>
      <FadeScrollbar targetRef={scrollRef} />
    </div>
  );
}
