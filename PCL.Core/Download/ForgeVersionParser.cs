using System.Globalization;
using System.Text.RegularExpressions;
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

    public static IReadOnlyList<ForgeVersionInfo> ParseOfficial(string html, string inherit) {
        var end = html.LastIndexOf("</table>", StringComparison.Ordinal);
        if (end < 0) throw new InvalidDataException("Forge 官方源页面缺少版本表格。");
        var chunks = html.Substring(0, end).Split(["<td class=\"download-version"], StringSplitOptions.None);
        var result = new List<ForgeVersionInfo>();
        for (var i = 1; i < chunks.Length; i++) {
            var chunk = chunks[i];
            var name = Regex.Match(chunk, "(?<=[^(0-9)]+)[0-9\\.]+").Value;
            if (string.IsNullOrWhiteSpace(name)) continue;

            var branchMatch = Regex.Match(chunk, $"(?<=-{Regex.Escape(name)}-)[^-\"]+(?=-[a-z]+\\.[a-z]{{3}})");
            var branch = branchMatch.Success ? branchMatch.Value : null;
            var releaseText = Regex.Match(chunk, "(?<=\"download-time\" title=\")[^\"]+").Value;
            if (!DateTime.TryParseExact(releaseText, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var releaseTime)) {
                throw new InvalidDataException("Forge 官方源发布时间无效：" + releaseText);
            }

            var recommended = chunk.Contains("fa promo-recommended");
            string? hash;
            string category;
            if (chunk.Contains("classifier-installer\"")) {
                chunk = chunk.Substring(chunk.IndexOf("installer.jar", StringComparison.Ordinal));
                hash = Regex.Match(chunk, "(?<=MD5:</strong> )[^<]+").Value;
                category = "installer";
            } else if (chunk.Contains("classifier-universal\"")) {
                chunk = chunk.Substring(chunk.IndexOf("universal.zip", StringComparison.Ordinal));
                hash = Regex.Match(chunk, "(?<=MD5:</strong> )[^<]+").Value;
                category = "universal";
            } else if (chunk.Contains("client.zip")) {
                chunk = chunk.Substring(chunk.IndexOf("client.zip", StringComparison.Ordinal));
                hash = Regex.Match(chunk, "(?<=MD5:</strong> )[^<]+").Value;
                category = "client";
            } else {
                continue;
            }

            result.Add(new ForgeVersionInfo {
                VersionName = name,
                Branch = branch,
                Inherit = inherit,
                Hash = hash?.Trim(),
                Category = category,
                IsRecommended = recommended,
                ReleaseTime = releaseTime.ToLocalTime().ToString("yyyy/MM/dd HH:mm", CultureInfo.InvariantCulture)
            });
        }
        return result;
    }

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
