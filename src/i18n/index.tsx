import { createContext, useCallback, useContext, useEffect, useMemo, useState, type ReactNode } from 'react';
import { enUS } from './messages/en-US';
import { zhCN } from './messages/zh-CN';

/** 语言代码，例如 zh-CN / en-US；外部语言包可以带自己的代码（en-GB、zh-TW…） */
export type Locale = string;

/**
 * 语言包：内置包与用户导入的包结构一致，像资源包一样可以叠加。
 * `messages` 的嵌套结构不做强约束，缺 key 时回退到简体中文。
 */
export interface LanguagePack {
    code: Locale;
    /** 语言对自己的称呼，例如 简体中文 / English；缺省回落到 name */
    nativeName?: string;
    /** 地区名，同样用该语言自己写，例如 中国大陆 / United States */
    regionName?: string;
    /** 兜底显示名，旧语言包可能只带这个字段 */
    name?: string;
    author?: string;
    version?: string;
    messages: Record<string, unknown>;
}

const LOCALE_KEY = 'mcug.locale';
const PACKS_KEY = 'mcug.languagePacks';

/** 内置语言包：不可删除，也是所有外部包的兜底 */
const BUILTIN_PACKS: LanguagePack[] = [
    {
        code: 'zh-CN',
        nativeName: '简体中文',
        regionName: '中国大陆',
        author: 'MCUG',
        messages: zhCN as unknown as Record<string, unknown>,
    },
    {
        code: 'en-US',
        nativeName: 'English',
        regionName: 'United States',
        author: 'MCUG',
        messages: enUS as unknown as Record<string, unknown>,
    },
];

const BUILTIN_CODES = BUILTIN_PACKS.map((pack) => pack.code);

type Vars = Record<string, string | number>;

function lookup(messages: Record<string, unknown>, key: string): string | undefined {
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
    return template.replace(/\{(\w+)\}/g, (match, name: string) => (name in vars ? String(vars[name]) : match));
}

function isRecord(value: unknown): value is Record<string, unknown> {
    return Boolean(value) && typeof value === 'object' && !Array.isArray(value);
}

/**
 * 校验并解析一个语言包文件。
 * 允许 `name` 缺省（用 code 顶上），但 code 必须是 BCP 47 的粗略形式，messages 必须是非空对象。
 */
export function parseLanguagePack(raw: string): LanguagePack {
    let data: unknown;
    try {
        data = JSON.parse(raw);
    } catch {
        throw new Error('不是合法的 JSON 文件。');
    }
    if (!isRecord(data)) throw new Error('语言包必须是一个 JSON 对象。');

    const code = typeof data.code === 'string' ? data.code.trim() : '';
    if (!/^[a-z]{2,3}(-[A-Za-z0-9]{2,8})*$/.test(code)) {
        throw new Error('缺少合法的 code，例如 zh-TW、en-GB。');
    }
    if (!isRecord(data.messages) || Object.keys(data.messages).length === 0) {
        throw new Error('缺少 messages 对象；至少要包含一条翻译。');
    }
    if (BUILTIN_CODES.includes(code)) {
        throw new Error(`${code} 是内置语言，无需导入。`);
    }

    const name = typeof data.name === 'string' && data.name.trim().length > 0 ? data.name.trim() : code;
    return {
        code,
        nativeName: typeof data.nativeName === 'string' && data.nativeName.trim().length > 0 ? data.nativeName.trim() : name,
        regionName:
            typeof data.regionName === 'string' && data.regionName.trim().length > 0 ? data.regionName.trim() : undefined,
        name,
        author: typeof data.author === 'string' ? data.author : undefined,
        version: typeof data.version === 'string' ? data.version : undefined,
        messages: data.messages,
    };
}

/** 读取已导入的语言包；任何一条坏数据都跳过，不影响其它包 */
function readStoredPacks(): LanguagePack[] {
    if (typeof window === 'undefined') return [];
    const raw = window.localStorage.getItem(PACKS_KEY);
    if (!raw) return [];
    try {
        const parsed = JSON.parse(raw);
        if (!Array.isArray(parsed)) return [];
        return parsed.filter(
            (item): item is LanguagePack =>
                isRecord(item) &&
                typeof item.code === 'string' &&
                typeof item.name === 'string' &&
                isRecord(item.messages) &&
                !BUILTIN_CODES.includes(item.code),
        );
    } catch {
        return [];
    }
}

