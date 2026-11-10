using System.IO;
using System.Text.Json;
using MCUGLauncher.Contracts;
using MCUGLauncher.Services;

/// <summary>
/// 契约层回归：后端消息里缺字段、或把数字写成 null 时，解析必须降级而不是抛异常。
/// 启动器曾因为任务列表里的 percent:null 在 UI 线程抛出未捕获异常而直接退出。
/// </summary>
internal static class ContractScenarios
{
    public static void Run(string fixture)
    {
        using (var tasks = JsonDocument.Parse(
                   """
                   {"type":"tasks","items":[
                     {"id":"queued","name":"排队任务","state":"Queued","percent":null,"message":"","committing":false},
                     {"id":"running","name":"下载任务","state":"Running","percent":42.5,"message":"下载中","committing":true},
                     {"id":"sparse","name":"缺字段任务","state":"Preparing"}
                   ]}
                   """))
        {
            var items = JsonRead.Items(tasks.RootElement, "items", TaskInfo.From);
            Check(items.Count == 3
                  && items[0].Percent == 0
                  && items[1].Percent == 42.5
                  && items[2].Percent == 0
                  && items[2].Message.Length == 0
                  && !items[2].Committing,
                "tasks payload tolerates null and missing numeric fields");
        }

        using (var wrongKind = JsonDocument.Parse("""{"type":"tasks","items":[{"id":"x","percent":"50"}]}"""))
        {
            var items = JsonRead.Items(wrongKind.RootElement, "items", TaskInfo.From);
            Check(items.Count == 1 && items[0].Percent == 0, "tasks payload tolerates a non-numeric percent");
        }

        var configDirectory = Path.Combine(fixture, "null-number-config");
        Directory.CreateDirectory(configDirectory);
        // 旧版配置被手工改坏（ram/width/height 为 null）时，加载不能失败。
        File.WriteAllText(Path.Combine(configDirectory, "settings.json"),
            """
            {"player":"NullNumber","root":"","ram":null,"width":null,"height":null}
            """);
        var store = new ConfigStore(configDirectory);
        store.Save(new MCUGLauncher.Models.AppConfig { Player = "NotNull" });
        var loaded = store.Load();
        Check(loaded.Player == "NotNull" && loaded.Ram > 0, "config loading ignores null numeric fields");

        var legacyDirectory = Path.Combine(fixture, "null-number-legacy");
        Directory.CreateDirectory(legacyDirectory);
        File.WriteAllText(Path.Combine(legacyDirectory, "settings.json"),
            """
            {"Player":"LegacyNull","Ram":null,"Width":null,"Height":null,"SkinFile":null}
            """);
        var migrated = ConfigStore.ReadLegacy(Path.Combine(legacyDirectory, "settings.json"));
        Check(migrated is not null && migrated.Player == "LegacyNull", "legacy config migration tolerates null numeric fields");
    }

    static void Check(bool success, string label)
    {
        if (!success)
            throw new Exception(label);
        Console.WriteLine("PASS " + label);
    }
}
