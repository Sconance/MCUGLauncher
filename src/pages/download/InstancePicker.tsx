import cubeIcon from '../../assets/icons/ui/cube.svg';
import checkIcon from '../../assets/icons/ui/check.svg';
import SvgIcon from '../../components/SvgIcon';
import Modal from '../../components/Modal';
import type { InstanceInfo } from '../../types/launcher';

interface InstancePickerProps {
    title: string;
    hint: string;
    instances: InstanceInfo[];
    /** 当前筛选用的实例；空串或 undefined 表示「不选择实例」 */
    activeId?: string;
    /** 主页启动实例，会打一个「主页启动」标签 */
    homeId?: string;
    /** 是否在第一项提供「不选择实例」选项 */
    allowNone?: boolean;
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
    allowNone = false,
    onPick,
    onClose,
}: InstancePickerProps) {
    const noneActive = !activeId;

    return (
        <Modal title={title} onClose={onClose} dismissOnOverlay width={420}>
            {(close) => (
                <>
                    <p className="muted dl-instance-hint">{hint}</p>
                    <div className="dl-instance-list">
                        {allowNone ? (
                            <button
                                type="button"
                                className={`dl-instance-row${noneActive ? ' on' : ''}`}
                                onClick={() => close(() => onPick(''))}
                            >
                                <span className="dl-instance-icon" aria-hidden="true">
                                    <SvgIcon src={cubeIcon} />
                                </span>
                                <span className="dl-instance-text">
                                    <span className="dl-instance-name">不选择实例</span>
                                    <span className="dl-instance-sub">
                                        不限版本与加载器，在详情页按版本和加载器分类浏览
                                    </span>
                                </span>
                                {noneActive ? (
                                    <SvgIcon src={checkIcon} className="dl-instance-check" />
                                ) : null}
                            </button>
                        ) : null}

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
