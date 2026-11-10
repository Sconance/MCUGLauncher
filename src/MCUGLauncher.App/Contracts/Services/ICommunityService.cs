namespace MCUGLauncher.Contracts;

/// <summary>公告、推荐、服务器收藏与书签。</summary>
public interface ICommunityService
{
    event Action<CommunityPayload>? CommunityReceived;
    event Action<CommunityData>? CommunityChanged;

    CommunityData Community
    {
        get;
    }

    Task RefreshCommunityAsync();

    [FeatureOperation]
    Task<SavedInfo> SaveRecommendationsAsync(IReadOnlyList<RecommendationInfo> items) =>
        FeatureRequest.InvokeAsync<SavedInfo>(this, nameof(SaveRecommendationsAsync), new
        {
            items = items.Select(item => item.Raw).ToArray()
        });

    /// <summary>保存服务器收藏；instance 为 null 时保留绑定，空字符串时清除绑定。</summary>
    [FeatureOperation]
    Task<ServerResultInfo> SaveServerAsync(string name, string address, string oldAddress = "", string? instance = null) =>
        FeatureRequest.InvokeAsync<ServerResultInfo>(
            this,
            nameof(SaveServerAsync),
            new
            {
                name,
                address,
                oldAddress,
                instance
            });

    [FeatureOperation]
    Task<SavedInfo> DeleteServerAsync(string address) =>
        FeatureRequest.InvokeAsync<SavedInfo>(this, nameof(DeleteServerAsync), new
        {
            address
        });

    [FeatureOperation]
    Task<ServerPingInfo> PingServerAsync(string address, string id) =>
        FeatureRequest.InvokeAsync<ServerPingInfo>(this, nameof(PingServerAsync), new
        {
            address,
            id
        });

    [FeatureOperation]
    Task<SavedInfo> ToggleBookmarkAsync(string id, string name, string description, string author, string kind) =>
        FeatureRequest.InvokeAsync<SavedInfo>(
            this,
            nameof(ToggleBookmarkAsync),
            new
            {
                id,
                name,
                description,
                author,
                kind
            });

    [FeatureOperation]
    Task<KeyResultInfo> VerifyKeyAsync(string role, string key) =>
        FeatureRequest.InvokeAsync<KeyResultInfo>(this, nameof(VerifyKeyAsync), new
        {
            role,
            key
        });
}
