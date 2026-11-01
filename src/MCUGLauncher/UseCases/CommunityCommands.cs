using System.Text.Json;
using BlockLaunch;
using MCUGLauncher.Models;
using MCUGLauncher.Services;
using static MCUGLauncher.UseCases.ProtocolValues;

namespace MCUGLauncher.UseCases;

// Handles community use cases; no WPF controls or browser access.
internal sealed class CommunityCommands(LauncherContext context)
{
    public async Task SendCommunityAsync(bool notify = false)
    {
        var items = new Dictionary<string, string>();
        var localAnn = Path.Combine(context.Store.DataDirectory, "announcements.local.json");
        if (File.Exists(localAnn))
            items["announcements"] = File.ReadAllText(localAnn);
        var localRecs = Path.Combine(context.Store.DataDirectory, "recommendations.local.json");
        if (File.Exists(localRecs))
            items["recommendations"] = File.ReadAllText(localRecs);
        var jobs = new[]
        {
            ("announcements", context.Config.DataSources.Announcements),
            ("recommendations", context.Config.DataSources.Recommendations),
            ("quotes", context.Config.DataSources.Quotes)
        };
        foreach (var (name, url) in jobs)
        {
            if (items.ContainsKey(name))
                continue;
            var text = await context.Community.ReadAsync(url, context.Token);
            if (text != null)
                items[name] = text;
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
                ok = items.Count == jobs.Length
            });
    }

    public async Task PingAsync(string address, string id)
    {
        var result = await ServerPingService.PingAsync(address, context.Token);
        context.Send(new
        {
            type = "serverPing",
            id,
            online = result.Online,
            players = result.OnlinePlayers,
            max = result.MaxPlayers,
            ping = result.Ping,
            motd = result.Motd,
            version = result.Version,
            error = result.Error
        });
    }

    public void SaveServer(JsonElement root)
    {
        var name = ReadString(root, "name");
        var address = ReadString(root, "address").Trim();
        var oldAddress = ReadString(root, "oldAddress").Trim();
        if (address.Length == 0)
        {
            context.Send(new
            {
                type = "serverResult",
                ok = false,
                message = "请填写服务器地址。"
            });
            return;
        }
        if (name.Length == 0)
            name = address.Split(':')[0];
        if (!string.IsNullOrWhiteSpace(oldAddress) &&
            !oldAddress.Equals(address, StringComparison.OrdinalIgnoreCase))
            context.Config.Servers.RemoveAll(s => s.Address.Equals(oldAddress, StringComparison.OrdinalIgnoreCase));
        context.Config.Servers.RemoveAll(s => s.Address.Equals(address, StringComparison.OrdinalIgnoreCase));
        context.Config.Servers.Add(new SavedServer { Name = name, Address = address });
        context.Store.Save(context.Config);
        context.Send(new
        {
            type = "serverResult",
            ok = true,
            message = (oldAddress.Length > 0 ? "已更新 " : "已添加 ") + name
        });
        context.RefreshState();
    }

    public void DeleteServer(string address)
    {
        context.Config.Servers.RemoveAll(s => s.Address.Equals(address, StringComparison.OrdinalIgnoreCase));
        context.Store.Save(context.Config);
        context.RefreshState();
    }

    public async Task TestSourceAsync(string name, string url)
    {
        var result = await context.Community.TestAsync(url, context.Token);
        context.Send(new
        {
            type = "testResult",
            name,
            url,
            ok = result.Ok,
            message = result.Text,
            ms = result.Ms
        });
    }

    public void VerifyKey(string role, string key)
    {
        if (string.IsNullOrWhiteSpace(key) || key.Trim().Length < 8)
        {
            context.Send(new
            {
                type = "keyResult",
                role,
                ok = false,
                message = "密钥格式不正确，请向群管理员确认。"
            });
            return;
        }
        if (role == "admin")
        {
            if (string.IsNullOrWhiteSpace(context.Config.AdminKey))
            {
                context.Config.AdminKey = key.Trim();
                context.Store.Save(context.Config);
                context.Send(new
                {
                    type = "keyResult",
                    role,
                    ok = true,
                    message = "已设置本机管理员密钥。"
                });
                context.RefreshState();
                return;
            }
            if (!string.Equals(context.Config.AdminKey, key.Trim(), StringComparison.Ordinal))
            {
                context.Send(new
                {
                    type = "keyResult",
                    role,
                    ok = false,
                    message = "管理员密钥不匹配。"
                });
                return;
            }
        }
        else
        {
            context.Config.CommunityKey = key.Trim();
        }
        context.Store.Save(context.Config);
        context.Send(new
        {
            type = "keyResult",
            role,
            ok = true,
            message = "验证成功"
        });
        context.RefreshState();
    }

    public void SaveCommunityLocal(string fileName, JsonElement root, string property)
    {
        var items = root.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.Array
            ? value.GetRawText()
            : "[]";
        var file = Path.Combine(context.Store.DataDirectory, fileName);
        File.WriteAllText(file, items);
        var key = fileName.StartsWith("announcements", StringComparison.OrdinalIgnoreCase) ? "announcements" : "recommendations";
        context.Send(new
        {
            type = "community",
            payload = new Dictionary<string, string> { [key] = items }
        });
        context.Send(new
        {
            type = "saved",
            message = "本地内容已更新。"
        });
    }

    public async Task PublishAsync(JsonElement root)
    {
        var title = ReadString(root, "title");
        var body = ReadString(root, "body");
        if (title.Length == 0 || body.Length == 0)
        {
            context.Send(new
            {
                type = "publishResult",
                ok = false,
                message = "标题和正文不能为空。"
            });
            return;
        }

        var imageData = ReadString(root, "imageData");
        var record = new
        {
            id = Guid.NewGuid().ToString("N"),
            title,
            body,
            tag = "公告",
            author = context.Config.Player,
            publishedAt = DateTimeOffset.Now.ToString("O"),
            expiresAt = ReadString(root, "expiresAt"),
            imageUrl = imageData.StartsWith("data:image/", StringComparison.Ordinal) ? imageData : ""
        };
        var file = Path.Combine(context.Store.DataDirectory, "announcements.local.json");
        var list = File.Exists(file)
            ? JsonSerializer.Deserialize<List<JsonElement>>(File.ReadAllText(file)) ?? []
            : [];
        list.Add(JsonSerializer.SerializeToElement(record));
        var localJson = JsonSerializer.Serialize(list, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(file, localJson);
        context.Send(new
        {
            type = "community",
            payload = new Dictionary<string, string> { ["announcements"] = localJson }
        });

        if (!string.IsNullOrWhiteSpace(context.Config.WriteEndpoint))
        {
            try
            {
                using var deadline = CancellationTokenSource.CreateLinkedTokenSource(context.Token);
                deadline.CancelAfter(TimeSpan.FromSeconds(8));
                using var response = await context.Http.PostAsync(
                    context.Config.WriteEndpoint,
                    new StringContent(JsonSerializer.Serialize(record), System.Text.Encoding.UTF8, "application/json"), deadline.Token);
                if (!response.IsSuccessStatusCode)
                    throw new IOException($"HTTP {(int)response.StatusCode}");
            }
            catch (Exception ex)
            {
                context.Send(new
                {
                    type = "publishResult",
                    ok = false,
                    message = "已保存到本机，但同步失败：" + ex.Message
                });
                return;
            }
        }

        context.Send(new
        {
            type = "publishResult",
            ok = true,
            pending = string.IsNullOrWhiteSpace(context.Config.WriteEndpoint),
            message = string.IsNullOrWhiteSpace(context.Config.WriteEndpoint) ? "已保存为待发布" : "公告已发布"
        });
    }
}
