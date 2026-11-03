using System.Text.Json;
using MCUGLauncher.Contracts;
using static MCUGLauncher.UseCases.ProtocolValues;

namespace MCUGLauncher.UseCases;

/// <summary>测试社区数据源并直接返回可用性结果。</summary>
/// <remarks>
/// 组件级别：B（依赖现有组件）。
/// 依赖：
/// - <see cref="LauncherContext.Community"/>.<see cref="MCUGLauncher.Services.CommunityService.TestAsync"/>：执行有界 URL 测试；
/// - handler 参数中的 <see cref="CancellationToken"/>：取消测试请求。
/// 不依赖：Views、原生窗口宿主、LauncherService、LauncherApplication。
/// </remarks>
[FeatureHandler(nameof(ILauncherService.TestSourceAsync))]
internal sealed class TestSourceFeatureHandler(LauncherContext context)
    : IFeatureHandler<TestResultInfo>
{
    public async Task<TestResultInfo> HandleAsync(JsonElement arguments, CancellationToken token)
    {
        var name = ReadString(arguments, "name");
        var url = ReadString(arguments, "url");
        var result = await context.Community.TestAsync(url, token);
        return new TestResultInfo(name, url, result.Ok, result.Text, result.Ms);
    }
}
