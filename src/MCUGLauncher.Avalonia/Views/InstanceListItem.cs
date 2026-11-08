using Avalonia.Media;
using MCUGLauncher.Contracts;

namespace MCUGLauncher.Avalonia.Views;

/// <summary>
/// 实例下拉/列表的一行：左侧是加载器图标字符，右侧上行实例名、下行“版本 · 加载器”。
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

    /// <summary>把实例转成展示行；加载器为“原版”时不写进下行，版本与名称相同时也不重复写。</summary>
    public static InstanceListItem From(InstanceInfo info)
    {
        var loader = info.Loader;
        var vanilla = loader.Length == 0 || loader == "原版";
        var title = info.DisplayName.Length > 0 ? info.DisplayName : info.Name;
        // 部分实例的“版本”就是文件夹名，和名称重复时不再写第二遍
        var version = info.Minecraft.Length > 0 &&
                      !info.Minecraft.Equals(title, StringComparison.OrdinalIgnoreCase) &&
                      !info.Minecraft.Equals(info.Name, StringComparison.OrdinalIgnoreCase)
            ? info.Minecraft
            : "";
        var subtitle = version.Length > 0 && !vanilla
            ? version + " · " + loader
            : version.Length > 0
                ? version
                : vanilla
                    ? ""
                    : loader;
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
