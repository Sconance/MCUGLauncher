import { useEffect, useState } from "react";
import defaultWallpaperUrl from "../assets/wallpaper/TmpBackground.png";

export type WallpaperSelection =
  | { kind: "preset"; id: "default" }
  | { kind: "solid" }
  | { kind: "custom"; id: string };

export interface PresetWallpaper {
  id: "default";
  name: string;
  url: string;
}

export interface CustomWallpaper {
  id: string;
  name: string;
  url: string;
  createdAt: number;
}

export interface WallpaperState {
  selection: WallpaperSelection;
  randomCustom: boolean;
  customs: CustomWallpaper[];
  /** 当前实际渲染的自定义图 ID（用于随机自定义模式）。 */
  activeCustomId: string | null;
  ready: boolean;
}

export const PRESET_WALLPAPERS: PresetWallpaper[] = [
  {
    id: "default",
    name: "默认背景",
    url: defaultWallpaperUrl,
  },
];

const SELECTION_KEY = "mcugl.wallpaper.selection";
const RANDOM_KEY = "mcugl.wallpaper.randomCustom";
const DB_NAME = "mcugl-wallpapers";
const DB_VERSION = 1;
const STORE_NAME = "images";

interface StoredImageRecord {
  id: string;
  name: string;
  blob: Blob;
  createdAt: number;
}

function loadSelection(): WallpaperSelection {
  try {
    const raw = window.localStorage.getItem(SELECTION_KEY);
    if (!raw) return { kind: "preset", id: "default" };
    const parsed = JSON.parse(raw) as Partial<WallpaperSelection>;
    if (parsed.kind === "solid") return { kind: "solid" };
    if (parsed.kind === "custom" && typeof parsed.id === "string" && parsed.id.length > 0) {
      return { kind: "custom", id: parsed.id };
    }
  } catch {
    // 忽略损坏的本地存储
  }
  return { kind: "preset", id: "default" };
}

function loadRandomCustom(): boolean {
  return window.localStorage.getItem(RANDOM_KEY) === "1";
}

function saveSelection(selection: WallpaperSelection): void {
  window.localStorage.setItem(SELECTION_KEY, JSON.stringify(selection));
}

function saveRandomCustom(enabled: boolean): void {
  window.localStorage.setItem(RANDOM_KEY, enabled ? "1" : "0");
}

let state: WallpaperState = {
  selection: loadSelection(),
  randomCustom: loadRandomCustom(),
  customs: [],
  activeCustomId: null,
  ready: false,
};

const listeners = new Set<(next: WallpaperState) => void>();
let dbPromise: Promise<IDBDatabase> | null = null;
let initStarted = false;

function emit(): void {
  const snapshot = state;
  listeners.forEach((listener) => listener(snapshot));
}

function openDb(): Promise<IDBDatabase> {
  if (dbPromise) return dbPromise;
  dbPromise = new Promise((resolve, reject) => {
    const req = window.indexedDB.open(DB_NAME, DB_VERSION);
    req.onupgradeneeded = () => {
      const db = req.result;
      if (!db.objectStoreNames.contains(STORE_NAME)) {
        db.createObjectStore(STORE_NAME, { keyPath: "id" });
      }
    };
    req.onsuccess = () => resolve(req.result);
    req.onerror = () => reject(req.error ?? new Error("无法打开壁纸数据库"));
  });
  return dbPromise;
}

async function readAllRecords(): Promise<StoredImageRecord[]> {
  const db = await openDb();
  return new Promise((resolve, reject) => {
    const tx = db.transaction(STORE_NAME, "readonly");
    const store = tx.objectStore(STORE_NAME);
    const req = store.getAll();
    req.onsuccess = () => {
      const list = (req.result as StoredImageRecord[]) ?? [];
      list.sort((a, b) => a.createdAt - b.createdAt);
      resolve(list);
    };
    req.onerror = () => reject(req.error ?? new Error("读取壁纸失败"));
  });
}

async function putRecord(record: StoredImageRecord): Promise<void> {
  const db = await openDb();
  return new Promise((resolve, reject) => {
    const tx = db.transaction(STORE_NAME, "readwrite");
    tx.objectStore(STORE_NAME).put(record);
    tx.oncomplete = () => resolve();
    tx.onerror = () => reject(tx.error ?? new Error("保存壁纸失败"));
  });
}

async function deleteRecord(id: string): Promise<void> {
  const db = await openDb();
  return new Promise((resolve, reject) => {
    const tx = db.transaction(STORE_NAME, "readwrite");
    tx.objectStore(STORE_NAME).delete(id);
    tx.oncomplete = () => resolve();
    tx.onerror = () => reject(tx.error ?? new Error("删除壁纸失败"));
  });
}

function pickRandomId(list: CustomWallpaper[]): string | null {
  if (list.length === 0) return null;
  const idx = Math.floor(Math.random() * list.length);
  return list[idx]?.id ?? null;
}

