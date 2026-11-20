import { AppInput } from '../../components/AppInput';
import SvgIcon from '../../components/SvgIcon';
import cubeIcon from '../../assets/icons/ui/cube.svg';
import chevronRightIcon from '../../assets/icons/ui/chevron-right.svg';
import type { VanillaVersionInfo } from '../../types/launcher';
import { useI18n } from '../../i18n';

export interface VersionGroup {
    theme: string;
    items: VanillaVersionInfo[];
}

export interface VersionDrawer {
    key: string;
    label: string;
    items: VanillaVersionInfo[];
    groups: VersionGroup[];
}

interface VanillaVersionListProps {
    query: string;
    onQueryChange: (value: string) => void;
    onRefresh: () => void;
    drawers: VersionDrawer[];
    open: Record<string, boolean>;
    onToggle: (key: string) => void;
    onPick: (item: VanillaVersionInfo) => void;
    hint: string;
}

/** 原版版本列表：三个抽屉（正式版 / 快照版 / 远古版），按 Wiki 主题分组。 */
export default function VanillaVersionList({
    query,
    onQueryChange,
    onRefresh,
    drawers,
    open,
    onToggle,
    onPick,
    hint,
}: VanillaVersionListProps) {
    const { t } = useI18n();
    return (
        <div className="vn-page">
            <div className="vn-toolbar">
                <AppInput
                    className="vn-search"
                    placeholder={t('download.searchVersionPlaceholder')}
                    value={query}
                    onChange={(event) => onQueryChange(event.target.value)}
                />
                <button type="button" className="btn ghost" onClick={onRefresh}>
                    {t('download.refresh')}
                </button>
            </div>

            <div className="vn-drawers">
                {drawers.map((drawer) => {
                    const expanded = open[drawer.key] ?? false;
                    return (
                        <section key={drawer.key} className={`vn-drawer${expanded ? ' open' : ''}`}>
                            <button
                                type="button"
                                className="vn-head"
                                aria-expanded={expanded}
                                onClick={() => onToggle(drawer.key)}
                            >
                                <span className="vn-head-title">{drawer.label}</span>
                                <span className="vn-count">{drawer.items.length}</span>
                                <SvgIcon src={chevronRightIcon} className="vn-chevron" />
                            </button>

                            {/* 抽出/抽回：外层用 0fr→1fr 过渡高度，内层裁剪，收起时禁止聚焦 */}
                            <div className={`vn-collapse${expanded ? ' open' : ''}`} inert={!expanded}>
                                <div className="vn-collapse-inner">
                                    <div className="vn-body">
                                        {drawer.groups.length === 0 ? <p className="muted">没有匹配的版本。</p> : null}
                                        {drawer.groups.map((group) => (
                                            <div className="vn-group" key={group.theme}>
                                                <h4 className="vn-group-title">{group.theme}</h4>
                                                <div className="section-group">
                                                    {group.items.map((item) => (
                                                        <button
                                                            key={item.id}
                                                            type="button"
                                                            className="section-item vn-item"
                                                            onClick={() => onPick(item)}
                                                        >
                                                            <SvgIcon src={cubeIcon} className="section-icon" />
                                                            <span className="section-label">{item.id}</span>
                                                            <span className="vn-date">{item.released}</span>
                                                        </button>
                                                    ))}
                                                </div>
                                            </div>
                                        ))}
                                    </div>
                                </div>
                            </div>
                        </section>
                    );
                })}
            </div>

            <p className="muted">{hint}</p>
        </div>
    );
}
