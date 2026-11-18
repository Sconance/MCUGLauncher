import { AppInput } from '../../components/AppInput';
import { useI18n } from '../../i18n';

interface SearchRowProps {
    placeholder: string;
    value: string;
    onChange: (value: string) => void;
    onSubmit: () => void;
}

/** 搜索输入框 + 右侧独立的搜索按钮。 */
export default function SearchRow({ placeholder, value, onChange, onSubmit }: SearchRowProps) {
    const { t } = useI18n();
    return (
        <div className="dl-searchrow">
            <AppInput
                className="dl-search-input"
                placeholder={placeholder}
                value={value}
                onChange={(event) => onChange(event.target.value)}
                onKeyDown={(event) => {
                    if (event.key === 'Enter') onSubmit();
                }}
            />
            <button type="button" className="btn primary" onClick={onSubmit}>
                {t('common.search')}
            </button>
        </div>
    );
}
