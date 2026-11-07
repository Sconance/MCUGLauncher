using System.Net;
using BlockLaunch;

internal static class LoaderCatalogScenarios
{
    sealed class Responses(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            token.ThrowIfCancellationRequested();
            return Task.FromResult(respond(request));
        }
    }
    static void Check(bool value, string name)
    {
        if (!value)
            throw new Exception(name);
        Console.WriteLine("PASS " + name);
    }
    public static async Task Run()
    {
        var paths = new List<string>();
        var status = HttpStatusCode.OK;
        using var http = new HttpClient(new Responses(request =>
        {
            var path = request.RequestUri!.AbsolutePath;
            paths.Add(path);
            var games = path.EndsWith("/game");
            return new HttpResponseMessage(games ? HttpStatusCode.OK : status)
            {
                Content = new StringContent(games ? "[{\"version\":\"1.20.1\"},{\"version\":\"26.4-snapshot-6\"}]" : "[{\"loader\":{\"version\":\"0.18.0\"}},{\"loader\":{\"version\":\"0.18.0\"}}]")
            };
        }));
        Check((await LoaderCatalog.Get(http, "1.12.1", "fabric", default)).Count == 0 && paths.Count == 1,
            "unsupported Fabric game returns no versions without sending the invalid loader request");
        foreach (var loader in new[] { "fabric", "quilt" })
            Check((await LoaderCatalog.Get(http, "1.20.1", loader, default)).Single().Version == "0.18.0",
                loader + " supported game reads and deduplicates loader releases");
        Check((await LoaderCatalog.Get(http, "26.4-snapshot-6", "fabric", default)).Count == 1,
            "provider catalog supports snapshot names without a hard-coded version cutoff");
        foreach (var failure in new[] { HttpStatusCode.BadRequest, HttpStatusCode.ServiceUnavailable })
        {
            status = failure;
            var readable = false;
            try
            {
                await LoaderCatalog.Get(http, "1.20.1", "fabric", default);
            }
            catch (IOException error) { readable = error.Message.Contains("读取加载器列表失败") && error.Message.Contains(((int)failure).ToString()); }
            Check(readable, "loader service " + (int)failure + " is a readable failure, never a false compatibility result");
        }
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();
        var stopped = false;
        try
        {
            await LoaderCatalog.Get(http, "1.20.1", "fabric", cancelled.Token);
        }
        catch (OperationCanceledException) { stopped = true; }
        Check(stopped, "loader catalog preserves user cancellation");
    }
}
