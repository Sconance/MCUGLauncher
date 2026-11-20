import { useEffect, useMemo, useRef, useState } from 'react';
import {
    installLoader,
    installVanilla,
    listContentVersions,
    listLoaders,
    listVanillaVersions,
    queueContentVersion,
    searchContent,
    toggleBookmark,
} from '../api/commands/content';
import { AppSelect } from '../components/AppInput';
import SplitPage from '../components/SplitPage';
import SvgIcon from '../components/SvgIcon';
import ContentDetail from './download/ContentDetail';
import VanillaInstallPage from './download/VanillaInstallPage';
import VanillaVersionList from './download/VanillaVersionList';
import PackFilterPickers from './download/PackFilterPickers';
import ContentCard from './download/ContentCard';
import FilterPanel from './download/FilterPanel';
import SearchRow from './download/SearchRow';
import ActiveFilters from './download/ActiveFilters';
import InstancePicker from './download/InstancePicker';
import DownloadPrompts from './download/DownloadPrompts';
import Pager from './download/Pager';
import { LOADER_DEFS, SUPPORTED_LOADERS, versionAtLeast } from './download/loaders';
import datapackIcon from '../assets/icons/download/datapack.svg';
import cubeIcon from '../assets/icons/ui/cube.svg';
import gridIcon from '../assets/icons/ui/grid.svg';
import listIcon from '../assets/icons/ui/list.svg';
import navBookmarks from '../assets/icons/nav/nav-bookmarks.svg';
import modIcon from '../assets/icons/download/mod.svg';
import modpackIcon from '../assets/icons/download/modpack.svg';
import resourcepackIcon from '../assets/icons/download/resourcepack.svg';
import shaderIcon from '../assets/icons/download/shader.svg';
import vanillaIcon from '../assets/icons/download/vanilla.svg';
import { useLauncher } from '../state/launcher';
import { useNavigation } from '../shell/navigation';
import { useI18n } from '../i18n';
import type {
    ContentItem,
    InstanceInfo,
    ResourceVersionsInfo,
    VanillaVersionInfo,
} from '../types/launcher';
import { groupVersions } from './vanillaThemes';
import './DownloadPage.css';

/** 左栏内容分区；「原版游戏」单独成组。 */
const SECTIONS = [
    ['mod', 'download.sections.mod'],
    ['modpack', 'download.sections.modpack'],
    ['resourcepack', 'download.sections.resourcepack'],
    ['shader', 'download.sections.shader'],
    ['datapack', 'download.sections.datapack'],
] as const;

const SECTION_ICONS: Record<string, string> = {
    vanilla: vanillaIcon,
    mod: modIcon,
    modpack: modpackIcon,
    resourcepack: resourcepackIcon,
    shader: shaderIcon,
    datapack: datapackIcon,
};

type SectionKey = 'vanilla' | (typeof SECTIONS)[number][0];

const CONTENT_SECTIONS: SectionKey[] = SECTIONS.map(([key]) => key);

/** 实例的加载器主名：`Fabric 0.16.9` → `fabric`；原版实例统一成 `vanilla` */
function instanceLoaderKey(instance: InstanceInfo): string {
    const head =
        instance.loader
            .trim()
            .split(/[\s_/]+/)[0]
            ?.toLowerCase() ?? '';
    if (head.length === 0 || head.includes('原版') || head.includes('vanilla')) return 'vanilla';
    return head;
}

/**
 * 内容分区筛选：模组按实例加载器、光影包区分 Iris / OptiFine、整合包用右上角自选的版本 + 加载器，
 * 资源包 / 数据包不筛（与加载器无关）。
 */
function contentFilter(
    category: string,
    instance?: InstanceInfo,
    pack?: { game: string; loader: string },
): { game: string; loader: string } {
    if (category === 'modpack') return { game: pack?.game ?? '', loader: pack?.loader ?? '' };
    if (!instance) return { game: '', loader: '' };
    if (category !== 'mod' && category !== 'shader') return { game: '', loader: '' };
    const loader = instanceLoaderKey(instance);
    if (category === 'shader') {
        const optifine = loader === 'forge' || loader === 'vanilla';
        return { game: instance.minecraft, loader: optifine ? 'optifine' : 'iris' };
    }
    return { game: instance.minecraft, loader };
}