function writeStoredPacks(packs: LanguagePack[]) {
    window.localStorage.setItem(PACKS_KEY, JSON.stringify(packs));
}

function detectLocale(available: LanguagePack[]): Locale {
    if (typeof window === 'undefined') return 'zh-CN';
    const saved = window.localStorage.getItem(LOCALE_KEY);
    if (saved && available.some((pack) => pack.code === saved)) return saved;
    const preferred = window.navigator.language;
    const exact = available.find((pack) => pack.code.toLowerCase() === preferred.toLowerCase());
    if (exact) return exact.code;
    const base = preferred.split('-')[0].toLowerCase();
    const sameLanguage = available.find((pack) => pack.code.toLowerCase().split('-')[0] === base);
    return sameLanguage?.code ?? 'zh-CN';
}

interface I18nValue {
    locale: Locale;
    setLocale: (locale: Locale) => void;
    /** 取文案：当前语言 → 简体中文 → key 本身 */
    t: (key: string, vars?: Vars) => string;
    /** 可选语言：内置包在前，导入包在后 */
    packs: LanguagePack[];
    /** 下拉里的一行，Minecraft 风格：简体中文 (中国大陆) */
    labelOf: (pack: LanguagePack) => string;
    builtinCodes: string[];
    /** 导入一个语言包文件内容；失败抛 Error，成功则立即切换过去 */
    importPack: (raw: string) => LanguagePack;
    removePack: (code: Locale) => void;
}

const I18nContext = createContext<I18nValue | null>(null);

export function I18nProvider({ children }: { children: ReactNode }) {
    const [external, setExternal] = useState<LanguagePack[]>(readStoredPacks);
    const packs = useMemo(() => [...BUILTIN_PACKS, ...external], [external]);
    const [locale, setLocaleState] = useState<Locale>(() => detectLocale([...BUILTIN_PACKS, ...readStoredPacks()]));

    // 语言包被删掉时，退回中文，避免界面变成一堆 key
    useEffect(() => {
        if (!packs.some((pack) => pack.code === locale)) setLocaleState('zh-CN');
    }, [packs, locale]);

    useEffect(() => {
        document.documentElement.lang = locale;
        window.localStorage.setItem(LOCALE_KEY, locale);
    }, [locale]);

    const bundle = useMemo(() => packs.find((pack) => pack.code === locale)?.messages ?? null, [packs, locale]);
    /** Minecraft 风格：语言名 (地区名)，两边都用该语言自己的写法 */
    const labelOf = useCallback((pack: LanguagePack) => {
        const self = pack.nativeName ?? pack.name ?? pack.code;
        return pack.regionName ? `${self} (${pack.regionName})` : self;
    }, []);

    const t = useCallback(
        (key: string, vars?: Vars) => {
            const text = (bundle ? lookup(bundle, key) : undefined) ?? lookup(zhCN as unknown as Record<string, unknown>, key);
            return text === undefined ? key : interpolate(text, vars);
        },
        [bundle],
    );

    const importPack = useCallback((raw: string) => {
        const pack = parseLanguagePack(raw);
        setExternal((current) => {
            const next = [...current.filter((item) => item.code !== pack.code), pack];
            writeStoredPacks(next);
            return next;
        });
        setLocaleState(pack.code);
        return pack;
    }, []);

    const removePack = useCallback(
        (code: Locale) => {
            setExternal((current) => {
                const next = current.filter((item) => item.code !== code);
                writeStoredPacks(next);
                return next;
            });
            if (locale === code) setLocaleState('zh-CN');
        },
        [locale],
    );

    const value = useMemo<I18nValue>(
        () => ({
            locale,
            setLocale: setLocaleState,
            t,
            packs,
            labelOf,
            builtinCodes: BUILTIN_CODES,
            importPack,
            removePack,
        }),
        [locale, t, packs, labelOf, importPack, removePack],
    );

    return <I18nContext.Provider value={value}>{children}</I18nContext.Provider>;
}

export function useI18n(): I18nValue {
    const value = useContext(I18nContext);
    if (!value) throw new Error('useI18n 必须在 I18nProvider 内使用');
    return value;
}
