using System.Text.RegularExpressions;

namespace PCL.Core.Download;

/// <summary>
/// 从 NeoForge Maven 版本 JSON 中提取版本名。
/// </summary>
public static class NeoForgeVersionParser {

    private const string Pattern = "(?<=\")(1\\.20\\.1-)?\\d+\\.[^\\.]+\\.\\d+(\\.\\d+)?(-(beta|alpha)(\\.\\d+)?)?(\\+snapshot-\\d+)?(?=\")";

    public static IReadOnlyList<string> Parse(string latestJson, string legacyJson) {
        return Regex.Matches(legacyJson + latestJson, Pattern)
            .Cast<Match>()
            .Select(match => match.Value)
            .Where(name => name != "47.1.82")
            .Distinct(StringComparer.Ordinal)
            .ToList();
    }

}
