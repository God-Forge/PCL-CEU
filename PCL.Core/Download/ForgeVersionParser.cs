using System.Globalization;
using Newtonsoft.Json.Linq;

namespace PCL.Core.Download;

public sealed class ForgeVersionInfo {

    public string VersionName { get; init; } = "";
    public string? Branch { get; init; }
    public string Inherit { get; init; } = "";
    public string? Hash { get; init; }
    public string Category { get; init; } = "unknown";
    public bool IsRecommended { get; init; }
    public string ReleaseTime { get; init; } = "";

}

/// <summary>
/// 解析 BMCLAPI 的 Forge 版本列表。
/// </summary>
public static class ForgeVersionParser {

    public static IReadOnlyList<ForgeVersionInfo> ParseBmclapi(JArray json, string inherit, string? recommended) {
        var result = new List<ForgeVersionInfo>();
        foreach (var token in json.OfType<JObject>()) {
            string? hash = null;
            var category = "unknown";
            var priority = -1;
            foreach (var file in token["files"]?.OfType<JObject>() ?? Enumerable.Empty<JObject>()) {
                var fileCategory = file["category"]?.ToString();
                var format = file["format"]?.ToString();
                switch (fileCategory) {
                    case "installer" when format == "jar":
                        hash = file["hash"]?.ToString();
                        category = "installer";
                        priority = 2;
                        break;
                    case "universal" when priority <= 1 && format == "zip":
                        hash = file["hash"]?.ToString();
                        category = "universal";
                        priority = 1;
                        break;
                    case "client" when priority <= 0 && format == "zip":
                        hash = file["hash"]?.ToString();
                        category = "client";
                        priority = 0;
                        break;
                }
            }

            var name = token["version"]?.ToString();
            if (string.IsNullOrWhiteSpace(name)) continue;
            var modified = token["modified"]?.ToString();
            if (!DateTimeOffset.TryParse(modified, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var releaseTime)) {
                throw new InvalidDataException("Forge 版本发布时间无效：" + modified);
            }

            result.Add(new ForgeVersionInfo {
                VersionName = name!,
                Branch = token["branch"]?.ToString(),
                Inherit = inherit,
                Hash = hash,
                Category = category,
                IsRecommended = string.Equals(recommended, name, StringComparison.Ordinal),
                ReleaseTime = releaseTime.ToLocalTime().ToString("yyyy/MM/dd HH:mm", CultureInfo.InvariantCulture)
            });
        }
        return result;
    }

}
