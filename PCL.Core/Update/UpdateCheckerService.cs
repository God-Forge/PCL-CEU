using Newtonsoft.Json.Linq;
using PCL.Core.Net;

namespace PCL.Core.Update;

public sealed class UpdateInfo {

    public string CurrentVersion { get; init; } = "";
    public string LatestVersion { get; init; } = "";
    public bool HasUpdate { get; init; }
    public string Url { get; init; } = "";
    public bool Prerelease { get; init; }

}

/// <summary>
/// 从 GitHub Release API 检查更新，不执行下载与替换。
/// </summary>
public static class UpdateCheckerService {

    public static async Task<UpdateInfo> CheckAsync(
        IStringFetcher fetcher,
        string apiUrl,
        string currentVersion,
        CancellationToken cancellationToken = default) {
        var text = await fetcher.GetStringAsync(apiUrl, cancellationToken).ConfigureAwait(false);
        var json = JObject.Parse(text);
        var latest = json["tag_name"]?.ToString() ?? "";
        var latestVersion = Normalize(latest);
        var current = Normalize(currentVersion);
        return new UpdateInfo {
            CurrentVersion = currentVersion,
            LatestVersion = latest,
            HasUpdate = Compare(latestVersion, current) > 0,
            Url = json["html_url"]?.ToString() ?? "",
            Prerelease = json["prerelease"]?.ToObject<bool>() == true
        };
    }

    private static string Normalize(string version) {
        var value = version.Trim();
        if (value.StartsWith('v') || value.StartsWith('V')) value = value.Substring(1);
        var dash = value.IndexOf('-');
        return dash >= 0 ? value.Substring(0, dash) : value;
    }

    private static int Compare(string left, string right) {
        return Version.TryParse(left, out var leftVersion) && Version.TryParse(right, out var rightVersion)
            ? leftVersion.CompareTo(rightVersion)
            : string.Compare(left, right, StringComparison.OrdinalIgnoreCase);
    }

}
