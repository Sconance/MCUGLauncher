import type { zhCN } from './zh-CN';

/** 英文语言包：结构与中文包完全一致，缺 key 会在编译期报错。 */
export const enUS: typeof zhCN = {
    app: {
        name: 'MCUG Launcher',
    },
    nav: {
        home: 'Launch',
        discover: 'Discover',
        download: 'Download',
        settings: 'Settings',
        account: 'Account',
        main: 'Main navigation',
    },
    common: {
        ok: 'OK',
        cancel: 'Cancel',
        back: 'Back',
        close: 'Close',
        search: 'Search',
        loading: 'Loading…',
    },
    settings: {
        language: 'Language',
        languageHint: 'Applies immediately and is remembered next time.',
        languagePackHint: 'Language packs work like resource packs: import a .json file to add a language.',
        languageImport: 'Import pack…',
        languageRemove: 'Remove',
        languageImported: 'Imported {name} ({code}).',
    },
    download: {
        title: 'Download',
        sections: {
            vanilla: 'Vanilla',
            mod: 'Mods',
            modpack: 'Modpacks',
            resourcepack: 'Resource packs',
            shader: 'Shaders',
            datapack: 'Data packs',
            loader: 'Loaders',
            pack: 'Import modpack',
        },
        searchPlaceholder: 'Search {sections}…',
        refresh: 'Refresh',
        searchVersionPlaceholder: 'Search Minecraft versions',
        list: 'List',
        pickInstanceTitle: 'Choose an instance for {sections}',
        instanceHint: {
            mod: 'Only items matching the instance game version and loader are shown.',
            shader: 'Shaders are matched to the instance Iris / OptiFine source.',
            other: 'Resource and data packs ignore the instance and are not filtered.',
        },
    },
};
