using Newtonsoft.Json.Linq;
using PCL.Core.Net;

namespace PCL.Core.Download;

/// <summary>
/// 按下载源设置获取 Forge 版本列表，支持官方源与 BMCLAPI 回退。
/// </summary>
public sealed class ForgeVersionService {

    private readonly IStringFetcher _fetcher;

    public ForgeVersionService(IStringFetcher? fetcher = null) {
        _fetcher = fetcher ?? new HttpStringFetcher();
    }

    public async Task<IReadOnlyList<ForgeVersionInfo>> GetVersionsAsync(
        string mcVersion,
        int sourceSetting,
        string? recommended = null,
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
                    ? await FetchBmclapiAsync(mcVersion, recommended, source.Token).ConfigureAwait(false)
                    : await FetchOfficialAsync(mcVersion, source.Token).ConfigureAwait(false);
            } catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) {
                throw;
            } catch (Exception ex) {
                lastError = ex;
            }
        }
        throw new AggregateException("Forge 版本列表获取失败", lastError!);
    }

    private async Task<IReadOnlyList<ForgeVersionInfo>> FetchOfficialAsync(string mcVersion, CancellationToken cancellationToken) {
        var url = "https://files.minecraftforge.net/maven/net/minecraftforge/forge/index_"
                  + mcVersion.Replace("-", "_") + ".html";
        var html = await _fetcher.GetStringAsync(url, cancellationToken).ConfigureAwait(false);
        if (html.Length < 1000) throw new InvalidDataException("获取到的版本列表长度不足");
        var versions = ForgeVersionParser.ParseOfficial(html, mcVersion);
        if (versions.Count == 0) throw new InvalidDataException("无");
        return versions;
    }

    private async Task<IReadOnlyList<ForgeVersionInfo>> FetchBmclapiAsync(string mcVersion, string? recommended, CancellationToken cancellationToken) {
        var url = "https://bmclapi2.bangbang93.com/forge/minecraft/" + mcVersion.Replace("-", "_");
        var text = await _fetcher.GetStringAsync(url, cancellationToken).ConfigureAwait(false);
        var json = JArray.Parse(text);
        var versions = ForgeVersionParser.ParseBmclapi(json, mcVersion, recommended);
        if (versions.Count == 0) throw new InvalidDataException("无");
        return versions;
    }

}
