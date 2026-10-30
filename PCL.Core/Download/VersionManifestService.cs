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

    public async Task<string> GetVersionJsonAsync(string versionId, CancellationToken cancellationToken = default) {
        var id = NormalizeId(versionId);
        Exception? lastError = null;
        foreach (var manifestUrl in ManifestUrls) {
            try {
                var manifest = JObject.Parse(await _fetcher.GetStringAsync(manifestUrl, cancellationToken).ConfigureAwait(false));
                var entry = manifest["versions"]?.OfType<JObject>()
                    .FirstOrDefault(item => string.Equals(item["id"]?.ToString(), id, StringComparison.OrdinalIgnoreCase));
                var url = entry?["url"]?.ToString();
                if (string.IsNullOrWhiteSpace(url)) continue;
                return await _fetcher.GetStringAsync(url!, cancellationToken).ConfigureAwait(false);
            } catch (OperationCanceledException) {
                throw;
            } catch (Exception ex) {
                lastError = ex;
            }
        }
        throw new InvalidOperationException($"未能获取 Minecraft {versionId} 的版本 JSON。", lastError);
    }

    private static string NormalizeId(string id) {
        var normalized = id.Replace('_', '-');
        if (normalized != "1.0" && normalized.EndsWith(".0", StringComparison.Ordinal)) {
            normalized = normalized.Substring(0, normalized.Length - 2);
        }
        return normalized;
    }

}
