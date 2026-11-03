using System.Text.Json;
using MCUGLauncher;
using static MCUGLauncher.UseCases.ProtocolValues;

namespace MCUGLauncher.UseCases;

internal sealed class BookmarkCommands(LauncherContext context)
{
    ResourceBookmarkStore Store => new(context.Store.DataDirectory);
    public Contracts.ResourceInfo[] Snapshot() => Store.Read().Select(b => new Contracts.ResourceInfo { Id = b.Project.Id, Kind = b.Kind, Name = b.Project.Title, Description = b.Project.Description, Author = b.Project.Author, Icon = b.Project.IconUrl, Downloads = b.Project.Downloads }).ToArray();

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
