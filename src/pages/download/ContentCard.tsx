import downloadIcon from '../../assets/icons/ui/download.svg';
import SvgIcon from '../../components/SvgIcon';
import type { ContentItem } from '../../types/launcher';

interface ContentCardProps {
    item: ContentItem;
    bookmarked: boolean;
    onOpen: () => void;
    onToggleBookmark: () => void;
}

/** 列表 / 网格里的一张资源卡：整卡可点进详情，收藏按钮单独拦事件。 */
export default function ContentCard({ item, bookmarked, onOpen, onToggleBookmark }: ContentCardProps) {
    return (
        <article
            className="dl-card"
            role="button"
            tabIndex={0}
            onClick={onOpen}
            onKeyDown={(event) => {
                if (event.key === 'Enter' || event.key === ' ') {
                    event.preventDefault();
                    onOpen();
                }
            }}
        >
            <div className="dl-icon" aria-hidden="true">
                {item.icon ? <img src={item.icon} alt="" /> : <span>{item.name.slice(0, 1)}</span>}
            </div>

            <div className="dl-card-main">
                <h3 className="dl-card-title">
                    <button
                        type="button"
                        className="dl-link"
                        onClick={(event) => {
                            event.stopPropagation();
                            onOpen();
                        }}
                    >
                        {item.name}
                    </button>
                    {item.author.length > 0 ? <span className="dl-by">by {item.author}</span> : null}
                </h3>
                <p className="dl-desc">{item.description}</p>
                {item.tags.length > 0 ? (
                    <div className="dl-tags">
                        {item.tags.slice(0, 3).map((tag) => (
                            <span key={tag} className="dl-tag">
                                {tag}
                            </span>
                        ))}
                        {item.tags.length > 3 ? <span className="dl-tag more">+{item.tags.length - 3}</span> : null}
                    </div>
                ) : null}
            </div>

            <div className="dl-stats">
                <span className="dl-stat" title="下载量">
                    <SvgIcon src={downloadIcon} />
                    {item.downloads.length > 0 ? item.downloads : '—'}
                </span>
                <button
                    type="button"
                    className={`dl-fav${bookmarked ? ' on' : ''}`}
                    onClick={(event) => {
                        event.stopPropagation();
                        onToggleBookmark();
                    }}
                >
                    {bookmarked ? '已收藏' : '收藏'}
                </button>
                <button
                    type="button"
                    className="dl-more"
                    onClick={(event) => {
                        event.stopPropagation();
                        onOpen();
                    }}
                >
                    详情
                </button>
            </div>
        </article>
    );
}
