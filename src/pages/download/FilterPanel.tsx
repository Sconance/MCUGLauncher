import type { RefObject } from 'react';
import banIcon from '../../assets/icons/ui/ban.svg';
import checkIcon from '../../assets/icons/ui/check.svg';
import filterIcon from '../../assets/icons/ui/filter.svg';
import { AppInput } from '../../components/AppInput';
import SvgIcon from '../../components/SvgIcon';
import { usePresence } from '../../shell/usePresence';
import { formatTagLabel, getTagIcon } from './modIcons';

interface FilterRowGroupProps {
    title: string;
    entries: [string, number][];
    included: string[];
    excluded: string[];
    onToggleInclude: (value: string) => void;
    onToggleExclude: (value: string) => void;
    withIcons?: boolean;
}

/** Modrinth 风格的筛选行组：一行一个，支持点击选中（包含）与右侧禁止按钮（排除）。 */
function FilterRowGroup({
    title,
    entries,
    included,
    excluded,
    onToggleInclude,
    onToggleExclude,
    withIcons,
}: FilterRowGroupProps) {
    return (
        <div className="dl-group">
            <span className="dl-group-title">{title}</span>
            <div className="dl-filter-rows">
                {entries.map(([value, count]) => {
                    const icon = withIcons ? getTagIcon(value) : undefined;
                    const isIncluded = included.includes(value);
                    const isExcluded = excluded.includes(value);
                    const label = formatTagLabel(value);
                    return (
                        <div
                            key={value}
                            className={`dl-filter-row${isIncluded ? ' is-included' : ''}${isExcluded ? ' is-excluded' : ''}`}
                        >
                            <button
                                type="button"
                                className="dl-filter-row-main"
                                aria-pressed={isIncluded}
                                onClick={() => onToggleInclude(value)}
                            >
                                {icon ? (
                                    <span
                                        className="dl-filter-row-icon"
                                        style={!isExcluded && icon.color ? { color: icon.color } : undefined}
                                    >
                                        <SvgIcon src={icon.src} />
                                    </span>
                                ) : null}
                                <span className="dl-filter-row-label">{label}</span>
                                <span className="dl-filter-row-count">{count}</span>
                                {isIncluded ? (
                                    <span className="dl-filter-row-check" aria-hidden="true">
                                        <SvgIcon src={checkIcon} />
                                    </span>
                                ) : null}
                            </button>
                            <button
                                type="button"
                                className="dl-filter-row-exclude"
                                aria-label={isExcluded ? `取消排除 ${label}` : `排除 ${label}`}
                                aria-pressed={isExcluded}
                                title={isExcluded ? `取消排除 ${label}` : `排除 ${label}`}
                                onClick={() => onToggleExclude(value)}
                            >
                                <SvgIcon src={banIcon} />
                            </button>
                        </div>
                    );
                })}
            </div>
        </div>
    );
}

interface FilterPanelProps {
    open: boolean;
    onToggle: () => void;
    onClose: () => void;
    /** 已选条件数量（包含 + 排除），>0 时按钮右上角显示小圆点 */
    activeCount: number;
    railQuery: string;
    onRailQueryChange: (value: string) => void;
    envTags: [string, number][];
    loaderTags: [string, number][];
    kindTags: [string, number][];
    activeTags: string[];
    excludedTags: string[];
    onToggleTag: (tag: string) => void;
    onToggleExcludeTag: (tag: string) => void;
    onClear: () => void;
    rootRef: RefObject<HTMLDivElement | null>;
}

/** 工具栏最右侧的筛选按钮与二级面板（Modrinth 风格：一行一个 + 滚动条 + 包含/排除）。 */
export default function FilterPanel({
    open,
    onToggle,
    onClose,
    activeCount,
    railQuery,
    onRailQueryChange,
    envTags,
    loaderTags,
    kindTags,
    activeTags,
    excludedTags,
    onToggleTag,
    onToggleExcludeTag,
    onClear,
    rootRef,
}: FilterPanelProps) {
    const { mounted, closing } = usePresence(open, 140);

    return (
        <div className="dl-filter-wrap" ref={rootRef}>
            <button
                type="button"
                className={`dl-icon-btn${open ? ' on' : ''}`}
                aria-label="筛选"
                aria-expanded={open}
                onClick={onToggle}
            >
                <SvgIcon src={filterIcon} />
                {activeCount > 0 ? <span className="dl-filter-dot" /> : null}
            </button>

            {mounted ? (
                <div className={`dl-filter-pop${closing ? ' is-closing' : ''}`}>
                    <AppInput
                        placeholder="筛选名称、简介"
                        value={railQuery}
                        onChange={(event) => onRailQueryChange(event.target.value)}
                    />

                    <div className="dl-filter-scroll">
                        {envTags.length > 0 ? (
                            <FilterRowGroup
                                title="运行环境"
                                entries={envTags}
                                included={activeTags}
                                excluded={excludedTags}
                                onToggleInclude={onToggleTag}
                                onToggleExclude={onToggleExcludeTag}
                            />
                        ) : null}
                        {loaderTags.length > 0 ? (
                            <FilterRowGroup
                                title="加载器"
                                entries={loaderTags}
                                included={activeTags}
                                excluded={excludedTags}
                                onToggleInclude={onToggleTag}
                                onToggleExclude={onToggleExcludeTag}
                                withIcons
                            />
                        ) : null}
                        {kindTags.length > 0 ? (
                            <FilterRowGroup
                                title="类别"
                                entries={kindTags}
                                included={activeTags}
                                excluded={excludedTags}
                                onToggleInclude={onToggleTag}
                                onToggleExclude={onToggleExcludeTag}
                                withIcons
                            />
                        ) : null}
                    </div>

                    <div className="dl-filter-foot">
                        <button type="button" className="btn ghost" onClick={onClear}>
                            清除筛选
                        </button>
                        <button type="button" className="btn primary" onClick={onClose}>
                            完成
                        </button>
                    </div>
                </div>
            ) : null}
        </div>
    );
}
