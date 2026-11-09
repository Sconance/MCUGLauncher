using Avalonia.Media;
using MCUGLauncher.Contracts;

namespace MCUGLauncher.Avalonia.Views;

/// <summary>
/// 实例下拉/列表的一行：左侧是加载器图标字符，右侧上行实例名、下行“游戏版本 - 加载器 加载器版本”。
/// </summary>
/// <param name="Instance">对应 <see cref="InstanceInfo.Name"/>；空串表示“不绑定”之类的占位项</param>
/// <param name="Title">上行文字</param>
/// <param name="Subtitle">下行文字</param>
/// <param name="Glyph">图标占位字符</param>
/// <param name="Accent">图标底色</param>
public sealed record InstanceListItem(string Instance, string Title, string Subtitle, string Glyph, IBrush Accent)
{
    static readonly IBrush VanillaBrush = Brush.Parse("#8A96A0");
    static readonly IBrush FabricBrush = Brush.Parse("#B8863B");
    static readonly IBrush ForgeBrush = Brush.Parse("#A5483F");
    static readonly IBrush NeoForgeBrush = Brush.Parse("#C2762A");
    static readonly IBrush QuiltBrush = Brush.Parse("#7B5AC8");
    static readonly IBrush OptiFineBrush = Brush.Parse("#2F8F87");

    /// <summary>下行没有内容时整行隐藏，避免留一条空行。</summary>
    public bool HasSubtitle => Subtitle.Length > 0;

    /// <summary>“不绑定实例”占位项。</summary>
    public static InstanceListItem None
    {
        get;
    } = new("", "不绑定实例", "打开前需要先选一个实例", "–", VanillaBrush);

    /// <summary>
    /// 把实例转成展示行。下行按 PCL2 的描述格式拼成「版本类型 版本号，加载器 加载器版本」，
    /// 例如「正式版 1.20.1，Forge 47.4.10」；没有加载器时连逗号一起省略。
    /// </summary>
    public static InstanceListItem From(InstanceInfo info)
    {
        var loader = info.Loader;
        var vanilla = loader.Length == 0 || loader == "原版";
        var title = info.DisplayName.Length > 0 ? info.DisplayName : info.Name;
        var version = info.Minecraft.Trim();
        var type = info.VersionType.Trim();
        var head = type.Length > 0 && version.Length > 0
            ? type + " " + version
            : type.Length > 0
                ? type
                : version;
        var loaderText = vanilla
            ? ""
            : info.LoaderVersion.Length > 0
                ? loader + " " + info.LoaderVersion
                : loader;
        var subtitle = head.Length > 0 && loaderText.Length > 0
            ? head + "，" + loaderText
            : head.Length > 0
                ? head
                : loaderText;
        var (glyph, accent) = Style(loader);
        return new InstanceListItem(info.Name, title, subtitle, glyph, accent);
    }

    /// <summary>空列表提示行。</summary>
    public static InstanceListItem Empty(string title, string subtitle) => new("", title, subtitle, "·", VanillaBrush);

    /// <summary>绑定过但本机已不存在的实例。</summary>
    public static InstanceListItem Missing(string name) => new(name, name, "当前未安装", "!", ForgeBrush);

    static (string Glyph, IBrush Accent) Style(string loader) => loader switch
    {
        "Fabric" => ("✦", FabricBrush),
        "Forge" => ("✚", ForgeBrush),
        "NeoForge" => ("✱", NeoForgeBrush),
        "Quilt" => ("◆", QuiltBrush),
        "OptiFine" => ("◈", OptiFineBrush),
        _ => ("◉", VanillaBrush)
    };
}
