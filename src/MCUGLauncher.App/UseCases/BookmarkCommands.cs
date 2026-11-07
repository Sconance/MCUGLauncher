using System.Text.Json;
using BlockLaunch;
using MCUGLauncher.Contracts;
using static MCUGLauncher.UseCases.ProtocolValues;

namespace MCUGLauncher.UseCases;

internal sealed class BookmarkCommands(LauncherContext context)
{
    ResourceBookmarkStore Store => new(context.Store.DataDirectory);
    public IReadOnlyList<FavoriteInfo> Snapshot() => Store.Read()
        .Select(item => new FavoriteInfo(item.Project.Id, item.Kind, item.Project.Title, item.Project.Description))
        .ToArray();
    public SavedInfo Toggle(JsonElement root)
    {
        var id = ReadString(root, "id");
        var exists = Store.Read().Any(b => b.Project.Id == id);
        if (exists)
            Store.Remove(id);
        else
            Store.Save(new ContentProject(id, id, ReadString(root, "name"), ReadString(root, "description"), ReadString(root, "author"), 0), ReadString(root, "kind"), "");
        context.RefreshState();
        return new(exists ? "已取消收藏。" : "已收藏。");
    }
}
