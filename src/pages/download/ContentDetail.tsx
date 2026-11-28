import { useEffect, useMemo, useState, type CSSProperties } from 'react';
import { AppSelect } from '../../components/AppInput';
import SvgIcon from '../../components/SvgIcon';
import chevronDownIcon from '../../assets/icons/ui/chevron-down.svg';
import chevronLeftIcon from '../../assets/icons/ui/chevron-left.svg';
import type {
    ContentItem,
    InstanceInfo,
    ResourceVersionInfo,
    ResourceVersionsInfo,
} from '../../types/launcher';
import { isAprilFoolsId, resolveSnapshotTargetRelease } from '../vanillaThemes';
import { formatTagLabel, getLoaderIcon, getTagIcon } from './modIcons';

interface ContentDetailProps {
    item: ContentItem;
    /** 返回按钮上的文字，取当前分区的名称 */
    categoryLabel: string;
    /** 当前选中的实例；为空表示「不选择实例」，按游戏版本与加载器分类展示 */
    scopedInstance?: InstanceInfo;
    versions: ResourceVersionsInfo | null;
    versionsLoading: boolean;
    versionsExiting?: boolean;
    versionsHint: string;
    versionId: string;
    onVersionChange: (id: string) => void;
    targetMode: string;
    onTargetModeChange: (mode: string) => void;
    /** 整合包 / 数据包 / 未选实例时只能下载到目录 */
    localOnly: boolean;
    onLoadVersions: () => void;
    onDownload: () => void;
    onBack: () => void;
}

const LOADER_DISPLAY: Record<string, string> = {
    neoforge: 'NeoForge',
    fabric: 'Fabric',
    forge: 'Forge',
    quilt: 'Quilt',
    liteloader: 'LiteLoader',
    rift: 'Rift',
    iris: 'Iris',
    optifine: 'OptiFine',
    minecraft: '原版',
    vanilla: '原版',
};

const LOADER_RANK: Record<string, number> = {
    neoforge: 0,
    fabric: 1,
    forge: 2,
    quilt: 3,
    iris: 4,
    optifine: 5,
};

function formatLoaderLabel(raw: string): string {
    const key = raw.trim().toLowerCase();
    return LOADER_DISPLAY[key] ?? (raw.length > 0 ? raw[0].toUpperCase() + raw.slice(1) : '通用');
}

function isPreviewVersion(versionType?: string): boolean {
    const t = (versionType ?? 'release').toLowerCase();
    return t === 'beta' || t === 'alpha';
}

function versionTypeBadge(versionType?: string): string {
    const t = (versionType ?? 'release').toLowerCase();
    if (t === 'alpha') return 'Alpha';
    if (t === 'beta') return 'Beta';
    return '';
}

/** 纯正式版本号判断（如 26.3、1.21.4、1.20） */
function isPureReleaseVersion(gameVersion: string): boolean {
    return /^\d+\.\d+(?:\.\d+)?$/.test(gameVersion.trim());
}

/** 取正式版本号的大版本系列（1.21.4 → 1.21；26.3 → 26.3） */
function toMajorSeries(releaseVersion: string): string {
    const match = /^(\d+\.\d+)/.exec(releaseVersion.trim());
    return match ? match[1] : releaseVersion.trim();
}

interface GameVersionBucket {
    /** 顶部药丸筛选标签（如 `26.3`、`26.4 快照版`、`1.20 快照版`、`愚人节版`） */
    majorLabel: string;
    /** 抽屉里显示的版本段（正式版为具体版本如 `1.21.4`，快照版为归属快照如 `1.20 快照版`） */
    groupVersionLabel: string;
    /** 用于排序的基准版本号（如 `26.4`、`1.21.4`、`1.20`） */
    sortVersion: string;
    /** 是否为快照归类（同版本号下正式版排在快照版前） */
    isSnapshot: boolean;
}

/** 将任意 Minecraft 版本号归类为正式版或所属正式版的快照版（如 `1.20 快照版`）。 */
function classifyGameVersion(gameVersion: string): GameVersionBucket {
    const trimmed = gameVersion.trim();
    if (!trimmed) {
        return { majorLabel: '其他', groupVersionLabel: '其他', sortVersion: '', isSnapshot: true };
    }
    if (isAprilFoolsId(trimmed)) {
        return { majorLabel: '愚人节版', groupVersionLabel: '愚人节版', sortVersion: '', isSnapshot: true };
    }
    if (isPureReleaseVersion(trimmed)) {
        return {
            majorLabel: toMajorSeries(trimmed),
            groupVersionLabel: trimmed,
            sortVersion: trimmed,
            isSnapshot: false,
        };
    }
    // 其余视为快照 / 预发布 / 候选版：归类到其目标正式版系列（如 26.4-snapshot-8 → 26.4 快照版，24w14a → 1.20 快照版）
    const targetRelease = resolveSnapshotTargetRelease(trimmed);
    if (targetRelease) {
        const series = toMajorSeries(targetRelease);
        const label = `${series} 快照版`;
        return {
            majorLabel: label,
            groupVersionLabel: label,
            sortVersion: series,
            isSnapshot: true,
        };
    }
    return { majorLabel: '其他快照版', groupVersionLabel: '其他快照版', sortVersion: '', isSnapshot: true };
}

