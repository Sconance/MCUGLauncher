using MCUGLauncher.Contracts;

namespace MCUGLauncher.Tui.Pages;

/// <summary>服务器收藏的增删改与状态查询；不伪造尚未提供的一键进服接口。</summary>
internal sealed class ServersPage(ILauncherService service, Terminal terminal) : TerminalPage(service, terminal)
{
    public override string Title => "服务器收藏";

    public override async Task ShowAsync()
    {
        await Service.RequestStateAsync();
        var action = IO.Select(Title, ["查看 / 查询 / 编辑 / 删除", "添加服务器"]);
        if (action == 1)
        {
            var name = IO.Read("服务器名称（可留空）：");
            var address = IO.Required("服务器地址，例如 play.example.com:25565：");
            var result = await Service.SaveServerAsync(name, address);
            IO.Write((result.Ok ? "成功：" : "失败：") + result.Message);
        }
        else if (action == 0)
        {
            var servers = State.Servers.ToArray();
            var index = IO.Select("服务器列表", servers.Select(item => $"{item.Name} | {item.Address}").ToArray());
            if (index < 0)
                return;
            var server = servers[index];
            var operation = IO.Select(server.Address, ["查询在线状态", "编辑", "删除"]);
            switch (operation)
            {
                case 0:
                    var ping = await Service.PingServerAsync(server.Address, Guid.NewGuid().ToString("N"));
                    IO.Write(ping.Online ? $"在线：{ping.Players}/{ping.Max} 人 | {ping.Ping} ms | {ping.Version}" : "无法连接：" + ping.Error);
                    IO.Write(ping.Motd);
                    break;
                case 1:
                    var name = IO.Read($"名称（当前 {server.Name}，留空保留）：");
                    var address = IO.Read($"地址（当前 {server.Address}，留空保留）：");
                    var result = await Service.SaveServerAsync(name.Length == 0 ? server.Name : name,
                        address.Length == 0 ? server.Address : address, server.Address);
                    IO.Write((result.Ok ? "成功：" : "失败：") + result.Message);
                    break;
                case 2:
                    if (IO.Confirm($"删除收藏 {server.Address}？"))
                        IO.Write((await Service.DeleteServerAsync(server.Address)).Message);
                    break;
                default:
                    return;
            }
        }
        else
            return;
        IO.Pause();
    }
}
