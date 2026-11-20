/**
 * 中文语言包：所有 key 以这里为准，英文包必须实现同样的结构。
 * 命名按「域.子域.名称」，域对应页面或组件，避免一个巨大的扁平表。
 */
export const zhCN = {
    app: {
        name: 'MCUG Launcher',
    },
    nav: {
        home: '启动',
        discover: '发现',
        download: '下载',
        settings: '设置',
        account: '账户',
        main: '主导航',
    },
    common: {
        ok: '确定',
        cancel: '取消',
        back: '返回',
        close: '关闭',
        search: '搜索',
        loading: '加载中…',
    },
    settings: {
        language: '界面语言',
        languageHint: '切换后立即生效，下次启动沿用。',
        languagePackHint: '语言包像资源包一样：导入一个 .json 文件就能新增显示语言。',
        languageImport: '导入语言包…',
        languageRemove: '删除',
        languageImported: '已导入 {name}（{code}）。',
    },
    download: {
        title: '下载',
        sections: {
            vanilla: '原版游戏',
            mod: '模组',
            modpack: '整合包',
            resourcepack: '资源包',
            shader: '光影包',
            datapack: '数据包',
            loader: '加载器',
            pack: '整合包导入',
        },
        searchPlaceholder: '搜索{sections}……',
        refresh: '刷新列表',
        searchVersionPlaceholder: '搜索 Minecraft 版本',
        list: '列表',
        pickInstanceTitle: '选择安装{sections}的实例',
        instanceHint: {
            mod: '只显示与该实例的游戏版本、加载器匹配的模组。',
            shader: '光影包按实例区分 Iris / OptiFine 来源。',
            other: '资源包与数据包与加载器无关，不按实例筛选。',
        },
    },
};
