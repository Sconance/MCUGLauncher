using System.Text.Json;
using BlockLaunch;
using MCUGLauncher.Contracts;
using MCUGLauncher.Models;
using MCUGLauncher.Services;
using static MCUGLauncher.UseCases.ProtocolValues;

namespace MCUGLauncher.UseCases;

// Handles community use cases; no WPF controls or browser access.
internal sealed class CommunityCommands(LauncherContext context)
{
    public async Task<byte[]> DownloadImageAsync(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || uri.Scheme is not ("http" or "https")
            || uri.UserInfo.Length > 0)
            throw new ArgumentException("图片地址无效。");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(context.Token);
        timeout.CancelAfter(TimeSpan.FromSeconds(8));
        using var response = await context.Http.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
        response.EnsureSuccessStatusCode();
        if (response.Content.Headers.ContentLength > 4 * 1024 * 1024)
            throw new InvalidDataException("公告配图不能超过 4 MB。");
        await using var input = await response.Content.ReadAsStreamAsync(timeout.Token);
        using var output = new MemoryStream();
        var buffer = new byte[8192];
        int read;
        while ((read = await input.ReadAsync(buffer, timeout.Token)) > 0)
        {
            if (output.Length + read > 4 * 1024 * 1024)
                throw new InvalidDataException("公告配图不能超过 4 MB。");
            await output.WriteAsync(buffer.AsMemory(0, read), timeout.Token);
        }
        return output.ToArray();
    }

    public async Task SendCommunityAsync(bool notify = false)
    {
        var items = new Dictionary<string, string>();
        string? announcementError = null;
        var successful = true;
        var localRecs = Path.Combine(context.Store.DataDirectory, "recommendations.local.json");
        if (File.Exists(localRecs))
            items["recommendations"] = File.ReadAllText(localRecs);
        var announcementUrl = context.Config.DataSources.Announcements;
        if (string.IsNullOrWhiteSpace(announcementUrl))
        {
            items["announcements"] = "[]";
        }
        else
        {
            var announcementResult = await context.Community.ReadAnnouncementAsync(
                announcementUrl,
                context.Token);
            successful &= announcementResult.Ok;
            if (announcementResult.Ok)
            {
                try
                {
                    var announcement = CommunityService.ParseMarkdownAnnouncement(
                        announcementResult.Markdown,
                        announcementUrl);
                    items["announcements"] = JsonSerializer.Serialize(new[] { announcement }, CommunityJsonOptions);
                }
                catch (InvalidDataException ex)
                {
                    successful = false;
                    announcementError = "公告 Markdown 无效：" + ex.Message;
                }
            }
            else
            {
                announcementError = announcementResult.Error;
            }
        }

        var jobs = new[]
        {
            ("recommendations", context.Config.DataSources.Recommendations),
            ("quotes", context.Config.DataSources.Quotes)
        };
        foreach (var (name, url) in jobs)
        {
            if (items.ContainsKey(name))
                continue;
            var text = string.IsNullOrWhiteSpace(url) ? "[]" : await context.Community.ReadAsync(url, context.Token);
            successful &= text != null;
            items[name] = text ?? "[]";
        }
        if (items.Count > 0)
            context.Send(new
            {
                type = "community",
                payload = items
            });
        if (notify)
            context.Send(new
            {
                type = "communityRefreshDone",
                ok = successful
            });
        if (announcementError is not null)
            context.Send(new
            {
                type = "error",
                message = announcementError
            });
    }

    static readonly JsonSerializerOptions CommunityJsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<ServerPingInfo> PingAsync(string address, string id)
    {
        var result = await ServerPingService.PingAsync(address, context.Token);
        return new(id, result.Online, result.OnlinePlayers, result.MaxPlayers,
            result.Ping, result.Motd, result.Version, result.Error);
    }

    public ServerResultInfo SaveServer(string name, string address, string oldAddress)
    {
        address = address.Trim();
        oldAddress = oldAddress.Trim();
        if (address.Length == 0)
            return new(false, "请填写服务器地址。");
        if (name.Length == 0)
            name = address.Split(':')[0];
        if (!string.IsNullOrWhiteSpace(oldAddress) &&
            !oldAddress.Equals(address, StringComparison.OrdinalIgnoreCase))
            context.Config.Servers.RemoveAll(s => s.Address.Equals(oldAddress, StringComparison.OrdinalIgnoreCase));
        context.Config.Servers.RemoveAll(s => s.Address.Equals(address, StringComparison.OrdinalIgnoreCase));
        context.Config.Servers.Add(new SavedServer { Name = name, Address = address });
        context.Store.Save(context.Config);
        context.RefreshState();
        return new(true, (oldAddress.Length > 0 ? "已更新 " : "已添加 ") + name);
    }

    public SavedInfo DeleteServer(string address)
    {
        context.Config.Servers.RemoveAll(s => s.Address.Equals(address, StringComparison.OrdinalIgnoreCase));
        context.Store.Save(context.Config);
        context.RefreshState();
        return new("已删除服务器 " + address);
    }

    public KeyResultInfo VerifyKey(string role, string key)
    {
        if (string.IsNullOrWhiteSpace(key) || key.Trim().Length < 8)
            return new(role, false, "密钥格式不正确，请向群管理员确认。");
        if (role == "admin")
        {
            if (string.IsNullOrWhiteSpace(context.Config.AdminKey))
            {
                context.Config.AdminKey = key.Trim();
                context.Store.Save(context.Config);
                context.RefreshState();
                return new(role, true, "已设置本机管理员密钥。");
            }
            if (!string.Equals(context.Config.AdminKey, key.Trim(), StringComparison.Ordinal))
                return new(role, false, "管理员密钥不匹配。");
        }
        else
        {
            context.Config.CommunityKey = key.Trim();
        }
        context.Store.Save(context.Config);
        context.RefreshState();
        return new(role, true, "验证成功");
    }

    public SavedInfo SaveRecommendationsLocal(JsonElement root)
    {
        var items = root.TryGetProperty("items", out var value) && value.ValueKind == JsonValueKind.Array
            ? value.GetRawText()
            : "[]";
        var file = Path.Combine(context.Store.DataDirectory, "recommendations.local.json");
        using (var document = JsonDocument.Parse(items))
            AtomicFile.WriteJson(file, document.RootElement);
        context.Send(new
        {
            type = "community",
            payload = new Dictionary<string, string> { ["recommendations"] = items }
        });
        return new("本地内容已更新。");
    }

}
