using Newtonsoft.Json.Linq;
using PCL.Core.Net;

namespace PCL.Core.Download;

public sealed class FabricListResultInfo {

    public string SourceName { get; init; } = "";
    public bool IsOfficial { get; init; }
    public JObject Value { get; init; } = new();

}

/// <summary>
/// 按下载源设置获取 Fabric 版本列表。
/// </summary>
public sealed class FabricListService {

    private readonly IStringFetcher _fetcher;

    public FabricListService(IStringFetcher? fetcher = null) {
        _fetcher = fetcher ?? new HttpStringFetcher();
    }

    public async Task<FabricListResultInfo> GetAsync(
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
                    ? "https://bmclapi2.bangbang93.com/fabric-meta/v2/versions"
                    : "https://meta.fabricmc.net/v2/versions";
                var text = await _fetcher.GetStringAsync(url, source.Token).ConfigureAwait(false);
                var json = JObject.Parse(text);
                if (json["game"] is null || json["loader"] is null || json["installer"] is null) {
                    throw new InvalidDataException("获取到的列表缺乏必要项");
                }
                return new FabricListResultInfo {
                    SourceName = bmclapi ? "BMCLAPI" : "Fabric 官方源",
                    IsOfficial = !bmclapi,
                    Value = json
                };
            } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
                throw;
            } catch (Exception ex) {
                lastError = ex;
            }
        }
        throw new AggregateException("Fabric 版本列表获取失败", lastError!);
    }

}
