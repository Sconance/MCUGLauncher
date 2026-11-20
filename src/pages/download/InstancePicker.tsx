import CubeIcon from '../../components/CubeIcon';
import Modal from '../../components/Modal';
import type { InstanceInfo } from '../../types/launcher';

interface InstancePickerProps {
    title: string;
    hint: string;
    instances: InstanceInfo[];
    /** 当前筛选用的实例 */
    activeId?: string;
    /** 主页启动实例，会打一个「主页启动」标签 */
    homeId?: string;
    onPick: (id: string) => void;
    onClose: () => void;
}

/** 选择「把内容装到哪个实例」的居中弹层。 */
export default function InstancePicker({
    title,
    hint,
    instances,
    activeId,
    homeId,
    onPick,
    onClose,
}: InstancePickerProps) {
    return (
        <Modal title={title} onClose={onClose} dismissOnOverlay width={420}>
            {(close) => (
                <>
                    <p className="muted dl-instance-hint">{hint}</p>
                    <div className="dl-instance-list">
                        {instances.map((item) => {
                            const active = item.id === activeId;
                            const home = item.id === homeId;
                            return (
                                <button
                                    key={item.id}
                                    type="button"
                                    className={`dl-instance-row${active ? ' on' : ''}`}
                                    onClick={() => close(() => onPick(item.id))}
                                >
                                    <span className="dl-instance-icon" aria-hidden="true">
                                        <CubeIcon />
                                    </span>
                                    <span className="dl-instance-text">
                                        <span className="dl-instance-name">
                                            {item.displayName}
                                            {home ? <span className="dl-instance-tag">主页启动</span> : null}
                                        </span>
                                        <span className="dl-instance-sub">
                                            {item.minecraft} · {item.loader} · {item.mods} 个模组
                                        </span>
                                    </span>
                                    {active ? (
                                        <svg className="dl-instance-check" viewBox="0 0 16 16" aria-hidden="true">
                                            <path
                                                d="m3.5 8.5 3 3 6-6"
                                                fill="none"
                                                stroke="currentColor"
                                                strokeWidth="1.8"
                                                strokeLinecap="round"
                                                strokeLinejoin="round"
                                            />
                                        </svg>
                                    ) : null}
                                </button>
                            );
                        })}
                    </div>
                </>
            )}
        </Modal>
    );
}
