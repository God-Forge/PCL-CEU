using Newtonsoft.Json.Linq;
using PCL.Core.Net;
using PCLCS;

namespace PCL.Core.Download;

public sealed class LoaderVersionInfo {

    public string Loader { get; init; } = "";
    public string VersionName { get; init; } = "";
    public string Inherit { get; init; } = "";
    public bool IsBeta { get; init; }
    public bool IsRecommended { get; init; }

}

/// <summary>
/// 从官方/镜像元数据中选择适用于指定 Minecraft 版本的加载器版本。
/// </summary>
public sealed class LoaderVersionService {

    private readonly IStringFetcher _fetcher;

    public LoaderVersionService(IStringFetcher fetcher) {
        _fetcher = fetcher;
    }

    public async Task<LoaderVersionInfo> FindForgeAsync(string mcVersion, CancellationToken cancellationToken = default) {
        var promos = JArray.Parse(await _fetcher.GetStringAsync("https://bmclapi2.bangbang93.com/forge/promos", cancellationToken).ConfigureAwait(false));
        foreach (var promo in promos.OfType<JObject>()) {
            if (!string.Equals(promo["name"]?.ToString(), mcVersion + "-recommended", StringComparison.OrdinalIgnoreCase)) continue;
            var version = promo["build"]?["version"]?.ToString();
            if (!string.IsNullOrWhiteSpace(version)) {
                return new LoaderVersionInfo {
                    Loader = "forge",
                    VersionName = version!,
                    Inherit = mcVersion,
                    IsRecommended = true
                };
            }
        }

        var listUrl = "https://bmclapi2.bangbang93.com/forge/minecraft/" + mcVersion.Replace('-', '_');
        var list = JArray.Parse(await _fetcher.GetStringAsync(listUrl, cancellationToken).ConfigureAwait(false));
        var versions = list.OfType<JObject>()
            .Select(item => item["version"]?.ToString())
            .Where(version => !string.IsNullOrWhiteSpace(version))
            .Cast<string>()
            .ToList();
        if (versions.Count == 0) throw new InvalidOperationException($"没有找到适用于 Minecraft {mcVersion} 的 Forge 版本。");

        var selected = versions.OrderByDescending(version => version, Comparer<string>.Create(McUtils.CompareVersion)).First();
        return new LoaderVersionInfo {
            Loader = "forge",
            VersionName = selected,
            Inherit = mcVersion
        };
    }

    public async Task<LoaderVersionInfo> FindNeoForgeAsync(string mcVersion, CancellationToken cancellationToken = default) {
        var latestJson = await _fetcher.GetStringAsync("https://maven.neoforged.net/api/maven/versions/releases/net/neoforged/neoforge", cancellationToken).ConfigureAwait(false);
        var legacyJson = await _fetcher.GetStringAsync("https://maven.neoforged.net/api/maven/versions/releases/net/neoforged/forge", cancellationToken).ConfigureAwait(false);

        var candidates = new List<LoaderVersionInfo>();
        AddNeoForgeVersions(latestJson, mcVersion, candidates);
        AddNeoForgeVersions(legacyJson, mcVersion, candidates);
        if (candidates.Count == 0) throw new InvalidOperationException($"没有找到适用于 Minecraft {mcVersion} 的 NeoForge 版本。");

        return candidates
            .OrderBy(item => item.IsBeta)
            .ThenByDescending(item => item.VersionName, Comparer<string>.Create(McUtils.CompareVersion))
            .First();
    }

    public async Task<LoaderVersionInfo> FindFabricAsync(string mcVersion, CancellationToken cancellationToken = default) {
        var json = JObject.Parse(await _fetcher.GetStringAsync("https://meta.fabricmc.net/v2/versions", cancellationToken).ConfigureAwait(false));
        var games = json["game"]?.ToObject<List<JObject>>();
        if (games is null || !games.Any(item => string.Equals(item["version"]?.ToString(), mcVersion, StringComparison.OrdinalIgnoreCase))) {
            throw new InvalidOperationException($"Fabric 列表中没有 Minecraft {mcVersion}。");
        }

        var loaders = json["loader"]?.ToObject<List<JObject>>();
        if (loaders is null || loaders.Count == 0) throw new InvalidOperationException("Fabric 列表中没有可用的 Loader。");
        var selected = loaders.FirstOrDefault(item => item["stable"]?.ToObject<bool>() == true) ?? loaders[0];
        var version = selected["version"]?.ToString();
        if (string.IsNullOrWhiteSpace(version)) throw new InvalidOperationException("Fabric Loader 版本解析失败。");

        return new LoaderVersionInfo {
            Loader = "fabric",
            VersionName = version!,
            Inherit = mcVersion
        };
    }

    private static void AddNeoForgeVersions(string json, string mcVersion, ICollection<LoaderVersionInfo> result) {
        var versions = JObject.Parse(json)["versions"]?.ToObject<List<string>>();
        if (versions is null) return;
        foreach (var version in versions) {
            if (string.IsNullOrWhiteSpace(version) || version == "47.1.82") continue;
            var inherit = GetNeoForgeInherit(version);
            if (!string.Equals(inherit, mcVersion, StringComparison.OrdinalIgnoreCase)) continue;
            result.Add(new LoaderVersionInfo {
                Loader = "neoforge",
                VersionName = version,
                Inherit = inherit,
                IsBeta = version.IndexOf("-beta", StringComparison.OrdinalIgnoreCase) >= 0
                         || version.IndexOf("-alpha", StringComparison.OrdinalIgnoreCase) >= 0
            });
        }
    }

    private static string GetNeoForgeInherit(string version) {
        if (version.StartsWith("1.20.1-", StringComparison.Ordinal)) return "1.20.1";

        var main = version.Split('+')[0].Split('-')[0];
        var parts = main.Split('.');
        string inherit;
        if (parts.Length >= 3 && int.TryParse(parts[0], out var major) && major >= 24) {
            inherit = $"{parts[0]}.{parts[1]}.{parts[2]}";
        } else if (parts.Length >= 2) {
            inherit = $"1.{parts[0]}.{parts[1]}";
        } else {
            inherit = version;
        }
        if (inherit.EndsWith(".0", StringComparison.Ordinal)) inherit = inherit.Substring(0, inherit.Length - 2);
        if (version.Contains("+")) inherit += "-" + version.Substring(version.IndexOf('+') + 1);
        return inherit;
    }

}
