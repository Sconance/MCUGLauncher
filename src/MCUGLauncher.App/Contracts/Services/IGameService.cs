namespace MCUGLauncher.Contracts;

/// <summary>运行中游戏与存档备份。</summary>
public interface IGameService
{
    event Action<IReadOnlyList<GameInfo>>? GamesChanged;

    IReadOnlyList<GameInfo> Games
    {
        get;
    }
    IReadOnlyList<SaveBackupInfo> SaveBackups
    {
        get;
    }

    Task RequestGamesAsync();
    Task StopGameAsync(string id);

    [FeatureOperation]
    Task<SavedInfo> BackupSavesAsync() =>
        FeatureRequest.InvokeAsync<SavedInfo>(this, nameof(BackupSavesAsync), new
        {
        });

    [FeatureOperation]
    Task<IReadOnlyList<SaveBackupInfo>> ListSaveBackupsAsync() =>
        FeatureRequest.InvokeAsync<IReadOnlyList<SaveBackupInfo>>(this, nameof(ListSaveBackupsAsync), new
        {
        });

    [FeatureOperation]
    Task<SavedInfo> RestoreSaveAsync(string id, string world) =>
        FeatureRequest.InvokeAsync<SavedInfo>(this, nameof(RestoreSaveAsync), new
        {
            id,
            world
        });
}
