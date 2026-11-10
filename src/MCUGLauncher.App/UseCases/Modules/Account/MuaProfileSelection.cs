using System.Text.Json;
using BlockLaunch;
using static MCUGLauncher.UseCases.ProtocolValues;

namespace MCUGLauncher.UseCases;

// Holds only a short-lived profile choice, never the password or access token.
internal sealed class MuaProfileSelection(AccountServices services)
{
    string? token;
    IReadOnlyList<MuaProfile>? profiles;
    TaskCompletionSource<MuaProfile?>? completion;

    public async Task<MuaProfile?> ChooseAsync(IReadOnlyList<MuaProfile> items)
    {
        if (items.Count == 1)
            return items[0];
        if (completion != null)
            throw new InvalidOperationException("已有角色选择正在进行。");
        token = Guid.NewGuid().ToString("N");
        profiles = items;
        completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        try
        {
            services.Notifier.Send(new
            {
                type = "muaProfiles",
                token,
                items = items.Select(p => new { p.Id, p.Name })
            });
            return await completion.Task.WaitAsync(TimeSpan.FromMinutes(3), services.Token);
        }
        finally { completion = null; profiles = null; token = null; }
    }

    public void Select(JsonElement message)
    {
        if (completion == null || token != ReadString(message, "token"))
            throw new ArgumentException("角色选择已过期。");
        var id = ReadString(message, "profileId");
        var choice = id.Length == 0 ? null : profiles!.SingleOrDefault(p => p.Id == id) ?? throw new ArgumentException("角色不属于本次登录。");
        completion.TrySetResult(choice);
    }
}
