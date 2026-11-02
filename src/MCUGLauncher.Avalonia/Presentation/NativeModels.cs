using System.Text.Json;
using MCUGLauncher.Models;

namespace MCUGLauncher.Avalonia.Presentation;

// Typed transport boundary. Views never parse JSON or hold application services.
internal sealed record NativeItem
{
    public string Id { get; init; } = "";
    public string Name { get; init; } = "";
    public string DisplayName { get; init; } = "";
    public string Loader { get; init; } = "";
    public string Minecraft { get; init; } = "";
    public string Description { get; init; } = "";
    public string Desc { get; init; } = "";
    public string Author { get; init; } = "";
    public string File { get; init; } = "";
    public string Path { get; init; } = "";
    public string V { get; init; } = "";
    public string Version { get; init; } = "";
    public string Player { get; init; } = "";
    public string State { get; init; } = "";
    public string Message { get; init; } = "";
    public string Kind { get; init; } = "";
    public string Address { get; init; } = "";
    public string Released { get; init; } = "";
    public string Created { get; init; } = "";
    public string Before { get; init; } = "";
    public string After { get; init; } = "";
    public string Error { get; init; } = "";
    public bool Enabled
    {
        get; init;
    }
    public bool Available
    {
        get; init;
    }
    public bool Committing
    {
        get; init;
    }
    public double? Percent
    {
        get; init;
    }
    public string Icon { get; init; } = "";
    [System.Text.Json.Serialization.JsonConverter(typeof(DisplayCountConverter))]
    public long Downloads
    {
        get; init;
    }
    public string[] Worlds { get; init; } = [];
    public string[] Files { get; init; } = [];
    public string Title => DisplayName.Length > 0 ? DisplayName : Name.Length > 0 ? Name : Id;
    public override string ToString() => Title;
}
internal sealed record LaunchOptions
{
    public bool AutoMemory
    {
        get; init;
    }
    public bool FullScreen
    {
        get; init;
    }
    public string Java { get; init; } = "";
    public int RamMb { get; init; } = 4096;
    public int Width { get; init; } = 1280;
    public int Height { get; init; } = 720;
    public string JvmArguments { get; init; } = "";
    public string WindowTitle { get; init; } = "";
    public string Server { get; init; } = "";
}
internal sealed record NativeMessage
{
    public string Id { get; init; } = "";
    public int Ping
    {
        get; init;
    }
    public string Error { get; init; } = "";
    public string Type { get; init; } = "";
    public string Message { get; init; } = "";
    public string RequestId { get; init; } = "";
    public string Token { get; init; } = "";
    public string Version { get; init; } = "";
    public string Name { get; init; } = "";
    public string Category { get; init; } = "";
    public string Root { get; init; } = "";
    public string Game { get; init; } = "";
    public string Loader { get; init; } = "";
    public string Notice { get; init; } = "";
    public string Player { get; init; } = "";
    public string Account { get; init; } = "";
    public string AccountType { get; init; } = "";
    public string AccountLabel { get; init; } = "";
    public string Image { get; init; } = "";
    public string Label { get; init; } = "";
    public string Url { get; init; } = "";
    public string Text { get; init; } = "";
    public string App { get; init; } = "";
    public string Dotnet { get; init; } = "";
    public string Os { get; init; } = "";
    public string Data { get; init; } = "";
    public string Target { get; init; } = "";
    public string Path { get; init; } = "";
    public bool Ok
    {
        get; init;
    }
    public bool Online
    {
        get; init;
    }
    public bool Inherited
    {
        get; init;
    }
    public bool Fallback
    {
        get; init;
    }
    public int Ms
    {
        get; init;
    }
    public int Players
    {
        get; init;
    }
    public int Max
    {
        get; init;
    }
    public NativeItem[] Items { get; init; } = [];
    public NativeItem[] Changes { get; init; } = [];
    public string[] Versions { get; init; } = [];
    public string[] Paths { get; init; } = [];
    public LaunchOptions Settings { get; init; } = new();
    public Dictionary<string, string> Payload { get; init; } = [];
}
internal sealed record CommunityEntry
{
    [System.Text.Json.Serialization.JsonExtensionData]
    public Dictionary<string, JsonElement>? AdditionalFields
    {
        get; init;
    }
    public string Id { get; init; } = "";
    public string Title { get; init; } = "";
    public string Text { get; init; } = "";
    public string Body { get; init; } = "";
    public string Name { get; init; } = "";
    public string Author { get; init; } = "";
    public string ImageUrl { get; init; } = "";
    public string Url { get; init; } = "";
    public string Address { get; init; } = "";
    public string Description { get; init; } = "";
    public string PublishedAt { get; init; } = "";
    public string ExpiresAt { get; init; } = "";
}

// Legacy local-mod snapshots contain downloads=""; network catalog counts are numeric.
internal sealed class DisplayCountConverter : System.Text.Json.Serialization.JsonConverter<long>
{
    public override long Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
        => reader.TokenType == JsonTokenType.Number && reader.TryGetInt64(out var value) ? value
        : reader.TokenType == JsonTokenType.String && long.TryParse(reader.GetString(), out value) ? value : 0;
    public override void Write(Utf8JsonWriter writer, long value, JsonSerializerOptions options) => writer.WriteNumberValue(value);
}
