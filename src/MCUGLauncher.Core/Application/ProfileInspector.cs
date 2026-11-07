using System.Text.Json;

namespace BlockLaunch;

/// <summary>Reads installed metadata, not display names, so renamed profiles retain their loader identity.</summary>
public static class ProfileInspector
{
    /// <summary>
    /// 读取。
    /// </summary>
    public static PackRequirements Read(string root, string profile)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var loader = "vanilla";
        var loaderVersion = "";
        string Visit(string id)
        {
            ContentService.SafeFile(id);
            if (!seen.Add(id) || seen.Count > 16)
                throw new IOException("版本继承关系无效或存在循环。");
            using var doc = JsonDocument.Parse(File.ReadAllText(Path.Combine(root, "versions", id, id + ".json")));
            var json = doc.RootElement;
            void Found(string kind, string version)
            {
                // Child metadata takes priority; Quilt's Fabric compatibility library is not its loader.
                if (loader == "vanilla" || loader == "fabric" && kind == "quilt")
                {
                    loader = kind;
                    loaderVersion = version;
                }
            }

            string Argument(string flag)
            {
                if (!json.TryGetProperty("arguments", out var args) || !args.TryGetProperty("game", out var game) || game.ValueKind != JsonValueKind.Array)
                    return "";
                var tokens = game.EnumerateArray().Where(x => x.ValueKind == JsonValueKind.String).Select(x => x.GetString() ?? "").ToArray();
                for (var i = 0; i < tokens.Length; i++)
                {
                    if (tokens[i] == flag && i + 1 < tokens.Length)
                        return tokens[i + 1];
                    if (tokens[i].StartsWith(flag + "=", StringComparison.Ordinal))
                        return tokens[i][(flag.Length + 1)..];
                }

                return "";
            }

            var neo = Argument("--fml.neoForgeVersion");
            if (neo.Length > 0)
                Found("neoforge", neo);
            var neoMetadata = false;
            if (json.TryGetProperty("libraries", out var libraries) && libraries.ValueKind == JsonValueKind.Array)
                foreach (var library in libraries.EnumerateArray())
                {
                    if (!library.TryGetProperty("name", out var name) || name.ValueKind != JsonValueKind.String)
                        continue;
                    var parts = (name.GetString() ?? "").Split(':');
                    if (parts.Length < 3)
                        continue;
                    if (parts[0].StartsWith("net.neoforged.", StringComparison.Ordinal))
                        neoMetadata = true;
                    var kind = (parts[0], parts[1]) switch
                    {
                        ("net.neoforged", "neoforge" or "forge") => "neoforge",
                        ("net.minecraftforge", "forge") => "forge",
                        ("net.fabricmc", "fabric-loader") => "fabric",
                        ("org.quiltmc", "quilt-loader") => "quilt",
                        ("optifine", "OptiFine") => "optifine",
                        _ => ""
                    };
                    if (kind.Length > 0)
                        Found(kind, parts[2]);
                }

            if (loader == "vanilla" && neoMetadata)
                Found("neoforge", Argument("--fml.forgeVersion"));
            if (loader == "vanilla")
            {
                var forge = Argument("--fml.forgeVersion");
                if (forge.Length > 0)
                    Found("forge", forge);
            }

            if (json.TryGetProperty("inheritsFrom", out var parent) && parent.ValueKind == JsonValueKind.String)
                return Visit(parent.GetString()!);
            foreach (var property in new[] { "clientVersion", "minecraftVersion", "jar" })
                if (json.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String && value.GetString() is { Length: > 0 } version)
                {
                    ContentService.SafeFile(version);
                    return version;
                }
            if (loader == "optifine" && loaderVersion.Contains('_'))
                return loaderVersion.Split('_')[0];
            var baseVersion = Argument("--fml.mcVersion");
            if (baseVersion.Length > 0)
            {
                ContentService.SafeFile(baseVersion);
                return baseVersion;
            }

            return id;
        }

        var minecraft = Visit(profile);
        if (loader is "forge" or "neoforge" && loaderVersion.StartsWith(minecraft + "-", StringComparison.Ordinal))
            loaderVersion = loaderVersion[(minecraft.Length + 1)..];
        if (loader == "optifine" && loaderVersion.StartsWith(minecraft + "_", StringComparison.Ordinal))
            loaderVersion = loaderVersion[(minecraft.Length + 1)..];
        return new(minecraft, loader, loaderVersion);
    }
}
