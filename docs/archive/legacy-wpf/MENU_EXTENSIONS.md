# 增加菜单栏目

在 `src/MCUGLauncher/MenuExtensions.cs` 的 `MenuExtensionsController.ConfigureMenuExtensions` 中注册已实现的页面，无需修改主窗口的导航索引、下载分类判断或账号逻辑。

```csharp
partial void ConfigureMenuExtensions(LauncherMenuRegistry menus)
{
    menus.Register(
        LauncherMenuArea.Download,
        new LauncherMenuPage(
            "community.resources",
            "社区资源",
            context => new CommunityResourcesPage(context.Window))
        {
            Glyph = "◇",
            Order = 20
        });
}
```

示例中的 `CommunityResourcesPage` 需要开发者自行实现，发布程序没有注册空白栏目或假按钮。

| 区域 | 显示位置 |
| --- | --- |
| `Top` | 顶部导航，适合少量短名称入口 |
| `Download` | 下载页左侧分类；内容在右侧显示，保留分类栏 |
| `Settings` | 设置页面中的附加栏目入口 |
| `More` | 顶部“更多”菜单 |
| `Home` | 主页账号和启动操作下方的附加入口 |
| `ResourceDetails` | 资源详情的附加标签，选中后创建内容 |
| `VersionSettings` | 版本设置左侧菜单 |
| `InstanceContent` | 实例内容管理的分类菜单 |

每个区域中的 `Id` 必须唯一。`Title` 是显示文字，`Order` 控制附加栏目之间的顺序。重复 ID 和无效定义会明确报错，不会覆盖已有栏目。注册不改变现有内置菜单的索引和行为。

`Glyph` 是图片缺失时的文字图标；也可以设置 `Icon` 为 WPF `ImageSource`。例如将图片作为项目 `Resource`，通过 `BitmapImage` 和 pack URI 加载。分类图标与新增页面图标均支持图片。

页面工厂每次应返回一个新的 `FrameworkElement`，不能把同一控件挂到多个页面。`context.Window` 提供当前主窗口；版本和实例菜单提供当前 `InstanceId`，资源详情提供当前 `Resource`。详情切换项目时会清除扩展标签的旧内容，避免继续展示上一个项目的数据。

初始化后也可以在 UI 线程调用 `window.MenuPages.Register(...)`。已打开的常驻菜单会同步增加入口；版本/实例临时菜单关闭后会解除订阅，下次打开重新读取注册项。不要从后台线程直接操作注册表或控件。

页面内容创建失败会显示页内错误提示。普通新增页面保留“返回”，下载分类保留左侧导航，不创建额外原生窗口。栏目数量较多时应使用下载分类或“更多”，避免把所有入口塞进顶部导航。

这是源码级扩展接口，不是第三方插件加载机制，不会执行远程下载的脚本。业务功能应继续调用已有服务；新增界面不应重置设置或直接修改用户存档。
