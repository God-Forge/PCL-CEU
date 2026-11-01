using System.Diagnostics;
using System.Net.Http;

namespace PCL.Core.Download;

public sealed class DownloadSpeedTarget {

    public string Name { get; init; } = "";
    public string Url { get; init; } = "";

}

public sealed class DownloadSpeedResult {

    public string Name { get; init; } = "";
    public string Url { get; init; } = "";
    public bool Success { get; init; }
    public long ElapsedMilliseconds { get; init; }
    public string? Error { get; init; }

}

/// <summary>
/// 测试下载源响应速度。
/// </summary>
public sealed class DownloadSourceSpeedTester {

    private readonly HttpMessageHandler? _handler;

    public DownloadSourceSpeedTester(HttpMessageHandler? handler = null) {
        _handler = handler;
    }

    public static IReadOnlyList<DownloadSpeedTarget> DefaultTargets() {
        return [
            new DownloadSpeedTarget { Name = "Mojang", Url = "https://launchermeta.mojang.com/mc/game/version_manifest.json" },
            new DownloadSpeedTarget { Name = "BMCLAPI", Url = "https://bmclapi2.bangbang93.com/mc/game/version_manifest.json" },
            new DownloadSpeedTarget { Name = "Modrinth 镜像", Url = "https://mod.mcimirror.top/modrinth" },
            new DownloadSpeedTarget { Name = "CurseForge 镜像", Url = "https://mod.mcimirror.top/curseforge" }
        ];
    }

    public async Task<IReadOnlyList<DownloadSpeedResult>> TestAllAsync(
        IEnumerable<DownloadSpeedTarget> targets,
        int timeoutMilliseconds = 10000,
        CancellationToken cancellationToken = default) {
        using var client = _handler is null ? new HttpClient() : new HttpClient(_handler, false);
        client.Timeout = TimeSpan.FromMilliseconds(timeoutMilliseconds);
        var results = new List<DownloadSpeedResult>();
        foreach (var target in targets) {
            var stopwatch = Stopwatch.StartNew();
            try {
                using var request = new HttpRequestMessage(HttpMethod.Head, target.Url);
                using var response = await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                    .ConfigureAwait(false);
                results.Add(new DownloadSpeedResult {
                    Name = target.Name,
                    Url = target.Url,
                    Success = response.IsSuccessStatusCode || (int)response.StatusCode is 301 or 302 or 403 or 405,
                    ElapsedMilliseconds = stopwatch.ElapsedMilliseconds
                });
            } catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested) {
                results.Add(new DownloadSpeedResult {
                    Name = target.Name,
                    Url = target.Url,
                    Success = false,
                    ElapsedMilliseconds = stopwatch.ElapsedMilliseconds,
                    Error = ex.Message
                });
            }
        }
        return results;
    }

    public static string Format(IReadOnlyList<DownloadSpeedResult> results) {
        var lines = results
            .OrderByDescending(result => result.Success)
            .ThenBy(result => result.ElapsedMilliseconds)
            .Select(result => result.Success
                ? $"{result.Name}：{result.ElapsedMilliseconds} ms"
                : $"{result.Name}：失败（{result.Error}）");
        return string.Join("\r\n", lines);
    }

}
