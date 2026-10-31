using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace PCL.Core.Download;

/// <summary>
/// 解析 Forge 的 Minecraft 版本列表。
/// </summary>
public static class ForgeListParser {

    public static IReadOnlyList<string> ParseOfficial(string html) {
        var names = Regex.Matches(html, "(?<=a href=\"index_)[0-9.]+((_pre|[_-]snapshot[_-]?)[0-9]?)?(?=.html)")
            .Cast<Match>()
            .Select(match => match.Value)
            .ToList();
        names.Add("1.2.4");
        return names;
    }

    public static IReadOnlyList<string> ParseBmclapi(JArray json) {
        return json.Select(token => token.ToString()).ToList();
    }

}
