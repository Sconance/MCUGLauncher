import { AppSelect } from '../../components/AppInput';

const LOADERS = [
    ['fabric', 'Fabric'],
    ['quilt', 'Quilt'],
    ['forge', 'Forge'],
    ['neoforge', 'NeoForge'],
] as const;

interface LoaderSectionProps {
    games: string[];
    game: string;
    onGameChange: (value: string) => void;
    type: string;
    onTypeChange: (value: string) => void;
    versions: string[];
    version: string;
    onVersionChange: (value: string) => void;
    hint: string;
    onRefreshVersions: () => void;
    onLoadVersions: () => void;
    onInstall: () => void;
}

/** 单独的加载器安装页：选游戏版本 → 选加载器 → 读版本 → 安装。 */
export default function LoaderSection({
    games,
    game,
    onGameChange,
    type,
    onTypeChange,
    versions,
    version,
    onVersionChange,
    hint,
    onRefreshVersions,
    onLoadVersions,
    onInstall,
}: LoaderSectionProps) {
    return (
        <div className="stack" style={{ gap: 10 }}>
            <p className="muted">为指定 Minecraft 版本安装 Fabric、Quilt、Forge 或 NeoForge。</p>
            <div className="row" style={{ gap: 10 }}>
                <span>游戏版本</span>
                <AppSelect value={game} onChange={(event) => onGameChange(event.target.value)}>
                    {games.map((entry) => (
                        <option key={entry} value={entry}>
                            {entry}
                        </option>
                    ))}
                </AppSelect>
                <button type="button" className="btn ghost" onClick={onRefreshVersions}>
                    读取版本
                </button>
            </div>
            <div className="row" style={{ gap: 10 }}>
                <span>加载器</span>
                <AppSelect value={type} onChange={(event) => onTypeChange(event.target.value)}>
                    {LOADERS.map(([value, label]) => (
                        <option key={value} value={value}>
                            {label}
                        </option>
                    ))}
                </AppSelect>
                <button type="button" className="btn ghost" onClick={onLoadVersions}>
                    读取加载器版本
                </button>
            </div>
            <div className="row" style={{ gap: 10 }}>
                <span>加载器版本</span>
                <AppSelect value={version} onChange={(event) => onVersionChange(event.target.value)}>
                    {versions.length === 0 ? <option value="">尚未读取</option> : null}
                    {versions.map((entry) => (
                        <option key={entry} value={entry}>
                            {entry}
                        </option>
                    ))}
                </AppSelect>
            </div>
            <button type="button" className="btn primary" style={{ alignSelf: 'flex-start' }} onClick={onInstall}>
                安装加载器
            </button>
            <p className="muted">{hint}</p>
        </div>
    );
}
