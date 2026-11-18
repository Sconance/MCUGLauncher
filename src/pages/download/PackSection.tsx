import Field from '../../components/Field';
import { AppInput } from '../../components/AppInput';
import type { PackPreviewInfo } from '../../types/launcher';

interface PackSectionProps {
    pack: PackPreviewInfo | null;
    name: string;
    onNameChange: (value: string) => void;
    root: string;
    onRootChange: (value: string) => void;
    hint: string;
    onPickFile: () => void;
    onPickLocal: () => void;
    onPickRoot: () => void;
    onConfirm: () => void;
}

/** 整合包导入页：选文件 → 预览 → 填名称与目录 → 确认导入。 */
export default function PackSection({
    pack,
    name,
    onNameChange,
    root,
    onRootChange,
    hint,
    onPickFile,
    onPickLocal,
    onPickRoot,
    onConfirm,
}: PackSectionProps) {
    return (
        <div className="stack" style={{ gap: 10 }}>
            <p className="muted">选择 .mrpack 或 .zip 整合包，先预览再确认导入。</p>
            <button type="button" className="btn ghost" style={{ alignSelf: 'flex-start' }} onClick={onPickFile}>
                选择整合包文件
            </button>
            <p className="muted">也可导入模组、资源包、光影或世界存档；拖入启动器窗口即可检查。</p>
            <button type="button" className="btn ghost" style={{ alignSelf: 'flex-start' }} onClick={onPickLocal}>
                选择本地文件
            </button>

            {pack ? (
                <section className="card stack" style={{ gap: 10 }}>
                    <h2 className="h2">{pack.name}</h2>
                    <p className="muted">
                        {pack.game} · {pack.loader} · 来源目录 {pack.root}
                    </p>
                    <Field label="实例名称">
                        <AppInput
                            placeholder="1–100 个字符"
                            value={name}
                            onChange={(event) => onNameChange(event.target.value)}
                        />
                    </Field>
                    <Field label="安装目录">
                        <div className="row" style={{ gap: 8 }}>
                            <AppInput
                                placeholder="已存在的游戏目录"
                                value={root}
                                onChange={(event) => onRootChange(event.target.value)}
                            />
                            <button type="button" className="btn ghost" onClick={onPickRoot}>
                                选择
                            </button>
                        </div>
                    </Field>
                    <button
                        type="button"
                        className="btn primary"
                        style={{ alignSelf: 'flex-start' }}
                        onClick={onConfirm}
                    >
                        确认导入
                    </button>
                </section>
            ) : null}
            <p className="muted">{hint}</p>
        </div>
    );
}
