namespace MCUGLauncher.Contracts;

/// <summary>实例的完整定位；名称只在所属游戏根目录内唯一。</summary>
public sealed record InstanceTarget(string Root, string Name);

/// <summary>一个已保存目录的扫描结果；错误不妨碍其他目录返回实例。</summary>
public sealed record GameDirectoryInfo(string Root, bool Available, string Error, IReadOnlyList<InstanceInfo> Instances);
