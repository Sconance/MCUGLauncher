/** 原版安装页用到的加载器定义、兼容性表与版本号比较。 */

export interface LoaderDef {
    key: string;
    label: string;
    /** 常驻：即使没有版本也要显示抽屉（显示「无」） */
    permanent: boolean;
    /** 从该 Minecraft 版本起才常驻（NeoForge 自 1.20.1 起） */
    since?: string;
}

/** 原版安装页的加载器抽屉顺序：Forge、NeoForge、Fabric 常驻，其余按有无版本决定 */
export const LOADER_DEFS: LoaderDef[] = [
    { key: 'forge', label: 'Forge', permanent: true },
    { key: 'neoforge', label: 'NeoForge', permanent: true, since: '1.20.1' },
    { key: 'fabric', label: 'Fabric', permanent: true },
    { key: 'quilt', label: 'Quilt', permanent: false },
    { key: 'optifine', label: 'OptiFine', permanent: false },
    { key: 'liteloader', label: 'LiteLoader', permanent: false },
];

/** 后端目前提供版本清单的加载器；OptiFine / LiteLoader 暂无数据，抽屉自动隐藏 */
export const SUPPORTED_LOADERS = ['forge', 'neoforge', 'fabric', 'quilt'];

/**
 * 加载器兼容性表：key 的值为「与 key 不能共存的加载器」。
 * 选了某个加载器后，其它落在它名单里的加载器会被禁用，必须先取消它；
 * 表是对称的：A 名单里有 B，则 B 名单里也有 A。OptiFine 可与 Forge 共存。
 */
export const LOADER_CONFLICTS: Record<string, string[]> = {
    forge: ['neoforge', 'fabric', 'quilt', 'liteloader'],
    neoforge: ['forge', 'fabric', 'quilt', 'optifine'],
    fabric: ['forge', 'neoforge', 'quilt', 'optifine', 'liteloader'],
    quilt: ['forge', 'neoforge', 'fabric', 'optifine', 'liteloader'],
    optifine: ['neoforge', 'fabric', 'quilt'],
    liteloader: ['forge', 'neoforge', 'fabric', 'quilt'],
};

/** x.y.z 版本比较：a 不低于 b 时返回 true */
export function versionAtLeast(version: string, target: string): boolean {
    const parse = (value: string) => value.split('.').map((part) => Number.parseInt(part, 10) || 0);
    const a = parse(version);
    const b = parse(target);
    for (let i = 0; i < Math.max(a.length, b.length); i += 1) {
        const left = a[i] ?? 0;
        const right = b[i] ?? 0;
        if (left !== right) return left > right;
    }
    return true;
}
