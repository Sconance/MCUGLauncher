import banIcon from '../../assets/icons/ui/ban.svg';
import SvgIcon from '../../components/SvgIcon';
import { formatTagLabel } from './modIcons';

interface ActiveFiltersProps {
    tags: string[];
    excludedTags: string[];
    onRemoveTag: (tag: string) => void;
    onRemoveExcludedTag: (tag: string) => void;
    onClear: () => void;
}

/** 已选筛选条件的横条：一键清除 + 每个包含/排除条件可单独去掉。 */
export default function ActiveFilters({
    tags,
    excludedTags,
    onRemoveTag,
    onRemoveExcludedTag,
    onClear,
}: ActiveFiltersProps) {
    return (
        <div className="dl-active">
            <button type="button" className="dl-clear" onClick={onClear}>
                <span className="dl-clear-x">×</span>
                清除全部筛选
            </button>
            {tags.map((tag) => (
                <button key={tag} type="button" className="dl-active-chip" onClick={() => onRemoveTag(tag)}>
                    {formatTagLabel(tag)}
                    <span className="dl-active-x">×</span>
                </button>
            ))}
            {excludedTags.map((tag) => (
                <button
                    key={`ex-${tag}`}
                    type="button"
                    className="dl-active-chip is-excluded"
                    onClick={() => onRemoveExcludedTag(tag)}
                >
                    <SvgIcon src={banIcon} className="dl-active-ban" />
                    {formatTagLabel(tag)}
                    <span className="dl-active-x">×</span>
                </button>
            ))}
        </div>
    );
}
