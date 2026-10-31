using PCL.Core.Net;

namespace PCL.Core.Download;

public sealed class NeoForgeListResultInfo {

    public string SourceName { get; init; } = "";
    public bool IsOfficial { get; init; }
    public IReadOnlyList<string> VersionNames { get; init; } = Array.Empty<string>();

}

/// <summary>
/// 按下载源设置获取 NeoForge 版本列表。
/// </summary>
public sealed class NeoForgeListService {

    private readonly IStringFetcher _fetcher;

    public NeoForgeListService(IStringFetcher? fetcher = null) {
        _fetcher = fetcher ?? new HttpStringFetcher();
    }

    public async Task<NeoForgeListResultInfo> GetAsync(
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
        throw new AggregateException("NeoForge 版本列表获取失败", lastError!);
    }

    private async Task<NeoForgeListResultInfo> FetchOfficialAsync(CancellationToken cancellationToken) {
        const string baseUrl = "https://maven.neoforged.net/api/maven/versions/releases/net/neoforged/";
        var latest = await _fetcher.GetStringAsync(baseUrl + "neoforge", cancellationToken).ConfigureAwait(false);
        var legacy = await _fetcher.GetStringAsync(baseUrl + "forge", cancellationToken).ConfigureAwait(false);
        return Create("NeoForge 官方源", true, latest, legacy);
    }

    private async Task<NeoForgeListResultInfo> FetchBmclapiAsync(CancellationToken cancellationToken) {
        const string baseUrl = "https://bmclapi2.bangbang93.com/neoforge/meta/api/maven/details/releases/net/neoforged/";
        var latest = await _fetcher.GetStringAsync(baseUrl + "neoforge", cancellationToken).ConfigureAwait(false);
        var legacy = await _fetcher.GetStringAsync(baseUrl + "forge", cancellationToken).ConfigureAwait(false);
        return Create("BMCLAPI", false, latest, legacy);
    }

    private static NeoForgeListResultInfo Create(string sourceName, bool isOfficial, string latest, string legacy) {
        if (latest.Length < 100 || legacy.Length < 100) throw new InvalidDataException("获取到的版本列表长度不足");
        var names = NeoForgeVersionParser.Parse(latest, legacy);
        if (names.Count == 0) throw new InvalidDataException("无");
        return new NeoForgeListResultInfo {
            SourceName = sourceName,
            IsOfficial = isOfficial,
            VersionNames = names
        };
    }

}
