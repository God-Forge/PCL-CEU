using Newtonsoft.Json.Linq;

namespace PCL.Core.Minecraft;

/// <summary>
/// 从版本 JSON 的 arguments.game 收集游戏参数，并应用 PCL 规则判断。
/// </summary>
public static class GameArgumentCollector {

    public static IReadOnlyList<string> Collect(JObject versionJson, string osVersion) {
        var result = new List<string>();
        if (versionJson["arguments"]?["game"] is not JArray arguments) return result;

        foreach (var token in arguments) {
            switch (token.Type) {
                case JTokenType.String:
                    result.Add(token.ToString());
                    break;
                case JTokenType.Object:
                    var argument = (JObject)token;
                    if (argument["rules"] is not null && !PclRuleEvaluator.IsAllowed(argument["rules"], osVersion)) continue;
                    if (argument["value"] is null) continue;
                    if (argument["value"]!.Type == JTokenType.String) {
                        result.Add(argument["value"]!.ToString());
                    } else {
                        result.AddRange(argument["value"]!.Select(value => value.ToString()));
                    }
                    break;
            }
        }
        return result;
    }

}
