import { AppSelect } from '../../components/AppInput';
import SvgIcon from '../../components/SvgIcon';
import chevronLeftIcon from '../../assets/icons/ui/chevron-left.svg';
import type { ContentItem, ResourceVersionsInfo } from '../../types/launcher';

interface ContentDetailProps {
    item: ContentItem;
    /** 返回按钮上的文字，取当前分区的名称 */
    categoryLabel: string;
    versions: ResourceVersionsInfo | null;
    versionsHint: string;
    versionId: string;
    onVersionChange: (id: string) => void;
    targetMode: string;
    onTargetModeChange: (mode: string) => void;
    /** 整合包 / 数据包只能下载到目录 */
    localOnly: boolean;
    onLoadVersions: () => void;
    onDownload: () => void;
    onBack: () => void;
}

/** 内容详情页：右栏整页替换列表，顶部是返回列表的按钮。 */
export default function ContentDetail({
    item,
    categoryLabel,
    versions,
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
                    {item.tags.map((tag) => (
                        <span key={tag} className="dl-tag">
                            {tag}
                        </span>
                    ))}
                </div>
            ) : null}

            <p className="muted">{versionsHint}</p>

            {!versions ? (
                <button
                    type="button"
                    className="btn primary"
                    style={{ alignSelf: 'flex-start' }}
                    onClick={onLoadVersions}
                >
                    选择版本并安装
                </button>
            ) : null}

            {versions && versions.items.length > 0 ? (
                <>
                    <div className="version-list">
                        {versions.items.map((version) => (
                            <button
                                key={version.id}
                                type="button"
                                className={`list-item ${version.id === versionId ? ' selected' : ''}`}
                                onClick={() => onVersionChange(version.id)}
                            >
                                {version.name} · {version.files.join(', ')}
                            </button>
                        ))}
                    </div>
                    <AppSelect
                        style={{ width: 200 }}
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
                </>
            ) : null}

            <div className="dl-detail-actions">
                <button
                    type="button"
                    className="btn primary"
                    disabled={!versions || versions.items.length === 0}
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
