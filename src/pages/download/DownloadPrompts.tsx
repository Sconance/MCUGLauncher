import PathPrompt from '../../components/PathPrompt';

export type PromptKind = 'download';

interface DownloadPromptsProps {
    prompt: PromptKind | null;
    onCancel: () => void;
    onPickDownloadDir: (path: string) => void;
}

/** 下载页的路径输入弹层：选下载目录。 */
export default function DownloadPrompts({ prompt, onCancel, onPickDownloadDir }: DownloadPromptsProps) {
    return (
        <>
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