/** 分区 key -> 语言包 key */
const CATEGORY_LABELS: Record<string, string> = {
    vanilla: 'download.sections.vanilla',
    mod: 'download.sections.mod',
    modpack: 'download.sections.modpack',
    resourcepack: 'download.sections.resourcepack',
    shader: 'download.sections.shader',
    datapack: 'download.sections.datapack',
};

/** 下载量字符串（38.2M / 1.68亿 等）换算成可比较的数值。 */
function downloadWeight(value: string): number {
    const matched = value.replace(/,/g, '').match(/([\d.]+)\s*([KMB万亿]?)/i);
    if (!matched) return 0;
    const amount = Number.parseFloat(matched[1]);
    if (Number.isNaN(amount)) return 0;
    const unit = matched[2].toUpperCase();
    const scale =
        unit === 'B' ? 1e9 : unit === 'M' ? 1e6 : unit === 'K' ? 1e3 : unit === '亿' ? 1e8 : unit === '万' ? 1e4 : 1;
    return amount * scale;
}

/** 下载页：原版安装与内容搜索。 */
export default function DownloadPage() {
    const { overview, instances: instanceCatalog, bookmarks, refreshState } = useLauncher();
    const { openDiscoverBookmarks } = useNavigation();
    const { t } = useI18n();
    // 左栏分区：内容页直接对应 category，安装类页面单独处理
    const [section, setSection] = useState<SectionKey>('mod');
    const isContentSection = CONTENT_SECTIONS.includes(section);
    const [status, setStatus] = useState('');

    const category = isContentSection ? section : 'mod';
    const [query, setQuery] = useState('');
    const [results, setResults] = useState<ContentItem[]>([]);
    const [searching, setSearching] = useState(false);
    const [selected, setSelected] = useState<ContentItem | null>(null);
    const [versions, setVersions] = useState<ResourceVersionsInfo | null>(null);
    const [versionsHint, setVersionsHint] = useState('尚未读取兼容版本。');
    const [versionId, setVersionId] = useState('');
    const [targetMode, setTargetMode] = useState('instance');

    // Modrinth 式列表：排序 / 每页数量 / 页码 / 视图 / 侧栏筛选
    const [sort, setSort] = useState<'relevance' | 'downloads' | 'name'>('relevance');
    const pageSize = 20;
    const [pageIndex, setPageIndex] = useState(0);
    const [view, setView] = useState<'list' | 'grid'>('list');
    /** 按哪个实例筛选内容：默认跟随主页启动实例，可在右上角切换 */
    const [pickInstanceId, setPickInstanceId] = useState('');
    const [instanceOpen, setInstanceOpen] = useState(false);
    /** 整合包分区的版本 / 加载器筛选，空串表示不限 */
    const [packGame, setPackGame] = useState('');
    const [packLoader, setPackLoader] = useState('');
    const [packGameOpen, setPackGameOpen] = useState(false);
    const [packLoaderOpen, setPackLoaderOpen] = useState(false);
    const packPickerRef = useRef<HTMLDivElement>(null);
    const [activeTags, setActiveTags] = useState<string[]>([]);
    const [activeAuthors, setActiveAuthors] = useState<string[]>([]);
    const [railQuery, setRailQuery] = useState('');
    const [filterOpen, setFilterOpen] = useState(false);
    const filterRef = useRef<HTMLDivElement>(null);

    const [vanilla, setVanilla] = useState<VanillaVersionInfo[]>([]);
    const [vanillaHint, setVanillaHint] = useState('正在读取 Mojang 版本列表…');
    /** 三个版本抽屉的开合状态，默认展开正式版 */
    const [openVanilla, setOpenVanilla] = useState<Record<string, boolean>>({ release: true });
    const [vanillaQuery, setVanillaQuery] = useState('');
    /** 点进版本后进入的安装页；null 表示还在版本列表 */
    const [vanillaPick, setVanillaPick] = useState<VanillaVersionInfo | null>(null);
    const [instanceName, setInstanceName] = useState('');
    const [nameTouched, setNameTouched] = useState(false);
    const [pickVersions, setPickVersions] = useState<Record<string, string[]>>({});
    const [pickLoader, setPickLoader] = useState<Record<string, string>>({});
    const [openPick, setOpenPick] = useState<string | null>(null);
    const [pickLoading, setPickLoading] = useState(false);
    /** 右栏切换：先向左/右滑出旧页，再从另一侧滑入新页 */
    const [slideOut, setSlideOut] = useState<null | 'left' | 'right'>(null);
    const [enterAnim, setEnterAnim] = useState<null | 'forward' | 'back'>(null);
    const slideTimers = useRef<number[]>([]);

    const [prompt, setPrompt] = useState<'download' | null>(null);

    useEffect(() => {
        let alive = true;
        void listVanillaVersions().then((vanillaResult) => {
            if (!alive) return;
            setVanilla(vanillaResult);
            setVanillaHint(`已读取 ${vanillaResult.length} 个正式版、快照和远古版本。`);
        });
        return () => {
            alive = false;
        };
    }, []);

    /** 三个抽屉：正式版 / 快照版 / 远古版，各自按主题更新分组 */
    const vanillaDrawers = useMemo(() => {
        const keyword = vanillaQuery.trim().toLowerCase();
        const matched = vanilla.filter((item) => item.id.toLowerCase().includes(keyword));
        return [
            { key: 'release', label: '正式版', items: matched.filter((item) => item.type === 'release') },
            { key: 'snapshot', label: '快照版', items: matched.filter((item) => item.type === 'snapshot') },
            {
                key: 'old',
                label: '远古版',
                items: matched.filter((item) => item.type === 'old_beta' || item.type === 'old_alpha'),
            },
        ].map((drawer) => ({ ...drawer, groups: groupVersions(drawer.items) }));
    }, [vanilla, vanillaQuery]);

    /** 整合包的版本下拉：只列正式版，按从新到旧 */
    const releaseVersions = useMemo(() => vanilla.filter((item) => item.type === 'release'), [vanilla]);

    const currentInstance = instanceCatalog?.items.find((item) => item.id === overview?.selectedInstanceId);
    /** 当前用于筛选内容的实例：没手动切过就跟着主页启动实例 */
    const contentInstance = instanceCatalog?.items.find((item) => item.id === pickInstanceId) ?? currentInstance;

    useEffect(() => {
        if (pickInstanceId.length > 0) return;
        if (overview?.selectedInstanceId) setPickInstanceId(overview.selectedInstanceId);
    }, [overview?.selectedInstanceId, pickInstanceId]);

    /** 运行环境类标签单独成组，其余标签归到“类别”。 */
    const ENV_TAGS = ['客户端', '服务端', '客户端或服务端', '通用'];

    const tagCounts = useMemo(() => {
        const counter = new Map<string, number>();
        results.forEach((item) => item.tags.forEach((tag) => counter.set(tag, (counter.get(tag) ?? 0) + 1)));
        return [...counter.entries()].sort((a, b) => b[1] - a[1] || a[0].localeCompare(b[0]));
    }, [results]);
    const envTags = tagCounts.filter(([tag]) => ENV_TAGS.includes(tag));
    const kindTags = tagCounts.filter(([tag]) => !ENV_TAGS.includes(tag));

    const authorCounts = useMemo(() => {
        const counter = new Map<string, number>();
        results.forEach((item) => {
            if (item.author.length > 0) counter.set(item.author, (counter.get(item.author) ?? 0) + 1);
        });
        return [...counter.entries()].sort((a, b) => b[1] - a[1] || a[0].localeCompare(b[0]));
    }, [results]);

    const filteredResults = useMemo(() => {
        const keyword = railQuery.trim().toLowerCase();
        const rows = results.filter((item) => {
            const tagsOk = activeTags.every((tag) => item.tags.includes(tag));
            const authorsOk = activeAuthors.length === 0 || activeAuthors.includes(item.author);
            const keywordOk =
                keyword.length === 0 ||
                [item.name, item.description, item.author].some((field) => field.toLowerCase().includes(keyword));
            return tagsOk && authorsOk && keywordOk;
        });
        if (sort === 'downloads') rows.sort((a, b) => downloadWeight(b.downloads) - downloadWeight(a.downloads));
        else if (sort === 'name') rows.sort((a, b) => a.name.localeCompare(b.name));
        return rows;
    }, [results, activeTags, activeAuthors, railQuery, sort]);

    const resultPageCount = Math.max(1, Math.ceil(filteredResults.length / pageSize));
    const resultPage = Math.min(pageIndex, resultPageCount - 1);
    const visibleResults = filteredResults.slice(resultPage * pageSize, resultPage * pageSize + pageSize);

    const toggleTag = (tag: string) => {
        setActiveTags((current) =>
            current.includes(tag) ? current.filter((entry) => entry !== tag) : [...current, tag],
        );
        setPageIndex(0);
    };

    const toggleAuthor = (author: string) => {
        setActiveAuthors((current) =>
            current.includes(author) ? current.filter((entry) => entry !== author) : [...current, author],
        );
        setPageIndex(0);
    };

    const clearFilters = () => {
        setActiveTags([]);
        setActiveAuthors([]);
        setRailQuery('');
        setPageIndex(0);
    };

    /** 切分区：只换 key，搜索等旧内容淡出、新内容就位后由 handleShownChange 触发。 */
    const selectSection = (key: string) => {
        if (key === 'bookmarks') {
            setFilterOpen(false);
            openDiscoverBookmarks();
            return;
        }
        if (key === section) return;
        setFilterOpen(false);
        setSection(key as SectionKey);
    };

    /** 选实例：关掉弹层、记住它，并立刻用新实例的版本 / 加载器重搜当前分区 */
    const chooseInstance = (id: string) => {
        setInstanceOpen(false);
        setPickInstanceId(id);
        if (!CONTENT_SECTIONS.includes(section)) return;
        const next = instanceCatalog?.items.find((item) => item.id === id);
        void search(section, query, contentFilter(section, next, { game: packGame, loader: packLoader }));
    };

    /** 整合包分区：选游戏版本后按「版本 + 当前加载器」重搜 */
    const choosePackGame = (game: string) => {
        setPackGameOpen(false);
        setPackGame(game);
        if (section !== 'modpack') return;
        void search('modpack', query, contentFilter('modpack', contentInstance, { game, loader: packLoader }));
    };

    /** 整合包分区：选加载器后按「当前版本 + 加载器」重搜 */
    const choosePackLoader = (loader: string) => {
        setPackLoaderOpen(false);
        setPackLoader(loader);
        if (section !== 'modpack') return;
        void search('modpack', query, contentFilter('modpack', contentInstance, { game: packGame, loader }));
    };

    /** SplitPage 换好内容后调用：内容分区立刻按新分类搜一次。 */
    const handleShownChange = (shown: string) => {
        if (!CONTENT_SECTIONS.includes(shown as SectionKey)) return;
        void search(shown, query);
    };

    const search = async (
        nextCategory: string = category,
        nextQuery: string = query,
        preset?: { game: string; loader: string },
    ) => {
        setSelected(null);
        setVersions(null);
        setSearching(true);
        setResults([]);
        setPageIndex(0);
        setActiveTags([]);
        setActiveAuthors([]);
        setStatus('正在搜索 Modrinth…');
        const filter = preset ?? contentFilter(nextCategory, contentInstance, { game: packGame, loader: packLoader });
        const result = await searchContent(nextCategory, nextQuery.trim(), filter.game, filter.loader);
        setSearching(false);
        setResults(result.items);
        const scope = [filter.game, filter.loader].filter((value) => value.length > 0).join(' · ');
        setStatus(`搜索完成，${scope.length > 0 ? `${scope} · ` : ''}共 ${result.items.length} 项。`);
    };

    // 点击筛选面板以外的地方收起它
    useEffect(() => {
        if (!filterOpen) return;
        const onDown = (event: MouseEvent) => {
            if (filterRef.current && !filterRef.current.contains(event.target as Node)) setFilterOpen(false);
        };
        const onKey = (event: KeyboardEvent) => {
            if (event.key === 'Escape') setFilterOpen(false);
        };
        window.addEventListener('mousedown', onDown);
        window.addEventListener('keydown', onKey);
        return () => {
            window.removeEventListener('mousedown', onDown);
            window.removeEventListener('keydown', onKey);
        };
    }, [filterOpen]);

    // 整合包的两个下拉：点外面或按 Esc 收起
    useEffect(() => {
        if (!packGameOpen && !packLoaderOpen) return;
        const close = () => {
            setPackGameOpen(false);
            setPackLoaderOpen(false);
        };
        const onDown = (event: MouseEvent) => {
            if (packPickerRef.current && !packPickerRef.current.contains(event.target as Node)) close();
        };
        const onKey = (event: KeyboardEvent) => {
            if (event.key === 'Escape') close();
        };
        window.addEventListener('mousedown', onDown);
        window.addEventListener('keydown', onKey);
        return () => {
            window.removeEventListener('mousedown', onDown);
            window.removeEventListener('keydown', onKey);
        };
    }, [packGameOpen, packLoaderOpen]);

    const showDetail = (item: ContentItem) => {
        setSelected(item);
        setVersions(null);
        setVersionsHint('尚未读取兼容版本。');
        setVersionId('');
        setTargetMode('instance');
    };

    /** 列表 → 详情：旧列表向左滑出，详情从右侧滑入 */
    const openContentDetail = (item: ContentItem) => runSlide(() => showDetail(item), 'forward');

    /** 详情 → 列表：反向滑回 */
    const closeContentDetail = () => runSlide(() => setSelected(null), 'back');

    const loadVersions = async (item: ContentItem) => {
        setVersionsHint('正在读取兼容版本…');
        const result = await listContentVersions(item.id);
        setVersions(result);
        setVersionId(result.items[0]?.id ?? '');
        setVersionsHint(`${result.game} · ${result.loader} · ${result.items.length} 个兼容版本`);
    };

    const queueDownload = async () => {
        if (!versions || versionId.length === 0) {
            setStatus('请选择资源版本。');
            return;
        }
        const localOnly = category === 'modpack' || category === 'datapack';
        if (localOnly || targetMode === 'local') {
            setPrompt('download');
            return;
        }
        await queueContentVersion(versions.token, versionId, 'instance');
        setStatus('下载请求已提交，请查看任务列表。');
    };

    const bookmark = async (item: ContentItem) => {
        const result = await toggleBookmark(item.id, item.name, item.description, category);
        await refreshState();
        setStatus(result.message);
    };

    /** 滑出旧页 → 换内容 → 从另一侧滑入；计时器统一登记，卸载时清理 */
    const runSlide = (swap: () => void, dir: 'forward' | 'back') => {
        if (slideOut !== null) return;
        setSlideOut(dir === 'forward' ? 'left' : 'right');
        const out = window.setTimeout(() => {
            swap();
            setSlideOut(null);
            setEnterAnim(dir);
            const enter = window.setTimeout(() => setEnterAnim(null), 240);
            slideTimers.current.push(enter);
        }, 170);
        slideTimers.current.push(out);
    };

    useEffect(
        () => () => {
            slideTimers.current.forEach((timer) => window.clearTimeout(timer));
        },
        [],
    );

    /** 进入某个版本的安装页：拉取各加载器版本；默认不选加载器，直接装原版 */
    const prepareVanillaDetail = async (item: VanillaVersionInfo) => {
        setInstanceName(item.id);
        setNameTouched(false);
        setStatus('');
        setPickLoader({});
        setPickVersions({});
        setOpenPick(null);
        setPickLoading(true);
        try {
            const entries = await Promise.all(
                SUPPORTED_LOADERS.map(
                    async (loader) => [loader, (await listLoaders(item.id, loader)).versions] as const,
                ),
            );
            const catalog = Object.fromEntries(entries) as Record<string, string[]>;
            setPickVersions(catalog);
        } finally {
            setPickLoading(false);
        }
    };

    /** 重新拉取 Mojang 版本清单 */
    const refreshVanilla = () => {
        void listVanillaVersions().then((result) => {
            setVanilla(result);
            setVanillaHint(`已读取 ${result.length} 个正式版、快照和远古版本。`);
        });
    };

    const openVanillaDetail = (item: VanillaVersionInfo) => {
        runSlide(() => {
            setVanillaPick(item);
            void prepareVanillaDetail(item);
        }, 'forward');
    };

    const closeVanillaDetail = () => {
        runSlide(() => setVanillaPick(null), 'back');
    };

    /** 实例名自动组合：版本名 + 已选加载器；手动改过就不再覆盖 */
    useEffect(() => {
        if (!vanillaPick || nameTouched) return;
        const segments = LOADER_DEFS.filter((def) => pickLoader[def.key]).map(
            (def) => `${def.label}_${pickLoader[def.key]}`,
        );
        setInstanceName([vanillaPick.id, ...segments].join(' - '));
    }, [vanillaPick, pickLoader, nameTouched]);

    /** 安装页的加载器抽屉：常驻的必显示（无版本时显示「无」），非常驻的有版本才显示 */
    const loaderRows = useMemo(() => {
        if (!vanillaPick) return [];
        return LOADER_DEFS.filter((def) => {
            if (def.permanent) {
                if (!def.since) return true;
                return versionAtLeast(vanillaPick.id, def.since);
            }
            return (pickVersions[def.key]?.length ?? 0) > 0;
        }).map((def) => ({ ...def, versions: pickVersions[def.key] ?? [] }));
    }, [vanillaPick, pickVersions]);

    const submitVanillaInstance = async () => {
        if (!vanillaPick) return;
        const base = LOADER_DEFS.find(
            (def) => def.key !== 'optifine' && def.key !== 'liteloader' && pickLoader[def.key],
        );
        try {
            if (base) await installLoader(vanillaPick.id, base.key, pickLoader[base.key]);
            else await installVanilla(vanillaPick.id);
            setStatus(`下载请求已提交：${instanceName}`);
        } catch {
            setStatus(`安装接口尚未接入，已记录：${instanceName}`);
        }
    };

    return (
        <>
            <SplitPage
                title={t('download.title')}
                navLabel="下载分区"
                groups={[
                    [{ key: 'vanilla', label: t('download.sections.vanilla'), icon: vanillaIcon }],
                    SECTIONS.map(([key, label]) => ({ key, label: t(label), icon: SECTION_ICONS[key] })),
                    [{ key: 'bookmarks', label: '收藏', icon: navBookmarks, jump: true }],
                ]}
                active={section}
                onSelect={selectSection}
                onShownChange={handleShownChange}
                contentClassName="dl-scroll"
                footer={
                    section === 'vanilla' && vanillaPick ? (
                        /* 下载按钮固定在右栏底部中央，不随内容滚动 */
                        <div className="vd-bar">
                            {status.length > 0 ? <p className="status vd-status">{status}</p> : null}
                            <button
                                type="button"
                                className="btn primary vd-download"
                                onClick={() => void submitVanillaInstance()}
                            >
                                下载
                            </button>
                        </div>
                    ) : (
                        <p className="status" style={{ marginTop: 8 }}>
                            {status}
                        </p>
                    )
                }
            >
                {(shown) => {
                    // 渲染跟着 shown 走：淡出的那 130ms 里仍然是旧分区的内容
                    const tab = shown === 'vanilla' ? 'vanilla' : 'search';
                    const viewCategory = CONTENT_SECTIONS.includes(shown as SectionKey) ? shown : 'mod';
                    return (
                        <>
                            {tab === 'search' ? (
                                <div className="dl">
                                    <div
                                        className={`dl-swap vd-swap${
                                            slideOut === 'left'
                                                ? ' is-out-left'
                                                : slideOut === 'right'
                                                    ? ' is-out-right'
                                                    : ''
                                        }${enterAnim ? ` enter-${enterAnim}` : ''}`}
                                    >
                                        {selected ? (
                                            <ContentDetail
                                                item={selected}
                                                categoryLabel={t(CATEGORY_LABELS[category] ?? 'download.list')}
                                                versions={versions}
                                                versionsHint={versionsHint}
                                                versionId={versionId}
                                                onVersionChange={setVersionId}
                                                targetMode={targetMode}
                                                onTargetModeChange={setTargetMode}
                                                localOnly={viewCategory === 'modpack' || viewCategory === 'datapack'}
                                                onLoadVersions={() => void loadVersions(selected)}
                                                onDownload={() => void queueDownload()}
                                                onBack={closeContentDetail}
                                            />
                                        ) : (
                                            <>
                                                {/* 工具条：排序 / 拉伸 / 显示样式 / 筛选 */}
                                                <div className="dl-topbar">
                                                    <label className="dl-sort">
                                                        排序方式：
                                                        <AppSelect
                                                            className="dl-sort-select"
                                                            value={sort}
                                                            onChange={(event) => {
                                                                setSort(
                                                                    event.target.value as
                                                                        'relevance' | 'downloads' | 'name',
                                                                );
                                                                setPageIndex(0);
                                                            }}
                                                        >
                                                            <option value="relevance">Relevance</option>
                                                            <option value="downloads">下载量</option>
                                                            <option value="name">名称</option>
                                                        </AppSelect>
                                                    </label>

                                                    <span className="spacer" />

                                                    {viewCategory === 'modpack' ? (
                                                        <PackFilterPickers
                                                            game={packGame}
                                                            loader={packLoader}
                                                            gameOpen={packGameOpen}
                                                            loaderOpen={packLoaderOpen}
                                                            onToggleGame={() => {
                                                                setPackLoaderOpen(false);
                                                                setPackGameOpen((current) => !current);
                                                            }}
                                                            onToggleLoader={() => {
                                                                setPackGameOpen(false);
                                                                setPackLoaderOpen((current) => !current);
                                                            }}
                                                            onPickGame={choosePackGame}
                                                            onPickLoader={choosePackLoader}
                                                            versions={releaseVersions.map((item) => item.id)}
                                                            rootRef={packPickerRef}
                                                        />
                                                    ) : (
                                                        <button
                                                            type="button"
                                                            className="dl-instance"
                                                            aria-label="选择要安装到的实例"
                                                            onClick={() => setInstanceOpen(true)}
                                                        >
                                                            <span className="dl-instance-icon" aria-hidden="true">
                                                                <SvgIcon src={cubeIcon} />
                                                            </span>
                                                            <span className="dl-instance-text">
                                                                <span className="dl-instance-name">
                                                                    {contentInstance?.displayName ?? '选择实例'}
                                                                </span>
                                                                <span className="dl-instance-sub">
                                                                    {contentInstance
                                                                        ? `${contentInstance.minecraft} · ${contentInstance.loader}`
                                                                        : '未选择实例'}
                                                                </span>
                                                            </span>
                                                        </button>
                                                    )}

                                                    <button
                                                        type="button"
                                                        className="dl-icon-btn"
                                                        aria-label={
                                                            view === 'list' ? '切换到网格视图' : '切换到列表视图'
                                                        }
                                                        onClick={() =>
                                                            setView((current) => (current === 'list' ? 'grid' : 'list'))
                                                        }
                                                    >
                                                        {view === 'list' ? (
                                                            <SvgIcon src={gridIcon} />
                                                        ) : (
                                                            <SvgIcon src={listIcon} />
                                                        )}
                                                    </button>

                                                    <FilterPanel
                                                        open={filterOpen}
                                                        onToggle={() => setFilterOpen((current) => !current)}
                                                        onClose={() => setFilterOpen(false)}
                                                        activeCount={activeTags.length + activeAuthors.length}
                                                        railQuery={railQuery}
                                                        onRailQueryChange={(value) => {
                                                            setRailQuery(value);
                                                            setPageIndex(0);
                                                        }}
                                                        authorCounts={authorCounts}
                                                        envTags={envTags}
                                                        kindTags={kindTags}
                                                        activeTags={activeTags}
                                                        activeAuthors={activeAuthors}
                                                        onToggleTag={toggleTag}
                                                        onToggleAuthor={toggleAuthor}
                                                        onClear={clearFilters}
                                                        rootRef={filterRef}
                                                    />
                                                </div>

                                                <SearchRow
                                                    placeholder={t('download.searchPlaceholder', {
                                                        sections: t(
                                                            CATEGORY_LABELS[shown] ?? 'download.sections.mod',
                                                        ),
                                                    })}
                                                    value={query}
                                                    onChange={setQuery}
                                                    onSubmit={() => void search()}
                                                />

                                                {activeTags.length + activeAuthors.length > 0 ? (
                                                    <ActiveFilters
                                                        tags={activeTags}
                                                        authors={activeAuthors}
                                                        onRemoveTag={toggleTag}
                                                        onRemoveAuthor={toggleAuthor}
                                                        onClear={clearFilters}
                                                    />
                                                ) : null}

                                                {searching ? <p className="muted">正在搜索…</p> : null}
                                                {!searching && filteredResults.length === 0 ? (
                                                    <p className="muted">
                                                        {viewCategory === 'mod' &&
                                                        contentInstance &&
                                                        instanceLoaderKey(contentInstance) === 'vanilla'
                                                            ? '该实例没有模组加载器，装不了模组。先给实例装 Forge / Fabric 之类的加载器，或换一个实例。'
                                                            : '没有匹配的项目。换个关键词，或清除筛选试试。'}
                                                    </p>
                                                ) : null}

                                                <div className={`dl-list${view === 'grid' ? ' grid' : ''}`}>
                                                    {visibleResults.map((item) => (
                                                        <ContentCard
                                                            key={item.id}
                                                            item={item}
                                                            bookmarked={
                                                                bookmarks?.some(
                                                                    (favorite) => favorite.id === item.id,
                                                                ) ?? false
                                                            }
                                                            onOpen={() => openContentDetail(item)}
                                                            onToggleBookmark={() => void bookmark(item)}
                                                        />
                                                    ))}
                                                </div>

                                                {filteredResults.length > 0 ? (
                                                    <Pager
                                                        page={resultPage}
                                                        pageCount={resultPageCount}
                                                        onPageChange={setPageIndex}
                                                    />
                                                ) : null}
                                            </>
                                        )}
                                    </div>
                                </div>
                            ) : null}

                            {tab === 'vanilla' ? (
                                <div
                                    className={`vd-swap${slideOut === 'left' ? ' is-out-left' : slideOut === 'right' ? ' is-out-right' : ''}${
                                        enterAnim ? ` enter-${enterAnim}` : ''
                                    }`}
                                >
                                    {vanillaPick ? (
                                        /* ---------- 版本详情：实例名 + 加载器抽屉 ---------- */
                                        <VanillaInstallPage
                                            minecraft={vanillaPick.id}
                                            instanceName={instanceName}
                                            onInstanceNameChange={(name) => {
                                                setNameTouched(true);
                                                setInstanceName(name);
                                            }}
                                            loading={pickLoading}
                                            rows={loaderRows}
                                            pickLoader={pickLoader}
                                            openPick={openPick}
                                            onOpenPickChange={setOpenPick}
                                            onPickVersion={(key, version) => {
                                                setPickLoader((current) => ({ ...current, [key]: version }));
                                                setOpenPick(null);
                                            }}
                                            onClearLoader={(key) => {
                                                setPickLoader((current) => {
                                                    const next = { ...current };
                                                    delete next[key];
                                                    return next;
                                                });
                                                setOpenPick(null);
                                            }}
                                            onBack={closeVanillaDetail}
                                        />
                                    ) : (
                                        /* ---------- 版本列表 ---------- */
                                        <VanillaVersionList
                                            query={vanillaQuery}
                                            onQueryChange={setVanillaQuery}
                                            onRefresh={refreshVanilla}
                                            drawers={vanillaDrawers}
                                            open={openVanilla}
                                            onToggle={(key) =>
                                                setOpenVanilla((current) => ({
                                                    ...current,
                                                    [key]: !(current[key] ?? false),
                                                }))
                                            }
                                            onPick={openVanillaDetail}
                                            hint={vanillaHint}
                                        />
                                    )}
                                </div>
                            ) : null}
                        </>
                    );
                }}
            </SplitPage>

            <DownloadPrompts
                prompt={prompt}
                onCancel={() => setPrompt(null)}
                onPickDownloadDir={(path) => {
                    setPrompt(null);
                    if (!versions || versionId.length === 0) return;
                    void queueContentVersion(versions.token, versionId, 'local', path);
                    setStatus('下载请求已提交，请查看任务列表。');
                }}
            />

            {instanceOpen ? (
                <InstancePicker
                    title={t('download.pickInstanceTitle', {
                        sections: t(CATEGORY_LABELS[category] ?? 'download.sections.mod'),
                    })}
                    hint={
                        category === 'mod'
                            ? '只显示与该实例的游戏版本、加载器匹配的模组。'
                            : category === 'shader'
                                ? '光影包按实例区分 Iris / OptiFine 来源。'
                                : '资源包与数据包与加载器无关，不按实例筛选。'
                    }
                    instances={instanceCatalog?.items ?? []}
                    activeId={contentInstance?.id}
                    homeId={overview?.selectedInstanceId ?? undefined}
                    onPick={chooseInstance}
                    onClose={() => setInstanceOpen(false)}
                />
            ) : null}
        </>
    );
}