/** 版本号倒序比较（26.4 > 26.3 > 1.21.4 > 1.21 > 1.20.1 > 愚人节版 > 其他） */
function compareVersionDesc(a: string, b: string): number {
    const parse = (value: string): { nums: number[]; isSnapshot: boolean } | null => {
        const match = value.match(/^(\d+)(?:\.(\d+))?(?:\.(\d+))?/);
        if (!match) return null;
        return {
            nums: [
                Number.parseInt(match[1], 10),
                Number.parseInt(match[2] ?? '0', 10),
                Number.parseInt(match[3] ?? '0', 10),
            ],
            isSnapshot: value.includes('快照'),
        };
    };
    const pa = parse(a);
    const pb = parse(b);
    if (pa && pb) {
        for (let i = 0; i < 3; i += 1) {
            if (pa.nums[i] !== pb.nums[i]) return pb.nums[i] - pa.nums[i];
        }
        if (pa.isSnapshot !== pb.isSnapshot) return pa.isSnapshot ? 1 : -1;
        return b.localeCompare(a);
    }
    if (pa) return -1;
    if (pb) return 1;
    return a.localeCompare(b);
}

interface VersionDrawerGroup {
    key: string;
    title: string;
    major: string;
    sortVersion: string;
    isSnapshot: boolean;
    loaderKey: string;
    preview: boolean;
    items: ResourceVersionInfo[];
}

function buildVersionGroups(items: ResourceVersionInfo[]): {
    majors: string[];
    groups: VersionDrawerGroup[];
} {
    const groupMap = new Map<string, VersionDrawerGroup>();
    const majorSet = new Set<string>();

    for (const item of items) {
        const games = item.gameVersions && item.gameVersions.length > 0 ? item.gameVersions : ['其他'];
        const loaders = item.loaders && item.loaders.length > 0 ? item.loaders : [''];
        const preview = isPreviewVersion(item.versionType);

        // 一个模组版本可能兼容多个子版本（如 1.21.4 / 1.21.3 或多个 26.4-snapshot-*），
        // 按每个药丸归类取最高代表版本，既不遗漏大版本也不刷屏重复
        const bestPerMajor = new Map<string, GameVersionBucket>();
        for (const gv of games) {
            const bucket = classifyGameVersion(gv);
            majorSet.add(bucket.majorLabel);
            const prev = bestPerMajor.get(bucket.majorLabel);
            if (!prev || compareVersionDesc(bucket.sortVersion, prev.sortVersion) < 0) {
                bestPerMajor.set(bucket.majorLabel, bucket);
            }
        }

        for (const bucket of bestPerMajor.values()) {
            for (const loader of loaders) {
                const loaderKey = loader.trim().toLowerCase();
                const loaderLabel = loaderKey.length > 0 ? formatLoaderLabel(loaderKey) : '';
                const showPreviewSuffix = preview && !bucket.isSnapshot;
                const title = [loaderLabel, bucket.groupVersionLabel, showPreviewSuffix ? '预览版' : '']
                    .filter((part) => part.length > 0)
                    .join(' ');
                const key = `${loaderKey}|${bucket.groupVersionLabel}|${showPreviewSuffix ? 'preview' : 'release'}`;
                let group = groupMap.get(key);
                if (!group) {
                    group = {
                        key,
                        title,
                        major: bucket.majorLabel,
                        sortVersion: bucket.sortVersion,
                        isSnapshot: bucket.isSnapshot,
                        loaderKey,
                        preview: showPreviewSuffix,
                        items: [],
                    };
                    groupMap.set(key, group);
                }
                if (!group.items.some((existing) => existing.id === item.id)) {
                    group.items.push(item);
                }
            }
        }
    }

    const majors = [...majorSet].sort(compareVersionDesc);
    const groups = [...groupMap.values()].sort((a, b) => {
        const gameDiff = compareVersionDesc(
            a.isSnapshot ? `${a.sortVersion} 快照版` : a.sortVersion,
            b.isSnapshot ? `${b.sortVersion} 快照版` : b.sortVersion,
        );
        if (gameDiff !== 0) return gameDiff;
        if (a.preview !== b.preview) return a.preview ? 1 : -1;
        const rankA = LOADER_RANK[a.loaderKey] ?? 99;
        const rankB = LOADER_RANK[b.loaderKey] ?? 99;
        if (rankA !== rankB) return rankA - rankB;
        return a.title.localeCompare(b.title);
    });

    return { majors, groups };
}

