/** 分页按钮序列：页数多时折叠成 1 … 4 5 6 … 92。 */
function pageNumbers(current: number, total: number): (number | 'gap')[] {
    if (total <= 7) return Array.from({ length: total }, (_, index) => index);
    const pages: (number | 'gap')[] = [0];
    const start = Math.max(1, current - 1);
    const end = Math.min(total - 2, current + 1);
    if (start > 1) pages.push('gap');
    for (let page = start; page <= end; page += 1) pages.push(page);
    if (end < total - 2) pages.push('gap');
    pages.push(total - 1);
    return pages;
}

interface PagerProps {
    page: number;
    pageCount: number;
    onPageChange: (page: number) => void;
}

/** 仿 PCL2 的底部分页条：首页 / 上一页 / 数字 / 下一页 / 末页。 */
export default function Pager({ page, pageCount, onPageChange }: PagerProps) {
    return (
        <div className="dl-pager-bar">
            <button
                type="button"
                className="dl-pg"
                disabled={page === 0}
                aria-label="第一页"
                onClick={() => onPageChange(0)}
            >
                «
            </button>
            <button
                type="button"
                className="dl-pg"
                disabled={page === 0}
                aria-label="上一页"
                onClick={() => onPageChange(Math.max(0, page - 1))}
            >
                ‹
            </button>
            {pageNumbers(page, pageCount).map((entry, index) =>
                entry === 'gap' ? (
                    <span key={`gap-${index}`} className="dl-pg-gap">
                        …
                    </span>
                ) : (
                    <button
                        key={entry}
                        type="button"
                        className={`dl-pg${entry === page ? ' on' : ''}`}
                        onClick={() => onPageChange(entry)}
                    >
                        {entry + 1}
                    </button>
                )
            )}
            <button
                type="button"
                className="dl-pg"
                disabled={page + 1 >= pageCount}
                aria-label="下一页"
                onClick={() => onPageChange(Math.min(pageCount - 1, page + 1))}
            >
                ›
            </button>
            <button
                type="button"
                className="dl-pg"
                disabled={page + 1 >= pageCount}
                aria-label="最后一页"
                onClick={() => onPageChange(pageCount - 1)}
            >
                »
            </button>
        </div>
    );
}
