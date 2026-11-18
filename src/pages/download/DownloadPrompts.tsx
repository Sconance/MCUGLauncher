import PathPrompt from '../../components/PathPrompt';

export type PromptKind = 'packFile' | 'localImport' | 'packRoot' | 'download';

interface DownloadPromptsProps {
    prompt: PromptKind | null;
    onCancel: () => void;
    /** 已经填过的安装目录，为空时回落到默认游戏目录 */
    packRoot: string;
    defaultRoot: string;
    onPickPackFile: (path: string) => void;
    onPickLocalFile: (path: string) => void;
    onPickPackRoot: (path: string) => void;
    onPickDownloadDir: (path: string) => void;
}

/** 下载页的四个路径输入弹层：选整合包、选本地文件、选安装目录、选下载目录。 */
export default function DownloadPrompts({
    prompt,
    onCancel,
    packRoot,
    defaultRoot,
    onPickPackFile,
    onPickLocalFile,
    onPickPackRoot,
    onPickDownloadDir,
}: DownloadPromptsProps) {
    return (
        <>
            {prompt === 'packFile' ? (
                <PathPrompt
                    title="选择整合包"
                    label="整合包文件路径"
                    initial="D:/Downloads/example.mrpack"
                    hint="支持 .mrpack 与 .zip。"
                    onCancel={onCancel}
                    onConfirm={onPickPackFile}
                />
            ) : null}

            {prompt === 'localImport' ? (
                <PathPrompt
                    title="选择本地文件"
                    label="文件路径"
                    initial="D:/Downloads/example.jar"
                    hint="支持模组、资源包、光影、存档与整合包。"
                    onCancel={onCancel}
                    onConfirm={onPickLocalFile}
                />
            ) : null}

            {prompt === 'packRoot' ? (
                <PathPrompt
                    title="选择安装目录"
                    label="游戏目录"
                    initial={packRoot.length > 0 ? packRoot : defaultRoot}
                    hint="必须是已存在的游戏目录。"
                    onCancel={onCancel}
                    onConfirm={onPickPackRoot}
                />
            ) : null}

            {prompt === 'download' ? (
                <PathPrompt
                    title="选择下载目录"
                    label="保存位置"
                    initial="D:/Downloads"
                    hint="资源将下载到该目录。"
                    onCancel={onCancel}
                    onConfirm={onPickDownloadDir}
                />
            ) : null}
        </>
    );
}
