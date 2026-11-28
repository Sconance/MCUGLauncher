import type { ContentItem, ResourceVersionsInfo, VanillaVersionInfo } from '../../types/launcher';

export interface CachedSearchEntry {
    items: ContentItem[];
    status: string;
}

const MAX_SEARCH_ENTRIES = 60;
const MAX_VERSION_ENTRIES = 60;

const searchCache = new Map<string, CachedSearchEntry>();
const versionsCache = new Map<string, ResourceVersionsInfo>();
const queryByCategory = new Map<string, string>();
let vanillaCache: VanillaVersionInfo[] | null = null;
let rememberedPickInstanceId = '';
let rememberedPackGame = '';
let rememberedPackLoader = '';

export function makeSearchCacheKey(
    category: string,
    query: string,
    game: string,
    loader: string,
): string {
    return `${category}\0${query.trim().toLowerCase()}\0${game.trim().toLowerCase()}\0${loader.trim().toLowerCase()}`;
}

export function getCachedSearch(key: string): CachedSearchEntry | undefined {
    const hit = searchCache.get(key);
    if (!hit) return undefined;
    // 刷新 LRU 顺序
    searchCache.delete(key);
    searchCache.set(key, hit);
    return hit;
}

export function setCachedSearch(key: string, entry: CachedSearchEntry): void {
    searchCache.delete(key);
    searchCache.set(key, entry);
    if (searchCache.size > MAX_SEARCH_ENTRIES) {
        const oldest = searchCache.keys().next().value;
        if (oldest !== undefined) searchCache.delete(oldest);
    }
}

export function makeVersionsCacheKey(id: string, game: string, loader: string): string {
    return `${id}\0${game.trim().toLowerCase()}\0${loader.trim().toLowerCase()}`;
}

export function getCachedVersions(key: string): ResourceVersionsInfo | undefined {
    const hit = versionsCache.get(key);
    if (!hit) return undefined;
    versionsCache.delete(key);
    versionsCache.set(key, hit);
    return hit;
}

export function setCachedVersions(key: string, value: ResourceVersionsInfo): void {
    versionsCache.delete(key);
    versionsCache.set(key, value);
    if (versionsCache.size > MAX_VERSION_ENTRIES) {
        const oldest = versionsCache.keys().next().value;
        if (oldest !== undefined) versionsCache.delete(oldest);
    }
}

export function getCategoryQuery(category: string): string {
    return queryByCategory.get(category) ?? '';
}

export function setCategoryQuery(category: string, query: string): void {
    queryByCategory.set(category, query);
}

export function getCachedVanilla(): VanillaVersionInfo[] | null {
    return vanillaCache;
}

export function setCachedVanilla(items: VanillaVersionInfo[]): void {
    vanillaCache = items;
}

export function getRememberedDownloadFilters(): {
    pickInstanceId: string;
    packGame: string;
    packLoader: string;
} {
    return {
        pickInstanceId: rememberedPickInstanceId,
        packGame: rememberedPackGame,
        packLoader: rememberedPackLoader,
    };
}

export function setRememberedPickInstanceId(id: string): void {
    rememberedPickInstanceId = id;
}

export function setRememberedPackFilters(game: string, loader: string): void {
    rememberedPackGame = game;
    rememberedPackLoader = loader;
}
