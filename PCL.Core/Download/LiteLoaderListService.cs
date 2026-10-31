using Newtonsoft.Json.Linq;
using PCL.Core.Net;

namespace PCL.Core.Download;

public sealed class LiteLoaderListResultInfo {

    public string SourceName { get; init; } = "";
    public bool IsOfficial { get; init; }
    public IReadOnlyList<LiteLoaderVersionInfo> Versions { get; init; } = Array.Empty<LiteLoaderVersionInfo>();

}

/// <summary>
/// 按下载源设置获取 LiteLoader 版本列表。
/// </summary>
public sealed class LiteLoaderListService {

    private readonly IStringFetcher _fetcher;

    public LiteLoaderListService(IStringFetcher? fetcher = null) {
        _fetcher = fetcher ?? new HttpStringFetcher();
    }

    public async Task<LiteLoaderListResultInfo> GetAsync(
        int sourceSetting,
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
                var url = bmclapi
                    ? "https://bmclapi2.bangbang93.com/maven/com/mumfrey/liteloader/versions.json"
                    : "https://dl.liteloader.com/versions/versions.json";
                var text = await _fetcher.GetStringAsync(url, source.Token).ConfigureAwait(false);
                var versions = LiteLoaderVersionParser.Parse(JObject.Parse(text));
                if (versions.Count == 0) throw new InvalidDataException("无");
                return new LiteLoaderListResultInfo {
                    SourceName = bmclapi ? "BMCLAPI" : "LiteLoader 官方源",
                    IsOfficial = !bmclapi,
                    Versions = versions
                };
            } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
                throw;
            } catch (Exception ex) {
                lastError = ex;
            }
        }
        throw new AggregateException("LiteLoader 版本列表获取失败", lastError!);
    }

}
