using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
using BlockLaunch;
using BlockLaunch.Downloads;
using CmlLib.Core;
using CmlLib.Core.Files;
using CmlLib.Core.Installers;
using CmlLib.Core.Tasks;

namespace FastDownloadChecks;

/// <summary>
/// 高速下载引擎检查：分片并发、断点续传、Range 降级、哈希闭环与小文件单流。
/// 与仓库其它检查一致，失败直接抛异常，成功输出大写徽标。
/// </summary>
internal static class Program
{
    const int KiB = 1024;
    const int MiB = 1024 * 1024;

    static async Task<int> Main()
    {
        var root = Path.Combine(Path.GetTempPath(), "BlockLaunch-fast-download-checks-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            await ChunkedParallelAsync(root);
            await ResumeAfterCancelAsync(root);
            await RangeIgnoredFallsBackAsync(root);
            await BadHashFailsAsync(root);
            await SmallFileSingleStreamAsync(root);
            await MultiSourceFailoverAsync(root);
            await SourceRankingAsync();
            await SourceOrderPreferenceAsync();
            await MirrorPreferredDownloadAsync(root);
            await OfficialPreferredDownloadAsync(root);
            await PreferredFailsOverAsync(root);
            await MirrorMissingFileFallsBackAsync(root);
            await EngineRequestNotRewrittenAsync();
            SettingsCompat();
            await FileConcurrencyLimitAsync(root);
            await CmlLibMappingAsync(root);
            await UnknownSizeSingleStreamAsync(root);
            await UnknownSizeChunkedAsync(root);
            await NoContentLengthAcceptsEofAsync(root);
            await ForbiddenFailsAsync(root);
            await RequestHeadersAsync(root);
            ConfigDefaults();
            Console.WriteLine("ALL_FAST_DOWNLOAD_CHECKS_PASSED");
            return 0;
        }
        finally
        {
            try
            {
                Directory.Delete(root, true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    static async Task ChunkedParallelAsync(string root)
    {
        var data = Bytes(8 * MiB, 11);
        await using var server = new TestServer
        {
            Handler = request => new TestReply(data, request.RangeStart.HasValue ? 206 : 200, 30)
        };
        var target = Path.Combine(root, "parallel", "game.bin");
        var progress = new Collector();
        using var http = new HttpClient();
        var engine = new DownloadEngine(http, Options(4), _ => { });
        await engine.DownloadAsync(MakeTask("chunked", server.Url("/game.bin"), target, data), progress.Progress);
        Require(File.Exists(target), "CHUNKED_PARALLEL_DOWNLOAD_OK: 目标文件不存在");
        Require(Sha1(File.ReadAllBytes(target)).Equals(Sha1(data), StringComparison.OrdinalIgnoreCase), "CHUNKED_PARALLEL_DOWNLOAD_OK: 内容哈希不符");
        var requests = server.Snapshot();
        Require(requests.Length >= 2, $"CHUNKED_PARALLEL_DOWNLOAD_OK: 期望多次分片请求，实际 {requests.Length}");
        Require(requests.All(request => request.RangeStart.HasValue), "CHUNKED_PARALLEL_DOWNLOAD_OK: 存在未带 Range 的请求");
        Require(server.Peak >= 2, $"CHUNKED_PARALLEL_DOWNLOAD_OK: 未观察到并发，峰值 {server.Peak}");
        Require(progress.Snapshot().Any(item => item.Phase == DownloadPhase.Completed), "CHUNKED_PARALLEL_DOWNLOAD_OK: 缺少 Completed 进度");
        Console.WriteLine("CHUNKED_PARALLEL_DOWNLOAD_OK");
    }

    static async Task ResumeAfterCancelAsync(string root)
    {
        var data = Bytes(4 * MiB, 22);
        await using var server = new TestServer
        {
            Handler = request => new TestReply(data, request.RangeStart.HasValue ? 206 : 200, 20)
        };
        var target = Path.Combine(root, "resume", "game.bin");
        using var http = new HttpClient();
        using var cancellation = new CancellationTokenSource();
        var first = new Collector();
        var cancelled = false;
        IProgress<DownloadProgress> progress = new InlineProgress<DownloadProgress>(item =>
        {
            first.Report(item);
            if (cancelled || item.CompletedBytes < MiB)
                return;
            cancelled = true;
            cancellation.Cancel();
        });
        var engine = new DownloadEngine(http, Options(), _ => { });
        await ThrowsAsync<OperationCanceledException>(() => engine.DownloadAsync(MakeTask("resume", server.Url("/game.bin"), target, data), progress, 0, 1, cancellation.Token));
        Require(!File.Exists(target), "RESUME_SKIPS_COMPLETED_CHUNKS_OK: 取消后不应提交目标文件");
        var directory = Path.GetDirectoryName(target)!;
        Require(Directory.GetFiles(directory, "*.chunked").Length == 1, "RESUME_SKIPS_COMPLETED_CHUNKS_OK: 取消后应保留分片断点");

        var before = server.RequestCount;
        var engine2 = new DownloadEngine(new HttpClient(), Options(), _ => { });
        await engine2.DownloadAsync(MakeTask("resume", server.Url("/game.bin"), target, data));
        Require(File.Exists(target), "RESUME_SKIPS_COMPLETED_CHUNKS_OK: 恢复后目标文件不存在");
        Require(Sha1(File.ReadAllBytes(target)).Equals(Sha1(data), StringComparison.OrdinalIgnoreCase), "RESUME_SKIPS_COMPLETED_CHUNKS_OK: 恢复后内容损坏");
        var resumed = server.Snapshot().Skip(before).ToArray();
        Require(resumed.Length > 0, "RESUME_SKIPS_COMPLETED_CHUNKS_OK: 恢复后没有发出请求");
        Require(resumed.All(request => (request.RangeStart ?? 0) >= MiB), "RESUME_SKIPS_COMPLETED_CHUNKS_OK: 重复请求了已完成分片");
        var requested = resumed.Sum(request => (request.RangeEnd ?? data.Length - 1) - (request.RangeStart ?? 0) + 1);
        Require(requested < data.Length, $"RESUME_SKIPS_COMPLETED_CHUNKS_OK: 恢复时重复下载了全部字节（{requested}）");
        Console.WriteLine("RESUME_SKIPS_COMPLETED_CHUNKS_OK");
    }

    static async Task RangeIgnoredFallsBackAsync(string root)
    {
        var data = Bytes(4 * MiB, 33);
        await using var server = new TestServer
        {
            Handler = _ => new TestReply(data, 200, 10)
        };
        var target = Path.Combine(root, "fallback", "game.bin");
        using var http = new HttpClient();
        var engine = new DownloadEngine(http, Options(), _ => { });
        await engine.DownloadAsync(MakeTask("fallback", server.Url("/game.bin"), target, data));
        Require(File.Exists(target), "RANGE_IGNORED_FALLS_BACK_TO_SINGLE_OK: 回退后目标文件不存在");
        Require(Sha1(File.ReadAllBytes(target)).Equals(Sha1(data), StringComparison.OrdinalIgnoreCase), "RANGE_IGNORED_FALLS_BACK_TO_SINGLE_OK: 回退后内容损坏");
        var requests = server.Snapshot();
        Require(requests.Length == 2, $"RANGE_IGNORED_FALLS_BACK_TO_SINGLE_OK: 期望 1 次分片尝试 + 1 次单流，实际 {requests.Length}");
        Require(requests[^1].RangeStart is null, "RANGE_IGNORED_FALLS_BACK_TO_SINGLE_OK: 回退后的单流请求仍带了 Range");
        Console.WriteLine("RANGE_IGNORED_FALLS_BACK_TO_SINGLE_OK");
    }

    static async Task BadHashFailsAsync(string root)
    {
        var data = Bytes(2 * MiB, 44);
        var expected = Sha1(Bytes(2 * MiB, 45));
        await using var server = new TestServer
        {
            Handler = request => new TestReply(data, request.RangeStart.HasValue ? 206 : 200)
        };
        var target = Path.Combine(root, "hash", "game.bin");
        using var http = new HttpClient();
        var engine = new DownloadEngine(http, Options(retries: 1), _ => { });
        var error = await ThrowsAsync<IOException>(() => engine.DownloadAsync(MakeTask("hash", server.Url("/game.bin"), target, data, expected)));
        Require(error.Message.Contains("完整性校验", StringComparison.Ordinal), $"BAD_HASH_RETRIES_AND_FAILS_OK: 错误文案不明确（{error.Message}）");
        Require(!File.Exists(target), "BAD_HASH_RETRIES_AND_FAILS_OK: 坏哈希文件被提交");
        Require(Directory.GetFiles(Path.GetDirectoryName(target)!, "*.chunked").Length == 0, "BAD_HASH_RETRIES_AND_FAILS_OK: 坏哈希后残留分片数据");
        Console.WriteLine("BAD_HASH_RETRIES_AND_FAILS_OK");
    }

    static async Task SmallFileSingleStreamAsync(string root)
    {
        var data = Bytes(512 * KiB, 55);
        await using var server = new TestServer
        {
            Handler = _ => new TestReply(data)
        };
        var target = Path.Combine(root, "small", "mod.jar");
        using var http = new HttpClient();
        var engine = new DownloadEngine(http, Options(), _ => { });
        await engine.DownloadAsync(MakeTask("small", server.Url("/mod.jar"), target, data));
        Require(File.Exists(target), "SMALL_FILE_SINGLE_STREAM_OK: 目标文件不存在");
        Require(server.RequestCount == 1, $"SMALL_FILE_SINGLE_STREAM_OK: 小文件不应探测或分片，实际 {server.RequestCount} 次请求");
        Require(server.Snapshot()[0].RangeStart is null, "SMALL_FILE_SINGLE_STREAM_OK: 小文件请求带了 Range");
        Console.WriteLine("SMALL_FILE_SINGLE_STREAM_OK");
    }

    static async Task MultiSourceFailoverAsync(string root)
    {
        var data = Bytes(3 * MiB, 66);
        await using var primary = new TestServer
        {
            Handler = _ => new TestReply([], 500)
        };
        await using var mirror = new TestServer
        {
            Handler = request => new TestReply(data, request.RangeStart.HasValue ? 206 : 200, 10)
        };
        var target = Path.Combine(root, "failover", "game.bin");
        var task = new DownloadTask
        {
            Id = "failover",
            Kind = DownloadKind.Game,
            TargetPath = target,
            Sources = [DownloadSource.Official(primary.Url("/game.bin")), DownloadSource.Mirror("mirror", mirror.Url("/game.bin"))],
            Hash = Sha1(data),
            Size = data.Length
        };
        using var http = new HttpClient();
        var engine = new DownloadEngine(http, Options(), _ => { });
        await engine.DownloadAsync(task);
        Require(File.Exists(target), "MULTI_SOURCE_FAILOVER_OK: 备用源下载后目标文件不存在");
        Require(Sha1(File.ReadAllBytes(target)).Equals(Sha1(data), StringComparison.OrdinalIgnoreCase), "MULTI_SOURCE_FAILOVER_OK: 备用源内容损坏");
        Require(mirror.RequestCount > 0, "MULTI_SOURCE_FAILOVER_OK: 未访问备用源");
        Console.WriteLine("MULTI_SOURCE_FAILOVER_OK");
    }

    static async Task SourceRankingAsync()
    {
        DownloadSpeedCache.Clear();
        SourceHealth.ClearCache();
        var payload = Bytes(2 * MiB, 77);
        await using var slow = new TestServer
        {
            Handler = request => new TestReply(payload, request.RangeStart.HasValue ? 206 : 200, 150)
        };
        await using var fast = new TestServer
        {
            Handler = request => new TestReply(payload, request.RangeStart.HasValue ? 206 : 200)
        };
        using var http = new HttpClient();
        var selector = new SourceSelector(http, Options());
        DownloadSource[] sources = [DownloadSource.Mirror("slow", slow.Url("/a")), DownloadSource.Mirror("fast", fast.Url("/a"))];
        var ranked = await selector.RankAsync(sources, CancellationToken.None);
        Require(ranked[0].Name == "fast", $"SOURCE_RANKING_PREFERS_FAST_OK: 排序首选为 {ranked[0].Name}");
        var slowCalls = slow.RequestCount;
        await selector.RankAsync(sources, CancellationToken.None);
        Require(slow.RequestCount == slowCalls, $"SOURCE_RANKING_PREFERS_FAST_OK: 健康与测速缓存未生效（新增 {slow.RequestCount - slowCalls} 次请求）");
        Console.WriteLine("SOURCE_RANKING_PREFERS_FAST_OK");
    }

    static async Task SourceOrderPreferenceAsync()
    {
        SourceHealth.ClearCache();
        var payload = Bytes(512 * KiB, 78);
        await using var official = new TestServer
        {
            Handler = _ => new TestReply(payload)
        };
        await using var mirror = new TestServer
        {
            Handler = _ => new TestReply(payload)
        };
        using var http = new HttpClient();
        DownloadSource[] sources = [DownloadSource.Official(official.Url("/a")), DownloadSource.Mirror("mirror", mirror.Url("/a"))];

        var mirrored = await new SourceSelector(http, Options()).RankAsync(sources, CancellationToken.None, measure: false);
        Require(mirrored[0].IsMirror && mirrored[0].Name == "mirror", $"SOURCE_ORDER_PREFERENCE_OK: 镜像偏好首选为 {mirrored[0].Name}");

        SourceHealth.ClearCache();
        var direct = await new SourceSelector(http, Options(preference: DownloadSourcePreference.Official))
            .RankAsync(sources, CancellationToken.None, measure: false);
        Require(!direct[0].IsMirror, $"SOURCE_ORDER_PREFERENCE_OK: 官方偏好首选为 {direct[0].Name}");
        Console.WriteLine("SOURCE_ORDER_PREFERENCE_OK");
    }

    static async Task MirrorPreferredDownloadAsync(string root)
    {
        SourceHealth.ClearCache();
        var data = Bytes(2 * MiB, 81);
        await using var official = new TestServer
        {
            Handler = _ => new TestReply(data)
        };
        await using var mirror = new TestServer
        {
            Handler = request => new TestReply(data, request.RangeStart.HasValue ? 206 : 200, 5)
        };
        var target = Path.Combine(root, "preferred-mirror", "game.bin");
        var task = new DownloadTask
        {
            Id = "preferred-mirror",
            Kind = DownloadKind.Game,
            TargetPath = target,
            Sources = [DownloadSource.Official(official.Url("/game.bin")), DownloadSource.Mirror("mirror", mirror.Url("/game.bin"))],
            Hash = Sha1(data),
            Size = data.Length
        };
        using var http = new HttpClient();
        await new DownloadEngine(http, Options(), _ => { }).DownloadAsync(task);
        Require(File.Exists(target) && Sha1(File.ReadAllBytes(target)) == Sha1(data), "MIRROR_PREFERRED_OK: 镜像下载内容不一致");
        Require(official.Requests.All(request => request.Method == "HEAD"), "MIRROR_PREFERRED_OK: 官方源被用于下载");
        Console.WriteLine("MIRROR_PREFERRED_OK");
    }

    static async Task OfficialPreferredDownloadAsync(string root)
    {
        SourceHealth.ClearCache();
        var data = Bytes(2 * MiB, 82);
        await using var official = new TestServer
        {
            Handler = request => new TestReply(data, request.RangeStart.HasValue ? 206 : 200, 5)
        };
        await using var mirror = new TestServer
        {
            Handler = _ => new TestReply(data)
        };
        var target = Path.Combine(root, "preferred-official", "game.bin");
        var task = new DownloadTask
        {
            Id = "preferred-official",
            Kind = DownloadKind.Game,
            TargetPath = target,
            Sources = [DownloadSource.Official(official.Url("/game.bin")), DownloadSource.Mirror("mirror", mirror.Url("/game.bin"))],
            Hash = Sha1(data),
            Size = data.Length
        };
        using var http = new HttpClient();
        await new DownloadEngine(http, Options(preference: DownloadSourcePreference.Official), _ => { }).DownloadAsync(task);
        Require(File.Exists(target) && Sha1(File.ReadAllBytes(target)) == Sha1(data), "OFFICIAL_PREFERRED_OK: 官方下载内容不一致");
        Require(mirror.Requests.All(request => request.Method == "HEAD"), "OFFICIAL_PREFERRED_OK: 镜像源被用于下载");
        Console.WriteLine("OFFICIAL_PREFERRED_OK");
    }

    static async Task PreferredFailsOverAsync(string root)
    {
        SourceHealth.ClearCache();
        var data = Bytes(1 * MiB, 83);
        await using var official = new TestServer
        {
            Handler = request => new TestReply(data, request.RangeStart.HasValue ? 206 : 200)
        };
        await using var mirror = new TestServer
        {
            Handler = _ => new TestReply([], 500)
        };
        var target = Path.Combine(root, "preferred-failover", "game.bin");
        var task = new DownloadTask
        {
            Id = "preferred-failover",
            Kind = DownloadKind.Game,
            TargetPath = target,
            Sources = [DownloadSource.Official(official.Url("/game.bin")), DownloadSource.Mirror("mirror", mirror.Url("/game.bin"))],
            Hash = Sha1(data),
            Size = data.Length
        };
        using var http = new HttpClient();
        await new DownloadEngine(http, Options(retries: 0), _ => { }).DownloadAsync(task);
        Require(File.Exists(target) && Sha1(File.ReadAllBytes(target)) == Sha1(data), "PREFERRED_SOURCE_FAILS_OVER_OK: 切源后内容不一致");
        Require(mirror.RequestCount > 0, "PREFERRED_SOURCE_FAILS_OVER_OK: 未尝试首选镜像源");
        Console.WriteLine("PREFERRED_SOURCE_FAILS_OVER_OK");
    }

    static async Task MirrorMissingFileFallsBackAsync(string root)
    {
        SourceHealth.ClearCache();
        var data = Bytes(768 * KiB, 84);
        await using var official = new TestServer
        {
            Handler = request => new TestReply(data, request.RangeStart.HasValue ? 206 : 200)
        };
        await using var mirror = new TestServer
        {
            Handler = _ => new TestReply([], 404)
        };
        var target = Path.Combine(root, "mirror-missing", "mod.jar");
        var task = new DownloadTask
        {
            Id = "mirror-missing",
            Kind = DownloadKind.Mod,
            TargetPath = target,
            Sources = [DownloadSource.Official(official.Url("/mod.jar")), DownloadSource.Mirror("mirror", mirror.Url("/mod.jar"))],
            Hash = Sha1(data),
            Size = data.Length
        };
        using var http = new HttpClient();
        await new DownloadEngine(http, Options(retries: 0), _ => { }).DownloadAsync(task);
        Require(File.Exists(target) && Sha1(File.ReadAllBytes(target)) == Sha1(data), "MIRROR_MISSING_FILE_FALLS_BACK_OK: 回退官方后内容不一致");
        Require(mirror.RequestCount > 0, "MIRROR_MISSING_FILE_FALLS_BACK_OK: 未优先访问镜像源");
        Console.WriteLine("MIRROR_MISSING_FILE_FALLS_BACK_OK");
    }

    static async Task EngineRequestNotRewrittenAsync()
    {
        var settings = new NetworkSettings { DownloadSourcePreference = DownloadSourcePreference.Mirror };
        var hosts = new List<string?>();
        using var inner = new StubHandler(request =>
        {
            hosts.Add(request.RequestUri?.Host);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent([]) };
        });
        using var client = new HttpClient(new NetworkPolicyHandler(settings, inner));
        using (var direct = new HttpRequestMessage(HttpMethod.Get, "https://libraries.minecraft.net/test.jar"))
        {
            direct.Options.Set(NetworkPolicyHandler.DirectSource, true);
            using var response = await client.SendAsync(direct);
        }
        Require(hosts.Count == 1 && hosts[0] == "libraries.minecraft.net", $"ENGINE_REQUEST_NOT_REWRITTEN_OK: 标记请求被改写为 {string.Join(",", hosts)}");

        hosts.Clear();
        using (var normal = new HttpRequestMessage(HttpMethod.Get, "https://libraries.minecraft.net/test.jar"))
        using (var response = await client.SendAsync(normal))
        {
        }
        Require(hosts.Count == 1 && hosts[0] == "bmclapi2.bangbang93.com", $"ENGINE_REQUEST_NOT_REWRITTEN_OK: 普通请求未镜像为 {string.Join(",", hosts)}");
        Console.WriteLine("ENGINE_REQUEST_NOT_REWRITTEN_OK");
    }

    static void SettingsCompat()
    {
        var legacy = JsonSerializer.Deserialize<NetworkSettings>("{\"LimitKibPerSecond\":0}");
        Require(legacy is not null && legacy.DownloadSourcePreference == DownloadSourcePreference.Mirror, "SETTINGS_COMPAT_OK: 缺字段未取镜像默认");
        Require(new NetworkSettings { DownloadSource = "仅官方" }.EffectiveDownloadSource == DownloadSourcePreference.Official, "SETTINGS_COMPAT_OK: 旧仅官方未生效");
        Require(new NetworkSettings { DownloadSource = "BMCLAPI 优先" }.EffectiveDownloadSource == DownloadSourcePreference.Mirror, "SETTINGS_COMPAT_OK: 旧镜像优先未生效");

        var saved = new NetworkSettings { DownloadSourcePreference = DownloadSourcePreference.Official, DownloadSource = "" };
        var roundTrip = JsonSerializer.Deserialize<NetworkSettings>(JsonSerializer.Serialize(saved));
        Require(roundTrip is not null && roundTrip.EffectiveDownloadSource == DownloadSourcePreference.Official, "SETTINGS_COMPAT_OK: 枚举未持久化");

        var invalid = new NetworkSettings { DownloadSourcePreference = (DownloadSourcePreference)7, DownloadSource = "" };
        invalid.Validate();
        Require(invalid.DownloadSourcePreference == DownloadSourcePreference.Mirror, "SETTINGS_COMPAT_OK: 非法枚举未回退");
        Console.WriteLine("SETTINGS_COMPAT_OK");
    }

    static async Task FileConcurrencyLimitAsync(string root)
    {
        var data = Bytes(2 * MiB, 88);
        await using var server = new TestServer
        {
            Handler = _ => new TestReply(data, 200, 120)
        };
        using var http = new HttpClient();
        var options = Options(fileConcurrency: 2);
        var engine = new DownloadEngine(http, options, _ => { });
        var tasks = Enumerable.Range(0, 6).Select(index => new DownloadTask
        {
            Id = $"file-{index}",
            Kind = DownloadKind.Library,
            TargetPath = Path.Combine(root, "many", $"lib-{index}.jar"),
            Sources = [DownloadSource.Official(server.Url($"/lib-{index}.jar"))],
            Hash = Sha1(data),
            Size = data.Length,
            AllowSplitting = false
        }).ToList();
        await engine.DownloadAllAsync(tasks);
        Require(tasks.All(task => File.Exists(task.TargetPath)), "FILE_CONCURRENCY_LIMIT_OK: 存在未完成的文件");
        Require(server.Peak == 2, $"FILE_CONCURRENCY_LIMIT_OK: 期望 2 个并发连接，实际峰值 {server.Peak}");
        Console.WriteLine("FILE_CONCURRENCY_LIMIT_OK");
    }

    static async Task CmlLibMappingAsync(string root)
    {
        var big = Bytes(2 * MiB, 41);
        var small = Bytes(64 * KiB, 42);
        await using var server = new TestServer
        {
            Handler = request => new TestReply(
                request.Path.EndsWith("big.bin", StringComparison.Ordinal) ? big : small,
                request.RangeStart.HasValue ? 206 : 200,
                5)
        };
        var directory = Path.Combine(root, "cml", "assets", "objects");
        Directory.CreateDirectory(directory);
        var bigPath = Path.Combine(directory, "big.bin");
        var smallPath = Path.Combine(directory, "small.bin");
        File.WriteAllBytes(smallPath, small);

        var update = new TouchTask();
        var bigFile = new GameFile("assets/big")
        {
            Path = bigPath,
            Url = server.Url("/big.bin").AbsoluteUri,
            Hash = Sha1(big),
            Size = big.Length,
            UpdateTask = [update]
        };
        var smallFile = new GameFile("assets/small")
        {
            Path = smallPath,
            Url = server.Url("/small.bin").AbsoluteUri,
            Hash = Sha1(small),
            Size = small.Length
        };

        var fileEvents = new List<InstallerProgressChangedEventArgs>();
        var byteEvents = new List<ByteProgress>();
        var messages = new List<string>();
        using var http = new HttpClient();
        var installer = new FastGameInstaller(http, Options(4), text =>
        {
            lock (messages)
                messages.Add(text);
        });
        await installer.Install(
            [bigFile, smallFile],
            new InlineProgress<InstallerProgressChangedEventArgs>(value =>
            {
                lock (fileEvents)
                    fileEvents.Add(value);
            }),
            new InlineProgress<ByteProgress>(value =>
            {
                lock (byteEvents)
                    byteEvents.Add(value);
            }),
            CancellationToken.None);

        Require(File.Exists(bigPath) && Sha1(File.ReadAllBytes(bigPath)) == Sha1(big), "CMLIB_GAME_FILE_MAPPING_OK: 大文件未下载或哈希不符");
        Require(File.Exists(smallPath) && Sha1(File.ReadAllBytes(smallPath)) == Sha1(small), "CMLIB_GAME_FILE_MAPPING_OK: 已存在文件被破坏");
        Require(File.Exists(bigPath + ".touched"), "CMLIB_GAME_FILE_MAPPING_OK: 附加更新任务未执行");
        Require(server.Snapshot().All(request => request.Path.EndsWith("big.bin", StringComparison.Ordinal)), "CMLIB_GAME_FILE_MAPPING_OK: 已存在的文件被重新请求");
        Require(fileEvents.Count(item => item.EventType == InstallerEventType.Queued) == 2, "CMLIB_GAME_FILE_MAPPING_OK: Queued 事件数量不符");
        var done = fileEvents.Where(item => item.EventType == InstallerEventType.Done).ToArray();
        Require(done.Length == 2 && done[^1].ProgressedTasks == 2 && done[^1].TotalTasks == 2, "CMLIB_GAME_FILE_MAPPING_OK: Done 事件不完整");
        Require(byteEvents.Count > 0 && byteEvents[^1].TotalBytes == big.Length + small.Length && byteEvents[^1].ProgressedBytes == big.Length + small.Length, "CMLIB_GAME_FILE_MAPPING_OK: 字节进度未收敛");
        Require(messages.Any(text => text.Contains("big.bin", StringComparison.Ordinal)), "CMLIB_GAME_FILE_MAPPING_OK: 缺少进度文案");
        Console.WriteLine("CMLIB_GAME_FILE_MAPPING_OK");
    }

    static async Task UnknownSizeSingleStreamAsync(string root)
    {
        var data = Bytes(3 * MiB, 51);
        await using var server = new TestServer
        {
            Handler = _ => new TestReply(data, 200, 5)
        };
        var target = Path.Combine(root, "unknown-single", "file.bin");
        var progress = new Collector();
        using var http = new HttpClient();
        var engine = new DownloadEngine(http, Options(4), _ => { });
        await engine.DownloadAsync(MakeUnknownTask("unknown-single", server.Url("/file.bin"), target), progress.Progress);
        Require(File.Exists(target), "UNKNOWN_SIZE_SINGLE_STREAM_OK: 目标文件不存在");
        Require(Sha1(File.ReadAllBytes(target)) == Sha1(data), "UNKNOWN_SIZE_SINGLE_STREAM_OK: 内容哈希不符");
        var requests = server.Snapshot();
        Require(requests.Length == 2, $"UNKNOWN_SIZE_SINGLE_STREAM_OK: 期望探测 + 单流两次请求，实际 {requests.Length}");
        Require(requests[0].RangeStart == 0 && requests[0].RangeEnd == 0, "UNKNOWN_SIZE_SINGLE_STREAM_OK: 首次探测未使用 bytes=0-0");
        Require(requests[1].RangeStart is null, "UNKNOWN_SIZE_SINGLE_STREAM_OK: 单流不应再带 Range");
        var completed = progress.Snapshot().LastOrDefault();
        Require(completed is { Phase: DownloadPhase.Completed } && completed.TotalBytes == data.Length,
            "UNKNOWN_SIZE_SINGLE_STREAM_OK: 进度未按探测到的大小收敛");
        Console.WriteLine("UNKNOWN_SIZE_SINGLE_STREAM_OK");
    }

    static async Task UnknownSizeChunkedAsync(string root)
    {
        var data = Bytes(4 * MiB, 52);
        await using var server = new TestServer
        {
            Handler = request => new TestReply(data, request.RangeStart.HasValue ? 206 : 200, 20)
        };
        var target = Path.Combine(root, "unknown-chunked", "file.bin");
        using var http = new HttpClient();
        var engine = new DownloadEngine(http, Options(4), _ => { });
        await engine.DownloadAsync(MakeUnknownTask("unknown-chunked", server.Url("/file.bin"), target));
        Require(File.Exists(target), "UNKNOWN_SIZE_CHUNKED_AFTER_PROBE_OK: 目标文件不存在");
        Require(Sha1(File.ReadAllBytes(target)) == Sha1(data), "UNKNOWN_SIZE_CHUNKED_AFTER_PROBE_OK: 内容哈希不符");
        var requests = server.Snapshot();
        Require(requests.Length >= 5, $"UNKNOWN_SIZE_CHUNKED_AFTER_PROBE_OK: 期望探测 + 至少 4 个分片，实际 {requests.Length}");
        Require(requests[0].RangeStart == 0 && requests[0].RangeEnd == 0, "UNKNOWN_SIZE_CHUNKED_AFTER_PROBE_OK: 首次探测未使用 bytes=0-0");
        Require(requests.Skip(1).All(request => request.RangeStart.HasValue), "UNKNOWN_SIZE_CHUNKED_AFTER_PROBE_OK: 分片请求缺少 Range");
        Require(server.Peak >= 2, $"UNKNOWN_SIZE_CHUNKED_AFTER_PROBE_OK: 未观察到并发，峰值 {server.Peak}");
        Console.WriteLine("UNKNOWN_SIZE_CHUNKED_AFTER_PROBE_OK");
    }

    static async Task NoContentLengthAcceptsEofAsync(string root)
    {
        var data = Bytes(300 * KiB, 53);
        await using var server = new TestServer
        {
            Handler = _ => new TestReply(data, 200, 0, OmitLength: true)
        };
        var target = Path.Combine(root, "no-length", "file.bin");
        var progress = new Collector();
        using var http = new HttpClient();
        var engine = new DownloadEngine(http, Options(4), _ => { });
        await engine.DownloadAsync(MakeUnknownTask("no-length", server.Url("/file.bin"), target), progress.Progress);
        Require(File.Exists(target), "NO_CONTENT_LENGTH_EOF_ACCEPTED_OK: 目标文件不存在");
        Require(Sha1(File.ReadAllBytes(target)) == Sha1(data), "NO_CONTENT_LENGTH_EOF_ACCEPTED_OK: 内容不完整");
        Require(progress.Snapshot().Any(item => item.Phase == DownloadPhase.Completed), "NO_CONTENT_LENGTH_EOF_ACCEPTED_OK: 缺少 Completed 进度");
        Require(server.RequestCount == 2, $"NO_CONTENT_LENGTH_EOF_ACCEPTED_OK: 期望探测 + 单流两次请求，实际 {server.RequestCount}");
        Console.WriteLine("NO_CONTENT_LENGTH_EOF_ACCEPTED_OK");
    }

    static async Task ForbiddenFailsAsync(string root)
    {
        await using var server = new TestServer
        {
            Handler = _ => new TestReply([], 403)
        };
        var target = Path.Combine(root, "forbidden", "file.bin");
        using var http = new HttpClient();
        var engine = new DownloadEngine(http, Options(4), _ => { });
        var error = await ThrowsAsync<IOException>(() =>
            engine.DownloadAsync(MakeUnknownTask("forbidden", server.Url("/file.bin"), target)));
        Require(error.Message.Contains("403", StringComparison.Ordinal), "FORBIDDEN_FAILS_WITHOUT_RETRY_OK: 错误信息未包含状态码");
        Require(error.Message.Contains("file.bin", StringComparison.Ordinal), "FORBIDDEN_FAILS_WITHOUT_RETRY_OK: 错误信息未使用文件名");
        Require(error.Message.Contains("人机验证", StringComparison.Ordinal), "FORBIDDEN_FAILS_WITHOUT_RETRY_OK: 错误信息缺少可操作建议");
        Require(!error.Message.Contains("Response status code", StringComparison.Ordinal), "FORBIDDEN_FAILS_WITHOUT_RETRY_OK: 错误信息仍是英文原文");
        Require(!File.Exists(target), "FORBIDDEN_FAILS_WITHOUT_RETRY_OK: 失败后不应留下目标文件");
        Require(server.RequestCount == 2, $"FORBIDDEN_FAILS_WITHOUT_RETRY_OK: 403 不应重试，实际请求 {server.RequestCount}");
        Console.WriteLine("FORBIDDEN_FAILS_WITHOUT_RETRY_OK");
    }

    static async Task RequestHeadersAsync(string root)
    {
        var data = Bytes(64 * KiB, 91);
        HttpRequestMessage? seen = null;
        using var inner = new StubHandler(request =>
        {
            seen ??= request;
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(data) };
        });
        using var http = new HttpClient(inner);
        var target = Path.Combine(root, "headers", "file.bin");
        await new DownloadEngine(http, Options(), _ => { }).DownloadAsync(MakeTask("headers", new Uri("https://example.invalid/file.bin"), target, data));
        var request = seen ?? throw new InvalidOperationException("REQUEST_HEADERS_OK: 未捕获请求");
        var agent = request.Headers.UserAgent.ToString();
        Require(agent.StartsWith("MCUGLauncher/", StringComparison.Ordinal), $"REQUEST_HEADERS_OK: User-Agent 为 {agent}");
        Require(request.Headers.Accept.Count > 0, "REQUEST_HEADERS_OK: 缺少 Accept");
        Console.WriteLine("REQUEST_HEADERS_OK");
    }

    static void ConfigDefaults()
    {
        var settings = new NetworkSettings
        {
            MaxFileConcurrency = 99,
            MaxChunkConcurrency = 0,
            ChunkSizeThreshold = 0,
            SmallFileThreshold = long.MaxValue,
            SpeedTestBytes = 1,
            SpeedCacheMinutes = 0,
            RequestTimeout = 999,
            MaxRetries = -5,
            HashAlgorithm = "MD5"
        };
        settings.Validate();
        Require(settings.MaxFileConcurrency == 16, "CONFIG_CLAMP_AND_DEFAULTS_OK: 文件并发未夹取");
        Require(settings.MaxChunkConcurrency == 1, "CONFIG_CLAMP_AND_DEFAULTS_OK: 分片并发未夹取");
        Require(settings.ChunkSizeThreshold == 256 * KiB, "CONFIG_CLAMP_AND_DEFAULTS_OK: 分片阈值未夹取");
        Require(settings.SmallFileThreshold == 256 * KiB, "CONFIG_CLAMP_AND_DEFAULTS_OK: 小文件阈值未跟随分片阈值");
        Require(settings.SpeedTestBytes == 16 * KiB, "CONFIG_CLAMP_AND_DEFAULTS_OK: 测速流量未夹取");
        Require(settings.SpeedCacheMinutes == 1, "CONFIG_CLAMP_AND_DEFAULTS_OK: 测速缓存未夹取");
        Require(settings.RequestTimeout == 120, "CONFIG_CLAMP_AND_DEFAULTS_OK: 超时未夹取");
        Require(settings.MaxRetries == 0, "CONFIG_CLAMP_AND_DEFAULTS_OK: 重试次数未夹取");
        Require(settings.HashAlgorithm == "SHA1", "CONFIG_CLAMP_AND_DEFAULTS_OK: 未知哈希算法未回退");

        var options = DownloadOptions.From(settings);
        Require(options.MaxFileConcurrency == 16 && options.MaxChunkConcurrency == 1, "CONFIG_CLAMP_AND_DEFAULTS_OK: 配置映射丢失并发上限");
        Require(options.ChunkSizeThreshold == 256 * KiB && options.MaxRetries == 0, "CONFIG_CLAMP_AND_DEFAULTS_OK: 配置映射丢失阈值或重试");
        Require(options.HashAlgorithm == "SHA1", "CONFIG_CLAMP_AND_DEFAULTS_OK: 配置映射丢失哈希算法");

        settings.DownloadSourcePreference = DownloadSourcePreference.Official;
        Require(DownloadOptions.From(settings).Preference == DownloadSourcePreference.Official, "CONFIG_CLAMP_AND_DEFAULTS_OK: 下载源偏好未映射");

        var defaults = new DownloadOptions().Normalize();
        Require(defaults.MaxFileConcurrency == 4 && defaults.MaxChunkConcurrency == 3 && defaults.MaxRetries == 3, "CONFIG_CLAMP_AND_DEFAULTS_OK: 默认值被改动");
        Console.WriteLine("CONFIG_CLAMP_AND_DEFAULTS_OK");
    }

    static DownloadOptions Options(int chunkConcurrency = 4, int retries = 2, int fileConcurrency = 4, DownloadSourcePreference preference = DownloadSourcePreference.Mirror) => new()
    {
        MaxFileConcurrency = fileConcurrency,
        MaxChunkConcurrency = chunkConcurrency,
        ChunkSizeThreshold = MiB,
        MaxRetries = retries,
        RequestTimeout = 15,
        AllowInsecureTransport = true,
        Preference = preference
    };

    static DownloadTask MakeTask(string id, Uri uri, string target, byte[] content, string? hash = null) => new()
    {
        Id = id,
        Kind = DownloadKind.Game,
        TargetPath = target,
        Sources = [DownloadSource.Official(uri)],
        Hash = hash ?? Sha1(content),
        Size = content.Length
    };

    static DownloadTask MakeUnknownTask(string id, Uri uri, string target) => new()
    {
        Id = id,
        Kind = DownloadKind.Custom,
        TargetPath = target,
        Sources = [DownloadSource.Official(uri)],
        Hash = "",
        Size = 0
    };

    static byte[] Bytes(int length, int seed)
    {
        var buffer = new byte[length];
        new Random(seed).NextBytes(buffer);
        return buffer;
    }

    static string Sha1(byte[] data) => Convert.ToHexString(SHA1.HashData(data)).ToLowerInvariant();

    static void Require(bool condition, string message)
    {
        if (!condition)
            throw new InvalidOperationException(message);
    }

    static async Task<T> ThrowsAsync<T>(Func<Task> action)
        where T : Exception
    {
        try
        {
            await action();
        }
        catch (T error)
        {
            return error;
        }
        catch (Exception error)
        {
            throw new InvalidOperationException($"期望 {typeof(T).Name}，实际 {error.GetType().Name}: {error.Message}", error);
        }

        throw new InvalidOperationException($"期望 {typeof(T).Name}，但调用成功。");
    }

    sealed class TouchTask : IUpdateTask
    {
        public ValueTask Execute(GameFile file, CancellationToken cancellationToken)
        {
            File.WriteAllText(file.Path + ".touched", "1");
            return ValueTask.CompletedTask;
        }
    }

    sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            => Task.FromResult(respond(request));
    }

    sealed class Collector
    {
        readonly List<DownloadProgress> items = [];

        public IProgress<DownloadProgress> Progress => new InlineProgress<DownloadProgress>(Report);

        public void Report(DownloadProgress value)
        {
            lock (items)
                items.Add(value);
        }

        public DownloadProgress[] Snapshot()
        {
            lock (items)
                return [.. items];
        }
    }

    sealed class InlineProgress<T>(Action<T> handler) : IProgress<T>
    {
        public void Report(T value) => handler(value);
    }
}
