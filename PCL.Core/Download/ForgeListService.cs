using Newtonsoft.Json.Linq;
using PCL.Core.Net;

namespace PCL.Core.Download;

public sealed class ForgeListResultInfo {

    public string SourceName { get; init; } = "";
    public bool IsOfficial { get; init; }
    public IReadOnlyList<string> Names { get; init; } = Array.Empty<string>();

}

/// <summary>
/// 按下载源设置获取 Forge 的 Minecraft 版本列表。
/// </summary>
public sealed class ForgeListService {

    private readonly IStringFetcher _fetcher;

    public ForgeListService(IStringFetcher? fetcher = null) {
        _fetcher = fetcher ?? new HttpStringFetcher();
    }

    public async Task<ForgeListResultInfo> GetAsync(
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
        throw new AggregateException("Forge 版本列表获取失败", lastError!);
    }

    private async Task<ForgeListResultInfo> FetchOfficialAsync(CancellationToken cancellationToken) {
        var html = await _fetcher.GetStringAsync(
            "https://files.minecraftforge.net/maven/net/minecraftforge/forge/index_1.2.4.html",
            cancellationToken).ConfigureAwait(false);
        if (html.Length < 200) throw new InvalidDataException("获取到的版本列表长度不足");
        var names = ForgeListParser.ParseOfficial(html);
        if (names.Count < 10) throw new InvalidDataException("获取到的版本数量不足");
        return new ForgeListResultInfo {
            SourceName = "Forge 官方源",
            IsOfficial = true,
            Names = names
        };
    }

    private async Task<ForgeListResultInfo> FetchBmclapiAsync(CancellationToken cancellationToken) {
        var text = await _fetcher.GetStringAsync(
            "https://bmclapi2.bangbang93.com/forge/minecraft",
            cancellationToken).ConfigureAwait(false);
        var names = ForgeListParser.ParseBmclapi(JArray.Parse(text));
        if (names.Count < 10) throw new InvalidDataException("获取到的版本数量不足");
        return new ForgeListResultInfo {
            SourceName = "BMCLAPI",
            IsOfficial = false,
            Names = names
        };
    }

}
