using System.Text.Json;
using MCUGLauncher.Models;

namespace MCUGLauncher.Services;

public sealed class ConfigStore
{
    readonly string _file;
    readonly bool dataDirectoryIsolated;

    public ConfigStore(string? dataDirectory = null)
    {
        dataDirectoryIsolated = dataDirectory != null;
        DataDirectory = dataDirectory ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "MCUGLauncher");
        System.IO.Directory.CreateDirectory(DataDirectory);
        _file = Path.Combine(DataDirectory, "config.json");
    }

    public string DataDirectory
    {
        get;
    }

    public AppConfig Load()
    {
        AppConfig config;
        if (File.Exists(_file))
        {
            try
            {
                config = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(_file)) ?? new AppConfig();
            }
            catch (JsonException)
            {
                File.Move(_file, _file + ".broken-" + DateTime.Now.ToString("yyyyMMddHHmmss"), true);
                config = new AppConfig();
            }
        }
        else
        {
            config = dataDirectoryIsolated ? new AppConfig() : ImportLegacy() ?? new AppConfig();
        }

        if (string.IsNullOrWhiteSpace(config.Root) || !Directory.Exists(config.Root))
            config.Root = FindDefaultRoot();
        config.GameRoots ??= [];
        if (string.IsNullOrWhiteSpace(config.Player))
            config.Player = "Steve";
        if (string.IsNullOrWhiteSpace(config.LastVersion))
            config.LastVersion = FindFirstVersion(config.Root);
        if (string.IsNullOrWhiteSpace(config.LastInstance))
            config.LastInstance = config.LastVersion;
        return config;
    }

    public void Save(AppConfig config)
    {
        config.LegacyPreferences.Root = config.Root;
        config.LegacyPreferences.Player = config.Player;
        config.LegacyPreferences.Java = config.Java;
        config.LegacyPreferences.Ram = config.Ram;
        config.LegacyPreferences.Width = config.Width;
        config.LegacyPreferences.Height = config.Height;
        config.LegacyPreferences.LastVersion = config.LastVersion;
        var temp = _file + ".tmp";
        File.WriteAllText(temp, JsonSerializer.Serialize(config, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(temp, _file, true);
    }

    AppConfig? ImportLegacy()
    {
        var legacy = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "BlockLaunch",
            "settings.json");
        return ReadLegacy(legacy);
    }

    internal static AppConfig? ReadLegacy(string legacy)
    {
        if (!File.Exists(legacy))
            return null;

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(legacy));
            var root = doc.RootElement;
            string Read(string name) => root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
                ? value.GetString() ?? ""
                : "";
            int ReadInt(string name, int fallback) => root.TryGetProperty(name, out var value) && value.TryGetInt32(out var number)
                ? number
                : fallback;

            var config = new AppConfig
            {
                LegacyPreferences = new BlockLaunch.SettingsStore(Path.GetDirectoryName(legacy)!).Load(),
                Root = Read("Root"),
                Player = Read("Player"),
                Java = Read("Java"),
                Ram = ReadInt("Ram", 4096),
                Width = ReadInt("Width", 1280),
                Height = ReadInt("Height", 720),
                LastVersion = Read("LastVersion"),
                SkinFile = Read("OfflineSkin")
            };

            if (root.TryGetProperty("Servers", out var servers) && servers.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in servers.EnumerateArray())
                {
                    if (item.ValueKind != JsonValueKind.Object)
                        continue;
                    var name = item.TryGetProperty("Name", out var n) ? n.GetString() ?? "" : "";
                    var address = item.TryGetProperty("Address", out var a) ? a.GetString() ?? "" : "";
                    if (address.Length > 0)
                        config.Servers.Add(new SavedServer { Name = name, Address = address });
                }
            }

            return config;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    static string FindDefaultRoot()
    {
        var roaming = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), ".minecraft");
        if (Directory.Exists(roaming))
            return roaming;
        var local = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "BlockLaunch",
            "minecraft");
        Directory.CreateDirectory(local);
        return local;
    }

    static string FindFirstVersion(string root)
    {
        var versions = Path.Combine(root, "versions");
        if (!Directory.Exists(versions))
            return "";
        return Directory.EnumerateDirectories(versions)
            .Select(Path.GetFileName)
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .OrderByDescending(name => name)
            .FirstOrDefault() ?? "";
    }
}
