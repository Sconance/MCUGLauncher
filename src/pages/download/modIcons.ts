import babricIcon from '../../assets/icons/loaders/babric.svg';
import btaBabricIcon from '../../assets/icons/loaders/bta-babric.svg';
import fabricIcon from '../../assets/icons/loaders/fabric.svg';
import forgeIcon from '../../assets/icons/loaders/forge.svg';
import javaAgentIcon from '../../assets/icons/loaders/java-agent.svg';
import legacyFabricIcon from '../../assets/icons/loaders/legacy-fabric.svg';
import liteloaderIcon from '../../assets/icons/loaders/liteloader.svg';
import modloaderIcon from '../../assets/icons/loaders/modloader.svg';
import neoforgeIcon from '../../assets/icons/loaders/neoforge.svg';
import nilloaderIcon from '../../assets/icons/loaders/nilloader.svg';
import optifineIcon from '../../assets/icons/loaders/optifine.svg';
import ornitheIcon from '../../assets/icons/loaders/ornithe.svg';
import quiltIcon from '../../assets/icons/loaders/quilt.svg';
import riftIcon from '../../assets/icons/loaders/rift.svg';

import adventureIcon from '../../assets/icons/categories/adventure.svg';
import cursedIcon from '../../assets/icons/categories/cursed.svg';
import decorationIcon from '../../assets/icons/categories/decoration.svg';
import economyIcon from '../../assets/icons/categories/economy.svg';
import equipmentIcon from '../../assets/icons/categories/equipment.svg';
import foodIcon from '../../assets/icons/categories/food.svg';
import gameMechanicsIcon from '../../assets/icons/categories/game-mechanics.svg';
import libraryIcon from '../../assets/icons/categories/library.svg';
import magicIcon from '../../assets/icons/categories/magic.svg';
import managementIcon from '../../assets/icons/categories/management.svg';
import minigameIcon from '../../assets/icons/categories/minigame.svg';
import mobsIcon from '../../assets/icons/categories/mobs.svg';
import optimizationIcon from '../../assets/icons/categories/optimization.svg';
import socialIcon from '../../assets/icons/categories/social.svg';
import storageIcon from '../../assets/icons/categories/storage.svg';
import technologyIcon from '../../assets/icons/categories/technology.svg';
import transportationIcon from '../../assets/icons/categories/transportation.svg';
import utilityIcon from '../../assets/icons/categories/utility.svg';
import worldgenIcon from '../../assets/icons/categories/worldgen.svg';

export interface ModIconMeta {
    src: string;
    color?: string;
}

const LOADER_ICONS: Record<string, ModIconMeta> = {
    fabric: { src: fabricIcon, color: '#8b7b6b' },
    forge: { src: forgeIcon, color: '#586299' },
    neoforge: { src: neoforgeIcon, color: '#d97c46' },
    babric: { src: babricIcon, color: '#4c5560' },
    'bta-babric': { src: btaBabricIcon, color: '#4ea832' },
    'bta (babric)': { src: btaBabricIcon, color: '#4ea832' },
    'java-agent': { src: javaAgentIcon, color: '#4c5560' },
    'java agent': { src: javaAgentIcon, color: '#4c5560' },
    'legacy-fabric': { src: legacyFabricIcon, color: '#4c5560' },
    'legacy fabric': { src: legacyFabricIcon, color: '#4c5560' },
    liteloader: { src: liteloaderIcon, color: '#4a90d9' },
    modloader: { src: modloaderIcon, color: '#4c5560' },
    "risugami's modloader": { src: modloaderIcon, color: '#4c5560' },
    nilloader: { src: nilloaderIcon, color: '#d94a7a' },
    ornithe: { src: ornitheIcon, color: '#5b9bd5' },
    quilt: { src: quiltIcon, color: '#8b5fbf' },
    rift: { src: riftIcon, color: '#4c5560' },
    optifine: { src: optifineIcon, color: '#4c5560' },
};

const CATEGORY_ICONS: Record<string, string> = {
    storage: storageIcon,
    存储: storageIcon,
    worldgen: worldgenIcon,
    地形: worldgenIcon,
    management: managementIcon,
    管理: managementIcon,
    'game-mechanics': gameMechanicsIcon,
    机制: gameMechanicsIcon,
    transportation: transportationIcon,
    交通: transportationIcon,
    economy: economyIcon,
    经济: economyIcon,
    technology: technologyIcon,
    科技: technologyIcon,
    library: libraryIcon,
    库: libraryIcon,
    adventure: adventureIcon,
    冒险: adventureIcon,
    magic: magicIcon,
    魔法: magicIcon,
    social: socialIcon,
    社交: socialIcon,
    mobs: mobsIcon,
    生物: mobsIcon,
    utility: utilityIcon,
    实用: utilityIcon,
    food: foodIcon,
    食物: foodIcon,
    minigame: minigameIcon,
    小游戏: minigameIcon,
    cursed: cursedIcon,
    阴间: cursedIcon,
    optimization: optimizationIcon,
    优化: optimizationIcon,
    equipment: equipmentIcon,
    装备: equipmentIcon,
    decoration: decorationIcon,
    装饰: decorationIcon,
};