/** 内容详情页：已选实例时仅显示兼容版本；未选实例时按游戏版本与加载器分类展示（PCL2 风格）。 */
export default function ContentDetail({
    item,
    categoryLabel,
    scopedInstance,
    versions,
    versionsLoading,
    versionsExiting = false,
    versionsHint,
    versionId,
    onVersionChange,
    targetMode,
    onTargetModeChange,
    localOnly,
    onLoadVersions,
    onDownload,
    onBack,
}: ContentDetailProps) {
    const [selectedMajor, setSelectedMajor] = useState<string>('all');
    const [openGroups, setOpenGroups] = useState<Record<string, boolean>>({});

    const { majors, groups } = useMemo(
        () => buildVersionGroups(versions?.items ?? []),
        [versions?.items],
    );

    const visibleGroups = useMemo(
        () => (selectedMajor === 'all' ? groups : groups.filter((group) => group.major === selectedMajor)),
        [groups, selectedMajor],
    );

    // 切换模组或重新加载版本时重置筛选并默认展开第一个抽屉
    useEffect(() => {
        setSelectedMajor('all');
        if (groups.length > 0) {
            setOpenGroups({ [groups[0].key]: true });
        } else {
            setOpenGroups({});
        }
    }, [item.id, versions?.requestId, groups.length > 0 ? groups[0].key : '']);

    // 切换大版本药丸时默认展开该筛选下的第一个抽屉
    useEffect(() => {
        if (visibleGroups.length === 0) return;
        const firstKey = visibleGroups[0].key;
        setOpenGroups((current) => {
            if (visibleGroups.some((group) => current[group.key])) return current;
            return { ...current, [firstKey]: true };
        });
    }, [selectedMajor, visibleGroups]);

    const toggleGroup = (key: string) => {
        setOpenGroups((current) => ({ ...current, [key]: !current[key] }));
    };

    return (
        <section className="dl-detail">
            <button type="button" className="vd-back" onClick={onBack}>
                <SvgIcon src={chevronLeftIcon} />
                {categoryLabel}
            </button>

            <div className="dl-detail-head">
                <div className="dl-icon big" aria-hidden="true">
                    {item.icon ? <img src={item.icon} alt="" /> : <span>{item.name.slice(0, 1)}</span>}
                </div>
                <div className="dl-detail-meta">
                    <h2 className="dl-detail-title">{item.name}</h2>
                    <p className="muted">
                        {[
                            item.author.length > 0 ? `by ${item.author}` : '',
                            item.id.toUpperCase(),
                            item.downloads.length > 0 ? `${item.downloads} 次下载` : '',
                        ]
                            .filter((value) => value.length > 0)
                            .join(' · ')}
                    </p>
                </div>
            </div>

            <p className="dl-detail-desc">{item.description}</p>

            {item.tags.length > 0 ? (
                <div className="dl-tags">
                    {item.tags.map((tag) => {
                        const icon = getTagIcon(tag);
                        return (
                            <span key={tag} className="dl-tag">
                                {icon ? (
                                    <span className="dl-tag-icon" style={icon.color ? { color: icon.color } : undefined}>
                                        <SvgIcon src={icon.src} />
                                    </span>
                                ) : null}
                                {formatTagLabel(tag)}
                            </span>
                        );
                    })}
                </div>
            ) : null}

            <div className="dl-detail-status-row">
                <p className="muted">{versionsHint}</p>
                {!versionsLoading ? (
                    <button type="button" className="btn link" onClick={onLoadVersions}>
                        刷新版本
                    </button>
                ) : null}
            </div>

            {versions && versions.items.length > 0 ? (
                scopedInstance ? (
                    /* 已选择具体实例：直接展示兼容该实例的版本列表 */
                    <div className={`version-list tall${versionsExiting ? ' is-exiting' : ''}`}>
                        {versions.items.map((version, index) => {
                            const badge = versionTypeBadge(version.versionType);
                            const waveStyle = { '--wave-index': Math.min(index, 12) } as CSSProperties;
                            return (
                                <button
                                    key={version.id}
                                    type="button"
                                    style={waveStyle}
                                    className={`dl-ver-row dl-wave-item${version.id === versionId ? ' is-selected' : ''}`}
                                    onClick={() => onVersionChange(version.id)}
                                >
                                    <span className="dl-ver-row-main">
                                        <span className="dl-ver-row-name">{version.name}</span>
                                        <span className="dl-ver-row-file">{version.files.join(', ')}</span>
                                    </span>
                                    {badge ? <span className="badge amber">{badge}</span> : null}
                                </button>
                            );
                        })}
                    </div>
                ) : (
                    /* 未选择具体实例：PCL2 风格大版本药丸条 + 按「加载器 + 游戏版本」分类的折叠抽屉 */
                    <div className="dl-ver-explorer">
                        {majors.length > 0 ? (
                            <div className="dl-ver-filter-bar" role="tablist" aria-label="按游戏版本筛选">
                                <button
                                    type="button"
                                    role="tab"
                                    aria-selected={selectedMajor === 'all'}
                                    className={`dl-ver-pill${selectedMajor === 'all' ? ' is-active' : ''}`}
                                    onClick={() => setSelectedMajor('all')}
                                >
                                    全部
                                </button>
                                {majors.map((major) => (
                                    <button
                                        key={major}
                                        type="button"
                                        role="tab"
                                        aria-selected={selectedMajor === major}
                                        className={`dl-ver-pill${selectedMajor === major ? ' is-active' : ''}`}
                                        onClick={() => setSelectedMajor(major)}
                                    >
                                        {major}
                                    </button>
                                ))}
                            </div>
                        ) : null}

                        <div key={selectedMajor} className={`dl-ver-groups${versionsExiting ? ' is-exiting' : ''}`}>
                            {visibleGroups.map((group, groupIndex) => {
                                const isOpen = !!openGroups[group.key];
                                const groupWaveStyle = { '--wave-index': Math.min(groupIndex, 10) } as CSSProperties;
                                const loaderIcon = getLoaderIcon(group.loaderKey);
                                return (
                                    <div
                                        key={group.key}
                                        style={groupWaveStyle}
                                        className={`dl-ver-group dl-wave-item${isOpen ? ' is-open' : ''}`}
                                    >
                                        <button
                                            type="button"
                                            className="dl-ver-group-head"
                                            aria-expanded={isOpen}
                                            onClick={() => toggleGroup(group.key)}
                                        >
                                            {loaderIcon ? (
                                                <span
                                                    className="dl-ver-group-icon"
                                                    style={loaderIcon.color ? { color: loaderIcon.color } : undefined}
                                                >
                                                    <SvgIcon src={loaderIcon.src} />
                                                </span>
                                            ) : null}
                                            <span className="dl-ver-group-title">{group.title}</span>
                                            <span className="dl-ver-group-count">{group.items.length}</span>
                                            <SvgIcon src={chevronDownIcon} className="dl-ver-group-caret" />
                                        </button>
                                        <div className={`dl-ver-collapse${isOpen ? ' is-open' : ''}`}>
                                            <div className="dl-ver-collapse-inner">
                                                <div className="dl-ver-group-body">
                                                    {group.items.map((version) => {
                                                        const badge = versionTypeBadge(version.versionType);
                                                        return (
                                                            <button
                                                                key={version.id}
                                                                type="button"
                                                                tabIndex={isOpen ? 0 : -1}
                                                                className={`dl-ver-row${version.id === versionId ? ' is-selected' : ''}`}
                                                                onClick={() => onVersionChange(version.id)}
                                                            >
                                                                <span className="dl-ver-row-main">
                                                                    <span className="dl-ver-row-name">{version.name}</span>
                                                                    <span className="dl-ver-row-file">
                                                                        {version.files.join(', ')}
                                                                    </span>
                                                                </span>
                                                                {badge ? <span className="badge amber">{badge}</span> : null}
                                                            </button>
                                                        );
                                                    })}
                                                </div>
                                            </div>
                                        </div>
                                    </div>
                                );
                            })}
                        </div>
                    </div>
                )
            ) : null}

            <div className="dl-detail-actions">
                <AppSelect
                    style={{ width: 180 }}
                    value={localOnly ? 'local' : targetMode}
                    onChange={(event) => onTargetModeChange(event.target.value)}
                >
                    {localOnly ? (
                        <option value="local">下载到目录</option>
                    ) : (
                        <>
                            <option value="instance">安装到所选实例</option>
                            <option value="local">下载到目录</option>
                        </>
                    )}
                </AppSelect>
                <button
                    type="button"
                    className="btn primary"
                    disabled={!versions || versions.items.length === 0 || versionId.length === 0}
                    onClick={onDownload}
                >
                    开始下载
                </button>
                <button type="button" className="btn ghost" onClick={onBack}>
                    返回列表
                </button>
            </div>
        </section>
    );
}
