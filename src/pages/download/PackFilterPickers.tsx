import type { RefObject } from 'react';
import chevronDownIcon from '../../assets/icons/ui/chevron-down.svg';
import SvgIcon from '../../components/SvgIcon';

/** 整合包右上角「加载器选择」的可选项，空字符串表示不限 */
const PACK_LOADERS: [string, string][] = [
    ['', '全部加载器'],
    ['forge', 'Forge'],
    ['neoforge', 'NeoForge'],
    ['fabric', 'Fabric'],
    ['quilt', 'Quilt'],
];

interface PackFilterPickersProps {
    game: string;
    loader: string;
    gameOpen: boolean;
    loaderOpen: boolean;
    onToggleGame: () => void;
    onToggleLoader: () => void;
    onPickGame: (value: string) => void;
    onPickLoader: (value: string) => void;
    /** 可选的正式版版本号，从新到旧 */
    versions: string[];
    rootRef: RefObject<HTMLDivElement | null>;
}

/** 整合包的版本 / 加载器两个下拉按钮：下拉互斥，选完立刻重搜。 */
export default function PackFilterPickers({
    game,
    loader,
    gameOpen,
    loaderOpen,
    onToggleGame,
    onToggleLoader,
    onPickGame,
    onPickLoader,
    versions,
    rootRef,
}: PackFilterPickersProps) {
    const loaderLabel = PACK_LOADERS.find(([value]) => value === loader)?.[1] ?? loader;
    return (
        <div className="dl-pickers" ref={rootRef}>
            <div className="dl-picker-wrap">
                <button
                    type="button"
                    className={`dl-picker${game.length > 0 ? ' has-value' : ''}${gameOpen ? ' on' : ''}`}
                    aria-expanded={gameOpen}
                    onClick={onToggleGame}
                >
                    <span className="dl-picker-label">版本</span>
                    <span className="dl-picker-value">{game.length > 0 ? game : '全部'}</span>
                    <SvgIcon src={chevronDownIcon} className="dl-picker-caret" />
                </button>
                {gameOpen ? (
                    <div className="dl-picker-pop">
                        <button
                            type="button"
                            className={`dl-picker-item${game.length === 0 ? ' on' : ''}`}
                            onClick={() => onPickGame('')}
                        >
                            全部版本
                        </button>
                        {versions.map((version) => (
                            <button
                                key={version}
                                type="button"
                                className={`dl-picker-item${game === version ? ' on' : ''}`}
                                onClick={() => onPickGame(version)}
                            >
                                {version}
                            </button>
                        ))}
                    </div>
                ) : null}
            </div>

            <div className="dl-picker-wrap">
                <button
                    type="button"
                    className={`dl-picker${loader.length > 0 ? ' has-value' : ''}${loaderOpen ? ' on' : ''}`}
                    aria-expanded={loaderOpen}
                    onClick={onToggleLoader}
                >
                    <span className="dl-picker-label">加载器</span>
                    <span className="dl-picker-value">{loader.length > 0 ? loaderLabel : '全部'}</span>
                    <SvgIcon src={chevronDownIcon} className="dl-picker-caret" />
                </button>
                {loaderOpen ? (
                    <div className="dl-picker-pop">
                        {PACK_LOADERS.map(([value, label]) => (
                            <button
                                key={value}
                                type="button"
                                className={`dl-picker-item${loader === value ? ' on' : ''}`}
                                onClick={() => onPickLoader(value)}
                            >
                                {label}
                            </button>
                        ))}
                    </div>
                ) : null}
            </div>
        </div>
    );
}
