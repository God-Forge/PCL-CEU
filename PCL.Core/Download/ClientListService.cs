using System.Diagnostics;
using Newtonsoft.Json.Linq;
using PCL.Core.Net;

namespace PCL.Core.Download;

public sealed class ClientListResultInfo {

    public string SourceName { get; init; } = "";
    public bool IsOfficial { get; init; }
    public JObject Value { get; init; } = new();
    public long ElapsedMilliseconds { get; init; }

}

/// <summary>
/// 按下载源设置获取 Minecraft 客户端版本列表，并处理 BMCLAPI 缓存合并与指定版本校验。
/// </summary>
public sealed class ClientListService {

    private readonly IStringFetcher _fetcher;

    public ClientListService(IStringFetcher? fetcher = null) {
        _fetcher = fetcher ?? new HttpStringFetcher();
    }

    public async Task<ClientListResultInfo> GetAsync(
        int sourceSetting,
        string? requiredVersion = null,
        string? cachePath = null,
        CancellationToken cancellationToken = default) {
        var sources = sourceSetting switch {
            0 => new (bool Bmclapi, int Timeout)[] { (true, 30), (false, 90) },
            1 => new[] { (false, 5), (true, 35) },
            _ => new[] { (false, 60), (true, 120) }
        };

        Exception? lastError = null;
        foreach (var (bmclapi, timeout) in sources) {
            try {
                using var source = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                source.CancelAfter(TimeSpan.FromSeconds(timeout));
                var stopwatch = Stopwatch.StartNew();
                var url = bmclapi
                    ? "https://bmclapi2.bangbang93.com/mc/game/version_manifest.json"
                    : "https://launchermeta.mojang.com/mc/game/version_manifest.json";
                var text = await _fetcher.GetStringAsync(url, source.Token).ConfigureAwait(false);
                var json = JObject.Parse(text);
                if (json["versions"] is not JArray versions || versions.Count < 200) {
                    throw new InvalidDataException("获取到的版本列表长度不足");
                }

                if (bmclapi && cachePath is not null && File.Exists(cachePath)) {
                    try {
                        if (JArray.Parse(File.ReadAllText(cachePath)) is { } extra) {
                            foreach (var item in extra) versions.Add(item);
                        }
                    } catch {
                        // 缓存损坏时忽略。
                    }
                }
                if (bmclapi && !string.IsNullOrEmpty(requiredVersion)
                    && !versions.Any(version => version["id"]?.ToString() == requiredVersion)) {
                    throw new InvalidDataException("BMCLAPI 源未包含目标版本 " + requiredVersion);
                }

                return new ClientListResultInfo {
                    SourceName = bmclapi ? "BMCLAPI" : "Mojang 官方源",
                    IsOfficial = !bmclapi,
                    Value = json,
                    ElapsedMilliseconds = stopwatch.ElapsedMilliseconds
                };
            } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
                throw;
            } catch (Exception ex) {
                lastError = ex;
            }
        }
        throw new AggregateException("Minecraft 版本列表获取失败", lastError!);
    }

}
