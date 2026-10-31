using Newtonsoft.Json.Linq;
using PCL.Core.Net;

namespace PCL.Core.Download;

public sealed class OptiFineListResultInfo {

    public string SourceName { get; init; } = "";
    public bool IsOfficial { get; init; }
    public IReadOnlyList<OptiFineVersionInfo> Versions { get; init; } = Array.Empty<OptiFineVersionInfo>();

}

/// <summary>
/// 按下载源设置获取 OptiFine 版本列表。
/// </summary>
public sealed class OptiFineListService {

    private readonly IStringFetcher _fetcher;

    public OptiFineListService(IStringFetcher? fetcher = null) {
        _fetcher = fetcher ?? new HttpStringFetcher();
    }

    public async Task<OptiFineListResultInfo> GetAsync(
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
                return bmclapi
                    ? await FetchBmclapiAsync(source.Token).ConfigureAwait(false)
                    : await FetchOfficialAsync(source.Token).ConfigureAwait(false);
            } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
                throw;
            } catch (Exception ex) {
                lastError = ex;
            }
        }
        throw new AggregateException("OptiFine 版本列表获取失败", lastError!);
    }

    private async Task<OptiFineListResultInfo> FetchOfficialAsync(CancellationToken cancellationToken) {
        var html = await _fetcher.GetStringAsync("https://optifine.net/downloads", cancellationToken).ConfigureAwait(false);
        if (html.Length < 200) throw new InvalidDataException("获取到的版本列表长度不足");
        var versions = OptiFineVersionParser.ParseOfficial(html);
        return new OptiFineListResultInfo {
            SourceName = "OptiFine 官方源",
            IsOfficial = true,
            Versions = versions
        };
    }

    private async Task<OptiFineListResultInfo> FetchBmclapiAsync(CancellationToken cancellationToken) {
        var text = await _fetcher.GetStringAsync("https://bmclapi2.bangbang93.com/optifine/versionList", cancellationToken).ConfigureAwait(false);
        var versions = OptiFineVersionParser.ParseBmclapi(JArray.Parse(text));
        return new OptiFineListResultInfo {
            SourceName = "BMCLAPI",
            IsOfficial = false,
            Versions = versions
        };
    }

}