/** 查找加载器对应的 SVG 图标与品牌色（支持 Modrinth slug 与显示名）。 */
export function getLoaderIcon(name: string): ModIconMeta | undefined {
    const key = name.trim().toLowerCase();
    return LOADER_ICONS[key];
}

/** 查找模组类别对应的 SVG 图标（支持英文 slug 与中文名）。 */
export function getCategoryIcon(tag: string): string | undefined {
    const key = tag.trim().toLowerCase();
    return CATEGORY_ICONS[key] ?? CATEGORY_ICONS[tag.trim()];
}

/** 统一查找任意标签（优先加载器，其次模组类别）对应的图标与可选配色。 */
export function getTagIcon(tag: string): ModIconMeta | undefined {
    const loader = getLoaderIcon(tag);
    if (loader) return loader;
    const category = getCategoryIcon(tag);
    return category ? { src: category } : undefined;
}

/** 加载器与服务端平台保持英文名并规范大小写 */
const LOADER_LABELS: Record<string, string> = {
    fabric: 'Fabric',
    forge: 'Forge',
    neoforge: 'NeoForge',
    quilt: 'Quilt',
    babric: 'Babric',
    'bta-babric': 'BTA (Babric)',
    'java-agent': 'Java Agent',
    'legacy-fabric': 'Legacy Fabric',
    liteloader: 'LiteLoader',
    modloader: "Risugami's ModLoader",
    nilloader: 'NilLoader',
    ornithe: 'Ornithe',
    rift: 'Rift',
    optifine: 'OptiFine',
    bukkit: 'Bukkit',
    spigot: 'Spigot',
    paper: 'Paper',
    purpur: 'Purpur',
    folia: 'Folia',
    velocity: 'Velocity',
    bungeecord: 'BungeeCord',
    waterfall: 'Waterfall',
    sponge: 'Sponge',
    iris: 'Iris',
    canvas: 'Canvas',
    vanilla: 'Vanilla',
    minecraft: 'Minecraft',
    datapack: 'Datapack',
};

/** Modrinth 类别标签中文化映射（不含加载器） */
const CATEGORY_LABELS: Record<string, string> = {
    storage: '存储',
    worldgen: '地形',
    management: '管理',
    'game-mechanics': '机制',
    transportation: '交通',
    economy: '经济',
    technology: '科技',
    library: '库',
    adventure: '冒险',
    magic: '魔法',
    social: '社交',
    mobs: '生物',
    utility: '实用',
    food: '食物',
    minigame: '小游戏',
    cursed: '阴间',
    optimization: '优化',
    equipment: '装备',
    decoration: '装饰',
    challenging: '挑战',
    combat: '战斗',
    'kitchen-sink': '大杂烩',
    lightweight: '轻量',
    multiplayer: '多人',
    quests: '任务',
    realistic: '写实',
    simplistic: '简约',
    themed: '主题',
    tweaks: '微调',
    'vanilla-like': '原版风格',
    fantasy: '奇幻',
    atmosphere: '氛围',
    bloom: '泛光',
    cartoon: '卡通',
    'colored-lighting': '彩色光照',
    foliage: '植被',
    'path-tracing': '路径追踪',
    pbr: 'PBR',
    reflections: '反射',
    shadows: '阴影',
    'semi-realistic': '半写实',
    high: '高画质',
    medium: '中画质',
    low: '低画质',
    potato: '极低配',
    screenshot: '截图',
    audio: '音频',
    blocks: '方块',
    'core-shaders': '核心着色器',
    entities: '实体',
    environment: '环境',
    fonts: '字体',
    gui: '界面',
    items: '物品',
    locale: '本地化',
    models: '模型',
    modded: '模组',
};

/** 仅在模组筛选栏中隐藏的非模组平台标签（服务端插件平台 + Datapack）；具体模组卡片与详情仍保留展示 */
const HIDDEN_MOD_FILTER_TAGS = new Set([
    'bukkit',
    'spigot',
    'paper',
    'purpur',
    'folia',
    'bungeecord',
    'velocity',
    'waterfall',
    'sponge',
    'geyser',
    'datapack',
]);

/** 判断某标签是否应从模组筛选栏中隐藏（不影响单个模组自身的标签展示）。 */
export function isHiddenInModFilter(tag: string): boolean {
    return HIDDEN_MOD_FILTER_TAGS.has(tag.trim().toLowerCase());
}

/** 判断是否为模组加载器标签（用于筛选栏拆分为「加载器」与「类别」两组）。 */
export function isModLoaderTag(tag: string): boolean {
    return getLoaderIcon(tag) !== undefined;
}

/** 格式化标签显示文案：加载器保留英文规范名称，其余类别标签显示中文。 */
export function formatTagLabel(tag: string): string {
    const trimmed = tag.trim();
    const key = trimmed.toLowerCase();
    return LOADER_LABELS[key] ?? CATEGORY_LABELS[key] ?? trimmed;
}
