interface ActiveFiltersProps {
    tags: string[];
    authors: string[];
    onRemoveTag: (tag: string) => void;
    onRemoveAuthor: (author: string) => void;
    onClear: () => void;
}

/** 已选筛选条件的横条：一键清除 + 每个条件可单独去掉。 */
export default function ActiveFilters({ tags, authors, onRemoveTag, onRemoveAuthor, onClear }: ActiveFiltersProps) {
    return (
        <div className="dl-active">
            <button type="button" className="dl-clear" onClick={onClear}>
                <span className="dl-clear-x">×</span>
                清除全部筛选
            </button>
            {tags.map((tag) => (
                <button key={tag} type="button" className="dl-active-chip" onClick={() => onRemoveTag(tag)}>
                    {tag}
                    <span className="dl-active-x">×</span>
                </button>
            ))}
            {authors.map((author) => (
                <button key={author} type="button" className="dl-active-chip" onClick={() => onRemoveAuthor(author)}>
                    {author}
                    <span className="dl-active-x">×</span>
                </button>
            ))}
        </div>
    );
}
