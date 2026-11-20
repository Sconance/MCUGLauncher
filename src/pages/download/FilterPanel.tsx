import type { RefObject } from 'react';
import filterIcon from '../../assets/icons/ui/filter.svg';
import { AppInput } from '../../components/AppInput';
import SvgIcon from '../../components/SvgIcon';

interface ChipGroupProps {
    title: string;
    entries: [string, number][];
    active: string[];
    onToggle: (value: string) => void;
    /** 作者可能很多，横向滚动 */
    scroll?: boolean;
}

/** 筛选面板里的一组 chip：作者 / 运行环境 / 类别共用。 */
function ChipGroup({ title, entries, active, onToggle, scroll }: ChipGroupProps) {
    return (
        <div className="dl-group">
            <span className="dl-group-title">{title}</span>
            <div className={`dl-chips${scroll ? ' scroll-x' : ''}`}>
                {entries.map(([value, count]) => (
                    <button
                        key={value}
                        type="button"
                        className={`dl-chip${active.includes(value) ? ' on' : ''}`}
                        onClick={() => onToggle(value)}
                    >
                        {value}
                        <span className="dl-chip-count">{count}</span>
                    </button>
                ))}
            </div>
        </div>
    );
}

interface FilterPanelProps {
    open: boolean;
    onToggle: () => void;
    onClose: () => void;
    /** 已选条件数量，>0 时按钮右上角显示小圆点 */
    activeCount: number;
    railQuery: string;
    onRailQueryChange: (value: string) => void;
    authorCounts: [string, number][];
    envTags: [string, number][];
    kindTags: [string, number][];
    activeTags: string[];
    activeAuthors: string[];
    onToggleTag: (tag: string) => void;
    onToggleAuthor: (author: string) => void;
    onClear: () => void;
    rootRef: RefObject<HTMLDivElement | null>;
}

/** 工具栏最右侧的筛选按钮与二级面板。 */
export default function FilterPanel({
    open,
    onToggle,
    onClose,
    activeCount,
    railQuery,
    onRailQueryChange,
    authorCounts,
    envTags,
    kindTags,
    activeTags,
    activeAuthors,
    onToggleTag,
    onToggleAuthor,
    onClear,
    rootRef,
}: FilterPanelProps) {
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

            {open ? (
                <div className="dl-filter-pop">
                    <AppInput
                        placeholder="筛选名称、简介、作者"
                        value={railQuery}
                        onChange={(event) => onRailQueryChange(event.target.value)}
                    />

                    {authorCounts.length > 0 ? (
                        <ChipGroup
                            title="作者"
                            entries={authorCounts}
                            active={activeAuthors}
                            onToggle={onToggleAuthor}
                            scroll
                        />
                    ) : null}
                    {envTags.length > 0 ? (
                        <ChipGroup title="运行环境" entries={envTags} active={activeTags} onToggle={onToggleTag} />
                    ) : null}
                    {kindTags.length > 0 ? (
                        <ChipGroup title="类别" entries={kindTags} active={activeTags} onToggle={onToggleTag} />
                    ) : null}

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
