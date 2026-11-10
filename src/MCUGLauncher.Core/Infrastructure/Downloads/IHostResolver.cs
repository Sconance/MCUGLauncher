using System.Net;

namespace BlockLaunch.Downloads;

/// <summary>
/// DNS 兜底的可插拔入口：解析失败时由实现决定改用哪个地址。
/// </summary>
/// <remarks>
/// 真正让请求“换地址重连”必须接管 <see cref="SocketsHttpHandler.ConnectCallback"/>：HttpClient 仍按原主机名做 TLS/SNI，
/// 只是把 TCP 连接指向备用 IP，所以不能靠改写 URI 实现（会破坏证书校验与镜像策略）。
/// 这会带来一个新的 HttpClient 实例，与本项目“全局单例 HttpClient + 限速/计量处理器链”的约定冲突，
/// 因此默认只保留接口与开关（<see cref="DownloadOptions.EnableDnsFallback"/>），不改变现有传输链路。
/// 后续若要接入，只需在创建 HttpClient 的位置装配一个使用 <see cref="IHostResolver"/> 的 ConnectCallback。
/// </remarks>
public interface IHostResolver
{
    /// <summary>
    /// 解析主机名对应的全部地址，按优先级排序。
    /// </summary>
    ValueTask<IReadOnlyList<IPAddress>> ResolveAsync(string host, CancellationToken token);
}

/// <summary>
/// 基于系统 DNS 的默认解析器。
/// </summary>
public sealed class SystemHostResolver : IHostResolver
{
    /// <inheritdoc />
    public async ValueTask<IReadOnlyList<IPAddress>> ResolveAsync(string host, CancellationToken token)
    {
        var addresses = await Dns.GetHostAddressesAsync(host, token).ConfigureAwait(false);
        return addresses;
    }
}
