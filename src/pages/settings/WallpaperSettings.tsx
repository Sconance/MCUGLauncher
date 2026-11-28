import { useEffect, useMemo, useRef, useState } from "react";
import { createPortal } from "react-dom";
import chevronLeftIcon from "../../assets/icons/ui/chevron-left.svg";
import chevronRightIcon from "../../assets/icons/ui/chevron-right.svg";
import closeIcon from "../../assets/icons/ui/close.svg";
import SvgIcon from "../../components/SvgIcon";
import { useBackgroundStyle } from "../../shell/backgroundStyle";
import {
  PRESET_WALLPAPERS,
  addCustomWallpapers,
  removeCustomWallpaper,
  selectWallpaper,
  setRandomCustomWallpaper,
  useWallpaper,
} from "../../shell/wallpaper";
import SettingRow from "./SettingRow";
import SettingsCard from "./SettingsCard";
import "./WallpaperSettings.css";

interface GalleryItem {
  key: string;
  kind: "preset" | "custom";
  id: string;
  name: string;
  url: string;
}

/**
 * 背景设置卡片：预设图像、自定义图像（支持导入与删除）、随机自定义，
 * 以及内置图片浏览器；不透明模式下额外提供「米白纯色」背景选项。
 */
export default function WallpaperSettings() {
  const backgroundStyle = useBackgroundStyle();
  const isOpaque = backgroundStyle === "opaque";
  const { selection, randomCustom, customs } = useWallpaper();
  const [status, setStatus] = useState("");
  const [viewerIndex, setViewerIndex] = useState<number | null>(null);
  const fileInputRef = useRef<HTMLInputElement>(null);

  const gallery = useMemo<GalleryItem[]>(
    () => [
      ...PRESET_WALLPAPERS.map((item) => ({
        key: `preset:${item.id}`,
        kind: "preset" as const,
        id: item.id,
        name: item.name,
        url: item.url,
      })),
      ...customs.map((item) => ({
        key: `custom:${item.id}`,
        kind: "custom" as const,
        id: item.id,
        name: item.name,
        url: item.url,
      })),
    ],
    [customs],
  );

  // 列表变动导致当前浏览索引越界时安全收敛
  useEffect(() => {
    if (viewerIndex === null) return;
    if (gallery.length === 0) {
      setViewerIndex(null);
    } else if (viewerIndex >= gallery.length) {
      setViewerIndex(gallery.length - 1);
    }
  }, [gallery.length, viewerIndex]);

  // 图片浏览器键盘导航
  useEffect(() => {
    if (viewerIndex === null) return;
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.key === "Escape") {
        setViewerIndex(null);
      } else if (event.key === "ArrowLeft" && gallery.length > 1) {
        setViewerIndex((prev) => (prev === null ? null : (prev - 1 + gallery.length) % gallery.length));
      } else if (event.key === "ArrowRight" && gallery.length > 1) {
        setViewerIndex((prev) => (prev === null ? null : (prev + 1) % gallery.length));
      }
    };
    window.addEventListener("keydown", onKeyDown);
    return () => window.removeEventListener("keydown", onKeyDown);
  }, [viewerIndex, gallery.length]);

  const openViewerByKey = (key: string) => {
    const idx = gallery.findIndex((item) => item.key === key);
    if (idx >= 0) setViewerIndex(idx);
  };

  const handleImport = async (files: FileList | null) => {
    if (!files || files.length === 0) return;
    try {
      const added = await addCustomWallpapers(files);
      if (added.length === 0) {
        setStatus("未检测到有效的图片文件。");
      } else {
        setStatus(`已添加 ${added.length} 张背景图片。`);
      }
    } catch (error) {
      setStatus(error instanceof Error ? error.message : "导入图片失败。");
    } finally {
      if (fileInputRef.current) fileInputRef.current.value = "";
    }
  };

  const handleRemove = async (id: string) => {
    try {
      await removeCustomWallpaper(id);
      setStatus("");
    } catch (error) {
      setStatus(error instanceof Error ? error.message : "删除图片失败。");
    }
  };

  const isPresetSelected = (id: "default") =>
    !randomCustom &&
    (selection.kind === "preset" ? selection.id === id : selection.kind === "solid" && !isOpaque);

  const isSolidSelected = !randomCustom && isOpaque && selection.kind === "solid";

  const isCustomSelected = (id: string) =>
    !randomCustom && selection.kind === "custom" && selection.id === id;

  const currentViewerItem = viewerIndex !== null ? gallery[viewerIndex] ?? null : null;
  const isCurrentViewerActive = currentViewerItem
    ? currentViewerItem.kind === "preset"
      ? isPresetSelected("default")
      : isCustomSelected(currentViewerItem.id)
    : false;

  return (
    <>
      <SettingsCard title="背景">
        <SettingRow
          title="预设图像"
          control={
            <div className="wp-grid">
              {PRESET_WALLPAPERS.map((preset) => {
                const selected = isPresetSelected(preset.id);
                return (
                  <div className="wp-tile" key={preset.id}>
                    <div className={`wp-thumb-wrap${selected ? " is-selected" : ""}`}>
                      <button
                        type="button"
                        className="wp-thumb-btn"
                        aria-pressed={selected}
                        onClick={() => {
                          selectWallpaper({ kind: "preset", id: preset.id });
                          setStatus("");
                        }}
                      >
                        <img className="wp-thumb-img" src={preset.url} alt={preset.name} />
                      </button>
                      <button
                        type="button"
                        className="wp-thumb-preview"
                        onClick={() => openViewerByKey(`preset:${preset.id}`)}
                      >
                        浏览
                      </button>
                    </div>
                    <span className="wp-label" title={preset.name}>
                      {preset.name}
                    </span>
                  </div>
                );
              })}

              {isOpaque ? (
                <div className="wp-tile">
                  <div className={`wp-thumb-wrap${isSolidSelected ? " is-selected" : ""}`}>
                    <button
                      type="button"
                      className="wp-thumb-btn wp-solid-swatch"
                      aria-pressed={isSolidSelected}
                      onClick={() => {
                        selectWallpaper({ kind: "solid" });
                        setStatus("");
                      }}
                    >
                      <span className="wp-solid-chip" />
                    </button>
                  </div>
                  <span className="wp-label" title="米白纯色">
                    米白纯色
                  </span>
                </div>
              ) : null}
            </div>
          }
        />

        <SettingRow
          title="自定义图像"
          control={
            <div className="wp-grid">
              {customs.map((item) => {
                const selected = isCustomSelected(item.id);
                return (
                  <div className="wp-tile" key={item.id}>
                    <div className={`wp-thumb-wrap${selected ? " is-selected" : ""}`}>
                      <button
                        type="button"
                        className="wp-thumb-btn"
                        aria-pressed={selected}
                        onClick={() => {
                          selectWallpaper({ kind: "custom", id: item.id });
                          setStatus("");
                        }}
                      >
                        <img className="wp-thumb-img" src={item.url} alt={item.name} />
                      </button>
                      <button
                        type="button"
                        className="wp-thumb-preview"
                        onClick={() => openViewerByKey(`custom:${item.id}`)}
                      >
                        浏览
                      </button>
                      <button
                        type="button"
                        className="wp-thumb-remove"
                        aria-label={`删除 ${item.name}`}
                        onClick={() => void handleRemove(item.id)}
                      >
                        <SvgIcon src={closeIcon} className="wp-remove-icon" />
                      </button>
                    </div>
                    <span className="wp-label" title={item.name}>
                      {item.name}
                    </span>
                  </div>
                );
              })}

              <div className="wp-tile">
                <button
                  type="button"
                  className="wp-add-box"
                  onClick={() => fileInputRef.current?.click()}
                >
                  <span className="wp-add-plus" aria-hidden="true">
                    +
                  </span>
                </button>
                <span className="wp-label">添加</span>
              </div>
            </div>
          }
        >
          {status.length > 0 ? <span className="setting-subtitle">{status}</span> : null}
        </SettingRow>

        <input
          ref={fileInputRef}
          type="file"
          accept="image/*"
          multiple
          hidden
          onChange={(event) => void handleImport(event.target.files)}
        />

        <SettingRow
          title="随机自定义"
          control={
            <button
              type="button"
              role="switch"
              aria-checked={randomCustom}
              aria-label="随机自定义"
              disabled={customs.length === 0}
              className={`switch${randomCustom ? " on" : ""}`}
              onClick={() => setRandomCustomWallpaper(!randomCustom)}
            >
              <span className="switch-thumb" />
            </button>
          }
        />
      </SettingsCard>

      {currentViewerItem
        ? createPortal(
            <div
              className="wp-viewer-overlay"
              role="dialog"
              aria-modal="true"
              aria-label={`浏览图片：${currentViewerItem.name}`}
              onClick={() => setViewerIndex(null)}
            >
              <div className="wp-viewer-card" onClick={(event) => event.stopPropagation()}>
                <header className="wp-viewer-head">
                  <div className="wp-viewer-title-group">
                    <span className="wp-viewer-name" title={currentViewerItem.name}>
                      {currentViewerItem.name}
                    </span>
                    <span className="badge">
                      {currentViewerItem.kind === "preset" ? "预设图像" : "自定义图像"}
                    </span>
                    <span className="muted">
                      {(viewerIndex ?? 0) + 1} / {gallery.length}
                    </span>
                  </div>
                  <button
                    type="button"
                    className="btn icon"
                    aria-label="关闭浏览"
                    onClick={() => setViewerIndex(null)}
                  >
                    <SvgIcon src={closeIcon} />
                  </button>
                </header>

                <div className="wp-viewer-stage">
                  {gallery.length > 1 ? (
                    <button
                      type="button"
                      className="wp-viewer-nav is-prev"
                      aria-label="上一张"
                      onClick={() =>
                        setViewerIndex((prev) =>
                          prev === null ? null : (prev - 1 + gallery.length) % gallery.length,
                        )
                      }
                    >
                      <SvgIcon src={chevronLeftIcon} />
                    </button>
                  ) : null}

                  <img
                    className="wp-viewer-image"
                    src={currentViewerItem.url}
                    alt={currentViewerItem.name}
                  />

                  {gallery.length > 1 ? (
                    <button
                      type="button"
                      className="wp-viewer-nav is-next"
                      aria-label="下一张"
                      onClick={() =>
                        setViewerIndex((prev) =>
                          prev === null ? null : (prev + 1) % gallery.length,
                        )
                      }
                    >
                      <SvgIcon src={chevronRightIcon} />
                    </button>
                  ) : null}
                </div>

                {gallery.length > 1 ? (
                  <div className="wp-viewer-strip">
                    {gallery.map((item, idx) => (
                      <button
                        key={item.key}
                        type="button"
                        className={`wp-strip-item${idx === viewerIndex ? " is-active" : ""}`}
                        onClick={() => setViewerIndex(idx)}
                      >
                        <img src={item.url} alt={item.name} />
                      </button>
                    ))}
                  </div>
                ) : null}

                <footer className="wp-viewer-foot">
                  {currentViewerItem.kind === "custom" ? (
                    <button
                      type="button"
                      className="btn ghost"
                      onClick={() => void handleRemove(currentViewerItem.id)}
                    >
                      删除图片
                    </button>
                  ) : (
                    <span />
                  )}
                  <div className="row wp-viewer-actions">
                    <button
                      type="button"
                      className="btn primary"
                      disabled={isCurrentViewerActive}
                      onClick={() => {
                        if (currentViewerItem.kind === "preset") {
                          selectWallpaper({ kind: "preset", id: "default" });
                        } else {
                          selectWallpaper({ kind: "custom", id: currentViewerItem.id });
                        }
                      }}
                    >
                      {isCurrentViewerActive ? "当前背景" : "设为背景"}
                    </button>
                    <button type="button" className="btn ghost" onClick={() => setViewerIndex(null)}>
                      关闭
                    </button>
                  </div>
                </footer>
              </div>
            </div>,
            document.body,
          )
        : null}
    </>
  );
}
