using System.Text.Json;
using BlockLaunch;
using static MCUGLauncher.UseCases.ProtocolValues;

namespace MCUGLauncher.UseCases;

internal sealed class BookmarkCommands(LauncherContext context)
{
    ResourceBookmarkStore Store => new(context.Store.DataDirectory);
    public object Snapshot() => Store.Read().Select(b => new { id = b.Project.Id, kind = b.Kind, name = b.Project.Title, description = b.Project.Description });
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
