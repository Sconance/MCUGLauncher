using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using MCUGLauncher.Contracts;

namespace MCUGLauncher.Avalonia.Views;

/// <summary>
/// 实例下拉/列表的一行：左侧是加载器图标，右侧上行实例名、下行“版本类型 版本号，加载器 加载器版本”。
/// </summary>
/// <param name="Instance">对应 <see cref="InstanceInfo.Name"/>；空串表示“不绑定”之类的占位项</param>
/// <param name="Title">上行文字</param>
/// <param name="Subtitle">下行文字</param>
/// <param name="Glyph">没有图片时的兜底字符</param>
/// <param name="IconPath">加载器图标的 avares 路径；空串表示只用字符</param>
/// <param name="Accent">兜底字符的底色</param>
public sealed record InstanceListItem(
    string Instance,
    string Title,
    string Subtitle,
    string Glyph,
    string IconPath,
    IBrush Accent)
{
    /// <summary>图标放在 Assets/Loaders 下；找不到文件时自动退回 <see cref="Glyph"/>。</summary>
    const string IconRoot = "avares://MCUGLauncher/Assets/Loaders/";

    static readonly IBrush VanillaBrush = Brush.Parse("#8A96A0");
    static readonly IBrush FabricBrush = Brush.Parse("#B8863B");
    static readonly IBrush ForgeBrush = Brush.Parse("#A5483F");
    static readonly IBrush NeoForgeBrush = Brush.Parse("#C2762A");
    static readonly IBrush QuiltBrush = Brush.Parse("#7B5AC8");
    static readonly IBrush OptiFineBrush = Brush.Parse("#2F8F87");

    // 64×64 的源图缩到 26px 显示，每行都重新解码太浪费，按路径缓存一次
    static readonly Dictionary<string, Bitmap?> IconCache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>下行没有内容时整行隐藏，避免留一条空行。</summary>
    public bool HasSubtitle => Subtitle.Length > 0;

    /// <summary>加载器图标；资源缺失时为 null，此时用 <see cref="Glyph"/> 顶替。</summary>
    public Bitmap? Icon => IconPath.Length == 0 ? null : LoadIcon(IconPath);

    public bool HasIcon => Icon is not null;

    public bool HasGlyph => Icon is null;

    /// <summary>“不绑定实例”占位项。</summary>
    public static InstanceListItem None
    {
        get;
    } = new("", "不绑定实例", "打开前需要先选一个实例", "–", "", VanillaBrush);

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
        var (glyph, icon, accent) = Style(loader);
        return new InstanceListItem(info.Name, title, subtitle, glyph, icon, accent);
    }

    /// <summary>空列表提示行。</summary>
    public static InstanceListItem Empty(string title, string subtitle) =>
        new("", title, subtitle, "·", "", VanillaBrush);

    /// <summary>绑定过但本机已不存在的实例。</summary>
    public static InstanceListItem Missing(string name) =>
        new(name, name, "当前未安装", "!", "", ForgeBrush);

    /// <summary>
    /// 图标取自 PCL2 的 Images/Blocks（方块风格）：草方块=原版、铁砧=Forge、NeoForge、Fabric、草径=OptiFine。
    /// Quilt 没有独立图标（PCL2 里归入 Fabric），继续用字符，避免张冠李戴。
    /// </summary>
    static (string Glyph, string Icon, IBrush Accent) Style(string loader) => loader switch
    {
        "Fabric" => ("✦", IconRoot + "Fabric.png", FabricBrush),
        "Forge" => ("✚", IconRoot + "Forge.png", ForgeBrush),
        "NeoForge" => ("✱", IconRoot + "NeoForge.png", NeoForgeBrush),
        "Quilt" => ("◆", "", QuiltBrush),
        "OptiFine" => ("◈", IconRoot + "OptiFine.png", OptiFineBrush),
        _ => ("◉", IconRoot + "Vanilla.png", VanillaBrush)
    };

    static Bitmap? LoadIcon(string path)
    {
        if (IconCache.TryGetValue(path, out var cached))
            return cached;

        Bitmap? bitmap = null;
        try
        {
            using var stream = AssetLoader.Open(new Uri(path));
            bitmap = new Bitmap(stream);
        }
        catch (Exception)
        {
            // 资源缺失不该让整行渲染失败，退回字符图标即可
        }

        IconCache[path] = bitmap;
        return bitmap;
    }
}
