import { AppInput } from '../../components/AppInput';
import CubeIcon from '../../components/CubeIcon';
import { LOADER_CONFLICTS, LOADER_DEFS, type LoaderDef } from './loaders';

export interface LoaderRow extends LoaderDef {
    versions: string[];
}

interface VanillaInstallPageProps {
    /** 选中的原版版本号，例如 1.21.1 */
    minecraft: string;
    instanceName: string;
    onInstanceNameChange: (name: string) => void;
    loading: boolean;
    rows: LoaderRow[];
    pickLoader: Record<string, string>;
    openPick: string | null;
    onOpenPickChange: (key: string | null) => void;
    onPickVersion: (key: string, version: string) => void;
    onClearLoader: (key: string) => void;
    onBack: () => void;
}

/** 原版安装页：实例图标 + 名称输入 + 各加载器抽屉。 */
export default function VanillaInstallPage({
    minecraft,
    instanceName,
    onInstanceNameChange,
    loading,
    rows,
    pickLoader,
    openPick,
    onOpenPickChange,
    onPickVersion,
    onClearLoader,
    onBack,
}: VanillaInstallPageProps) {
    return (
        <div className="vd-page">
            <button type="button" className="vd-back" onClick={onBack}>
                <svg viewBox="0 0 16 16" aria-hidden="true">
                    <path
                        d="M10 4 6 8l4 4"
                        fill="none"
                        stroke="currentColor"
                        strokeWidth="1.6"
                        strokeLinecap="round"
                        strokeLinejoin="round"
                    />
                </svg>
                版本列表
            </button>

            <div className="vd-head">
                <div className="vd-icon" aria-hidden="true">
                    <CubeIcon className="vd-cube" />
                </div>
                <AppInput
                    className="vd-name"
                    placeholder="实例名称"
                    value={instanceName}
                    onChange={(event) => onInstanceNameChange(event.target.value)}
                />
            </div>

            <p className="muted">
                {loading ? '正在读取加载器版本…' : `为 ${minecraft} 选择加载器；不选加载器则安装原版。`}
            </p>

            <div className="vn-drawers">
                {rows.map((row) => {
                    const empty = row.versions.length === 0;
                    const open = openPick === row.key;
                    const picked = pickLoader[row.key];
                    // 已选的其它加载器与本行冲突时，本行不可选，必须先取消那个加载器
                    const conflict = LOADER_DEFS.find(
                        (def) => pickLoader[def.key] && LOADER_CONFLICTS[row.key]?.includes(def.key)
                    );
                    const blocked = Boolean(conflict) && !picked;
                    const locked = empty || blocked;
                    return (
                        <section key={row.key} className={`vn-drawer${open ? ' open' : ''}`}>
                            <div
                                className={`vn-head vd-loader-head${empty ? ' is-empty' : ''}${
                                    blocked ? ' is-blocked' : ''
                                }`}
                            >
                                <button
                                    type="button"
                                    className="vn-head-main"
                                    aria-expanded={open}
                                    disabled={locked}
                                    onClick={() => onOpenPickChange(open ? null : row.key)}
                                >
                                    <span className="vn-head-title">{row.label}</span>
                                    <span className={`vd-loader-value${blocked ? ' is-blocked' : ''}`}>
                                        {blocked
                                            ? `与 ${conflict?.label} 不兼容`
                                            : empty
                                              ? '无'
                                              : (picked ?? '选择版本')}
                                    </span>
                                </button>
                                <span className="vd-loader-tail">
                                    {picked ? (
                                        <button
                                            type="button"
                                            className="vd-loader-clear"
                                            aria-label={`取消选择 ${row.label}`}
                                            title={`取消选择 ${row.label}`}
                                            onClick={() => onClearLoader(row.key)}
                                        >
                                            <svg viewBox="0 0 16 16" aria-hidden="true">
                                                <path
                                                    d="M4.5 4.5l7 7M11.5 4.5l-7 7"
                                                    fill="none"
                                                    stroke="currentColor"
                                                    strokeWidth="1.5"
                                                    strokeLinecap="round"
                                                />
                                            </svg>
                                        </button>
                                    ) : null}
                                    {locked ? null : (
                                        <svg className="vn-chevron" viewBox="0 0 16 16" aria-hidden="true">
                                            <path
                                                d="m6 4 4 4-4 4"
                                                fill="none"
                                                stroke="currentColor"
                                                strokeWidth="1.6"
                                                strokeLinecap="round"
                                                strokeLinejoin="round"
                                            />
                                        </svg>
                                    )}
                                </span>
                            </div>

                            {locked ? null : (
                                <div className={`vn-collapse${open ? ' open' : ''}`} inert={!open}>
                                    <div className="vn-collapse-inner">
                                        <div className="vn-body vd-versions">
                                            {row.versions.map((version) => (
                                                <button
                                                    key={version}
                                                    type="button"
                                                    className={`section-item vn-item${
                                                        picked === version ? ' selected' : ''
                                                    }`}
                                                    onClick={() => onPickVersion(row.key, version)}
                                                >
                                                    <span className="section-label">{version}</span>
                                                </button>
                                            ))}
                                        </div>
                                    </div>
                                </div>
                            )}
                        </section>
                    );
                })}
            </div>
        </div>
    );
}
