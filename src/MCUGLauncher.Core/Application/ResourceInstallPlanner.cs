namespace BlockLaunch;

/// <summary>Freezes the chosen version and target into the existing persistent content queue format.</summary>
public static class ResourceInstallPlanner
{
    /// <summary>
    /// 创建。
    /// </summary>
    public static DownloadRequest Create(ContentVersion version, PackRequirements target, string kind, string directory)
    {
        if (kind is not ("mod" or "resourcepack" or "shader" or "datapack"))
            throw new IOException("该资源类型需要下载后通过整合包导入。");
        if (target.Minecraft.Length == 0)
            throw new IOException("请选择有效的安装目标。");
        if (!version.GameVersions.Contains(target.Minecraft))
            throw new IOException("资源版本不支持目标实例的 Minecraft 版本。");
        var loader = kind == "datapack" ? "datapack" : target.Loader;
        if (kind is "mod" or "datapack" && !version.Loaders.Contains(loader))
            throw new IOException("资源版本不支持目标实例的加载器。");
        if (kind == "mod" && loader is "vanilla" or "optifine")
            throw new IOException("请先给目标实例安装支持模组的加载器。");
        return new DownloadRequest
        {
            Kind = "content",
            Content = version,
            Destination = Path.GetFullPath(directory),
            GameVersion = target.Minecraft,
            Loader = loader,
            ResourceKind = kind
        };
    }
}
