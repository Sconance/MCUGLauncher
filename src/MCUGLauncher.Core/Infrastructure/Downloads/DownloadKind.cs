namespace BlockLaunch.Downloads;

/// <summary>
/// 下载资源的类型，用于选择候选源与日志标记。
/// </summary>
public enum DownloadKind
{
    /// <summary>
    /// 游戏本体文件。
    /// </summary>
    Game,
    /// <summary>
    /// 依赖库。
    /// </summary>
    Library,
    /// <summary>
    /// 资源文件（声音、语言、贴图）。
    /// </summary>
    Asset,
    /// <summary>
    /// 加载器或安装器。
    /// </summary>
    Loader,
    /// <summary>
    /// 模组。
    /// </summary>
    Mod,
    /// <summary>
    /// 整合包。
    /// </summary>
    Pack,
    /// <summary>
    /// 启动器自身的更新文件。
    /// </summary>
    Launcher,
    /// <summary>
    /// 用户指定的任意文件。
    /// </summary>
    Custom
}
