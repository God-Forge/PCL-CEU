using System.Globalization;
using Newtonsoft.Json.Linq;

namespace PCL.Core.Download;

public sealed class LiteLoaderVersionInfo {

    public string Inherit { get; init; } = "";
    public bool IsLegacy { get; init; }
    public bool IsPreview { get; init; }
    public string FileName { get; init; } = "";
    public string? Md5 { get; init; }
    public string ReleaseTime { get; init; } = "";
    public JToken? JsonToken { get; init; }

}

/// <summary>
/// 解析 LiteLoader versions.json 的官方源与 BMCLAPI 数据。
/// </summary>
public static class LiteLoaderVersionParser {

    public static IReadOnlyList<LiteLoaderVersionInfo> Parse(JObject result) {
        var versions = new List<LiteLoaderVersionInfo>();
        if (result["versions"] is not JObject json) return versions;

        foreach (var pair in json.Properties()) {
            if (pair.Name.StartsWith("1.6", StringComparison.Ordinal)
                || pair.Name.StartsWith("1.5", StringComparison.Ordinal)) continue;

            var container = pair.Value["artefacts"] ?? pair.Value["snapshots"];
            var realEntry = container?["com.mumfrey:liteloader"]?["latest"];
            if (realEntry is null) continue;

            var segments = pair.Name.Split('.');
            var legacy = segments.Length > 1
                         && int.TryParse(segments[1], NumberStyles.Integer, CultureInfo.InvariantCulture, out var minor)
                         && minor < 8;
            var timestamp = realEntry["timestamp"]?.ToString();
            if (!DateTimeOffset.TryParse(timestamp, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var releaseTime)) {
                releaseTime = DateTimeOffset.MinValue;
            }

            versions.Add(new LiteLoaderVersionInfo {
                Inherit = pair.Name,
                IsLegacy = legacy,
                IsPreview = string.Equals(realEntry["stream"]?.ToString(), "snapshot", StringComparison.OrdinalIgnoreCase),
                FileName = "liteloader-installer-" + pair.Name
                           + (pair.Name is "1.8" or "1.9" ? ".0" : "")
                           + "-00-SNAPSHOT.jar",
                Md5 = realEntry["md5"]?.ToString(),
                ReleaseTime = releaseTime.ToLocalTime().ToString("yyyy/MM/dd HH:mm", CultureInfo.InvariantCulture),
                JsonToken = realEntry
            });
        }
        return versions;
    }

}