/** 初始化壁纸库：从 IndexedDB 恢复自定义图片并确定随机项。 */
export function initWallpaper(): void {
  if (initStarted) return;
  initStarted = true;
  void readAllRecords()
    .then((records) => {
      const customs: CustomWallpaper[] = records.map((rec) => ({
        id: rec.id,
        name: rec.name,
        url: URL.createObjectURL(rec.blob),
        createdAt: rec.createdAt,
      }));
      let selection = state.selection;
      if (selection.kind === "custom") {
        const selectedId = selection.id;
        if (!customs.some((item) => item.id === selectedId)) {
          selection = { kind: "preset", id: "default" };
          saveSelection(selection);
        }
      }
      const activeCustomId = state.randomCustom ? pickRandomId(customs) : null;
      state = {
        ...state,
        selection,
        customs,
        activeCustomId,
        ready: true,
      };
      emit();
    })
    .catch(() => {
      state = { ...state, ready: true };
      emit();
    });
}

export function selectWallpaper(selection: WallpaperSelection): void {
  saveSelection(selection);
  state = {
    ...state,
    selection,
    // 手动选择具体背景时关闭随机自定义，以免选中项被覆盖造成困惑
    randomCustom: false,
    activeCustomId: null,
  };
  saveRandomCustom(false);
  emit();
}

export function setRandomCustomWallpaper(enabled: boolean): void {
  saveRandomCustom(enabled);
  const activeCustomId = enabled ? pickRandomId(state.customs) : null;
  state = {
    ...state,
    randomCustom: enabled,
    activeCustomId,
  };
  emit();
}

function stripExt(filename: string): string {
  return filename.replace(/\.[^.]+$/, "") || filename;
}

/** 导入一张或多张自定义壁纸图片，并自动切换到最后一张导入的图片。 */
export async function addCustomWallpapers(files: FileList | File[]): Promise<CustomWallpaper[]> {
  const list = Array.from(files).filter((file) => file.type.startsWith("image/"));
  if (list.length === 0) return [];

  const added: CustomWallpaper[] = [];
  const now = Date.now();
  for (let i = 0; i < list.length; i += 1) {
    const file = list[i];
    const id = `custom-${now}-${i}-${Math.random().toString(36).slice(2, 8)}`;
    const record: StoredImageRecord = {
      id,
      name: stripExt(file.name),
      blob: file,
      createdAt: now + i,
    };
    await putRecord(record);
    added.push({
      id,
      name: record.name,
      url: URL.createObjectURL(file),
      createdAt: record.createdAt,
    });
  }

  const nextCustoms = [...state.customs, ...added];
  const last = added[added.length - 1];
  const nextSelection: WallpaperSelection = last
    ? { kind: "custom", id: last.id }
    : state.selection;
  saveSelection(nextSelection);
  saveRandomCustom(false);

  state = {
    ...state,
    customs: nextCustoms,
    selection: nextSelection,
    randomCustom: false,
    activeCustomId: null,
  };
  emit();
  return added;
}

/** 删除一张自定义壁纸；若正在使用则回退到默认背景。 */
export async function removeCustomWallpaper(id: string): Promise<void> {
  const target = state.customs.find((item) => item.id === id);
  await deleteRecord(id);
  if (target) {
    URL.revokeObjectURL(target.url);
  }
  const nextCustoms = state.customs.filter((item) => item.id !== id);
  let nextSelection = state.selection;
  if (nextSelection.kind === "custom" && nextSelection.id === id) {
    nextSelection = { kind: "preset", id: "default" };
    saveSelection(nextSelection);
  }
  const nextRandom = nextCustoms.length > 0 ? state.randomCustom : false;
  if (nextRandom !== state.randomCustom) {
    saveRandomCustom(nextRandom);
  }
  const nextActiveCustomId =
    nextRandom && state.activeCustomId === id ? pickRandomId(nextCustoms) : state.activeCustomId;

  state = {
    ...state,
    customs: nextCustoms,
    selection: nextSelection,
    randomCustom: nextRandom,
    activeCustomId: nextActiveCustomId,
  };
  emit();
}

/**
 * 解析当前应当显示的背景：
 * - 磨砂模式下不支持纯米白背景，若选中了 solid 则回退到默认预设壁纸；
 * - 开启随机自定义且存在自定义图时，展示本次启动随机选中的图。
 */
export function resolveActiveWallpaper(
  current: WallpaperState,
  isOpaque: boolean,
): { kind: "solid" } | { kind: "image"; url: string } {
  if (current.randomCustom && current.customs.length > 0) {
    const picked =
      current.customs.find((item) => item.id === current.activeCustomId) ?? current.customs[0];
    if (picked) return { kind: "image", url: picked.url };
  }
  if (current.selection.kind === "solid") {
    if (isOpaque) return { kind: "solid" };
    return { kind: "image", url: defaultWallpaperUrl };
  }
  if (current.selection.kind === "custom") {
    const selectedId = current.selection.id;
    const found = current.customs.find((item) => item.id === selectedId);
    if (found) return { kind: "image", url: found.url };
  }
  return { kind: "image", url: defaultWallpaperUrl };
}

export function useWallpaper(): WallpaperState {
  const [snapshot, setSnapshot] = useState<WallpaperState>(state);

  useEffect(() => {
    initWallpaper();
    setSnapshot(state);
    listeners.add(setSnapshot);
    return () => {
      listeners.delete(setSnapshot);
    };
  }, []);

  return snapshot;
}
