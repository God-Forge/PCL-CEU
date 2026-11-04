using Newtonsoft.Json.Linq;
using PCL.Core.Net;

namespace PCL.Core.Download;

/// <summary>
/// 获取指定 Minecraft 版本的版本 JSON，优先 Mojang，失败后使用 BMCLAPI。
/// </summary>
public sealed class VersionManifestService {

    private static readonly string[] ManifestUrls = [
        "https://piston-meta.mojang.com/mc/game/version_manifest_v2.json",
        "https://bmclapi2.bangbang93.com/mc/game/version_manifest_v2.json"
    ];

    private readonly IStringFetcher _fetcher;

    public VersionManifestService(IStringFetcher fetcher) {
        _fetcher = fetcher;
    }

    public async Task<IReadOnlyList<(string Id, string Type, string Url)>> GetVersionsAsync(CancellationToken cancellationToken = default) {
        Exception? lastError = null;
        foreach (var manifestUrl in ManifestUrls) {
            try {
                var manifest = JObject.Parse(await _fetcher.GetStringAsync(manifestUrl, cancellationToken).ConfigureAwait(false));
                var versions = manifest["versions"]?.OfType<JObject>()
                    .Select(item => (Id: item["id"]?.ToString() ?? "", Type: item["type"]?.ToString() ?? "", Url: item["url"]?.ToString() ?? ""))
                    .Where(item => !string.IsNullOrWhiteSpace(item.Id))
                    .ToList();
                if (versions is { Count: > 0 }) return versions!;
            } catch (OperationCanceledException) {
                throw;
            } catch (Exception ex) {
                lastError = ex;
            }
        }
        throw new InvalidOperationException("未能获取 Minecraft 版本列表。", lastError);
    }

    public async Task<string> GetVersionJsonAsync(string versionId, CancellationToken cancellationToken = default) {
        var url = await GetVersionUrlAsync(versionId, cancellationToken).ConfigureAwait(false);
        return await _fetcher.GetStringAsync(url, cancellationToken).ConfigureAwait(false);
    }

    public async Task<string> GetVersionUrlAsync(string versionId, CancellationToken cancellationToken = default) {
        var id = NormalizeId(versionId);
        Exception? lastError = null;
        foreach (var manifestUrl in ManifestUrls) {
            try {
                var manifest = JObject.Parse(await _fetcher.GetStringAsync(manifestUrl, cancellationToken).ConfigureAwait(false));
                var entry = manifest["versions"]?.OfType<JObject>()
                    .FirstOrDefault(item => string.Equals(item["id"]?.ToString(), id, StringComparison.OrdinalIgnoreCase));
                var url = entry?["url"]?.ToString();
                if (!string.IsNullOrWhiteSpace(url)) return url!;
            } catch (OperationCanceledException) {
                throw;
            } catch (Exception ex) {
                lastError = ex;
            }
        }
        throw new InvalidOperationException($"未能获取 Minecraft {versionId} 的版本 JSON 地址。", lastError);
    }

    private static string NormalizeId(string id) {
        var normalized = id.Replace('_', '-');
        if (normalized != "1.0" && normalized.EndsWith(".0", StringComparison.Ordinal)) {
            normalized = normalized.Substring(0, normalized.Length - 2);
        }
        return normalized;
    }

}

