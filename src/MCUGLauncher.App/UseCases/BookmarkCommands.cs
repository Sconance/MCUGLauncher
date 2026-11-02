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
    public void Toggle(JsonElement root)
    {
        var id = ReadString(root, "id");
        if (Store.Read().Any(b => b.Project.Id == id))
            Store.Remove(id);
        else
            Store.Save(new ContentProject(id, id, ReadString(root, "name"), ReadString(root, "description"), ReadString(root, "author"), 0), ReadString(root, "kind"), "");
        context.RefreshState();
    }
}
