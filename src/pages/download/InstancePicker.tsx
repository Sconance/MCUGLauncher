import cubeIcon from '../../assets/icons/ui/cube.svg';
import checkIcon from '../../assets/icons/ui/check.svg';
import SvgIcon from '../../components/SvgIcon';
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
                                        <SvgIcon src={cubeIcon} />
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
                                        <SvgIcon src={checkIcon} className="dl-instance-check" />
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
