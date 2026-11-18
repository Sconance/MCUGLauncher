import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react';
import { enUS } from './messages/en-US';
import { zhCN } from './messages/zh-CN';

export type Locale = 'zh-CN' | 'en-US';

/** 语言包结构以中文包为准，新增 key 时先加 zh-CN，再补 en-US */
export type Messages = typeof zhCN;

const STORAGE_KEY = 'mcug.locale';

const BUNDLES: Record<Locale, Messages> = {
    'zh-CN': zhCN,
    'en-US': enUS,
};

/** 语言在界面上的显示名，不随当前语言变化 */
export const LOCALE_LABELS: Record<Locale, string> = {
    'zh-CN': '简体中文',
    'en-US': 'English',
};

type Vars = Record<string, string | number>;

function lookup(messages: Messages, key: string): string | undefined {
    const value = key.split('.').reduce<unknown>((node, part) => {
        if (node && typeof node === 'object' && part in (node as Record<string, unknown>)) {
            return (node as Record<string, unknown>)[part];
        }
        return undefined;
    }, messages);
    return typeof value === 'string' ? value : undefined;
}

/** 支持 {name} 占位符；缺变量时保留原样，便于发现漏传 */
function interpolate(template: string, vars?: Vars): string {
    if (!vars) return template;
    return template.replace(/\{(\w+)\}/g, (match, name: string) =>
        name in vars ? String(vars[name]) : match,
    );
}

function detectLocale(): Locale {
    if (typeof window === 'undefined') return 'zh-CN';
    const saved = window.localStorage.getItem(STORAGE_KEY);
    if (saved === 'zh-CN' || saved === 'en-US') return saved;
    return window.navigator.language.toLowerCase().startsWith('zh') ? 'zh-CN' : 'en-US';
}

interface I18nValue {
    locale: Locale;
    setLocale: (locale: Locale) => void;
    /** 取文案：找不到时回退中文，再找不到就返回 key 本身 */
    t: (key: string, vars?: Vars) => string;
}

const I18nContext = createContext<I18nValue | null>(null);

export function I18nProvider({ children }: { children: ReactNode }) {
    const [locale, setLocale] = useState<Locale>(detectLocale);

    useEffect(() => {
        document.documentElement.lang = locale;
        window.localStorage.setItem(STORAGE_KEY, locale);
    }, [locale]);

    const t = useCallback(
        (key: string, vars?: Vars) => {
            const text = lookup(BUNDLES[locale], key) ?? lookup(BUNDLES['zh-CN'], key);
            return text === undefined ? key : interpolate(text, vars);
        },
        [locale],
    );

    const value = useMemo<I18nValue>(() => ({ locale, setLocale, t }), [locale, t]);

    return <I18nContext.Provider value={value}>{children}</I18nContext.Provider>;
}

export function useI18n(): I18nValue {
    const value = useContext(I18nContext);
    if (!value) throw new Error('useI18n 必须在 I18nProvider 内使用');
    return value;
}
