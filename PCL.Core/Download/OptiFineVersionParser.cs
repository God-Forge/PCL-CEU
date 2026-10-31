using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace PCL.Core.Download;

public sealed class OptiFineVersionInfo {

    public string DisplayName { get; init; } = "";
    public string ReleaseTime { get; init; } = "";
    public bool IsPreview { get; init; }
    public string Inherit { get; init; } = "";
    public string FileName { get; init; } = "";
    public string? RequiredForgeVersion { get; init; }
    public string InstanceName { get; init; } = "";

}

/// <summary>
/// 解析 OptiFine 官方 HTML 与 BMCLAPI 版本列表。
/// </summary>
public static class OptiFineVersionParser {

    public static IReadOnlyList<OptiFineVersionInfo> ParseOfficial(string html) {
        var forge = Matches(html, "(?<=colForge'>)[^<]*");
        var releaseTimes = Matches(html, "(?<=colDate'>)[^<]+");
        var names = Matches(html, "(?<=OptiFine_)[0-9A-Za-z_.]+(?=\\.jar\")");
        if (releaseTimes.Count != names.Count) throw new InvalidDataException("版本与发布时间数据无法对应");
        if (forge.Count != names.Count) throw new InvalidDataException("版本与 Forge 兼容数据无法对应");
        if (releaseTimes.Count < 10) throw new InvalidDataException("获取到的版本数量不足");

        var versions = new List<OptiFineVersionInfo>();
        for (var i = 0; i < names.Count; i++) {
            var name = names[i].Replace('_', ' ');
            var parts = releaseTimes[i].Split('.');
            var releaseTime = parts.Length >= 3 ? $"{parts[2]}/{parts[1]}/{parts[0]}" : releaseTimes[i];
            var requiredForge = forge[i].Replace("Forge ", "").Replace("#", "");
            if (requiredForge.Contains("N/A")) requiredForge = null;
            var separator = name.IndexOf(' ');
            var suffix = separator >= 0 ? name.Substring(separator + 1) : name;
            versions.Add(new OptiFineVersionInfo {
                DisplayName = name.Replace("HD U ", "").Replace(".0 ", " "),
                ReleaseTime = releaseTime,
                IsPreview = name.IndexOf("pre", StringComparison.OrdinalIgnoreCase) >= 0,
                Inherit = name.Split(' ')[0],
                FileName = (name.IndexOf("pre", StringComparison.OrdinalIgnoreCase) >= 0 ? "preview_" : "")
                           + "OptiFine_" + name.Replace(' ', '_') + ".jar",
                RequiredForgeVersion = requiredForge,
                InstanceName = name.Split(' ')[0] + "-OptiFine_" + suffix.Replace(' ', '_')
            });
        }
        return versions;
    }

    public static IReadOnlyList<OptiFineVersionInfo> ParseBmclapi(JArray json) {
        var versions = new List<OptiFineVersionInfo>();
        foreach (var token in json.OfType<JObject>()) {
            var mcVersion = token["mcversion"]?.ToString() ?? "";
            var type = token["type"]?.ToString() ?? "";
            var patch = token["patch"]?.ToString() ?? "";
            var displayName = (mcVersion + type.Replace("HD_U", "").Replace('_', ' ') + " " + patch).Replace(".0 ", " ");
            var requiredForge = token["forge"]?.ToString()?.Replace("Forge ", "").Replace("#", "");
            if (requiredForge?.Contains("N/A") == true) requiredForge = null;
            var instanceSuffix = (type + " " + patch)
                .Replace(".0 ", " ")
                .Replace(' ', '_')
                .Replace(mcVersion + "_", "");
            versions.Add(new OptiFineVersionInfo {
                DisplayName = displayName,
                ReleaseTime = "",
                IsPreview = patch.IndexOf("pre", StringComparison.OrdinalIgnoreCase) >= 0,
                Inherit = mcVersion,
                FileName = token["filename"]?.ToString() ?? "",
                RequiredForgeVersion = requiredForge,
                InstanceName = mcVersion + "-OptiFine_" + instanceSuffix
            });
        }
        return versions;
    }

    private static List<string> Matches(string html, string pattern) {
        return Regex.Matches(html, pattern).Cast<Match>().Select(match => match.Value).ToList();
    }

}
