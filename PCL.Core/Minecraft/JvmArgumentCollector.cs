using Newtonsoft.Json.Linq;

namespace PCL.Core.Minecraft;

/// <summary>
/// 从版本 JSON 的 arguments.jvm 收集 JVM 参数，并应用 PCL 规则判断。
/// </summary>
public static class JvmArgumentCollector {

    /// <summary>
    /// 收集 JVM 参数。<paramref name="extraArguments"/> 会在版本自带参数之后追加，
    /// 用于承载用户自定义的 JVM 参数（VB 中的 VersionAdvanceJvm）。
    /// </summary>
    public static IReadOnlyList<string> Collect(
        JObject versionJson,
        string osVersion,
        IEnumerable<string>? extraArguments = null) {
        var result = new List<string>();
        if (versionJson["arguments"]?["jvm"] is JArray arguments) {
            foreach (var token in arguments) {
                switch (token.Type) {
                    case JTokenType.String:
                        result.Add(token.ToString().Trim());
                        break;
                    case JTokenType.Object:
                        var argument = (JObject)token;
                        if (argument["rules"] is not null && !PclRuleEvaluator.IsAllowed(argument["rules"], osVersion)) continue;
                        if (argument["value"] is null) continue;
                        if (argument["value"]!.Type == JTokenType.String) {
                            result.Add(argument["value"]!.ToString().Trim());
                        } else {
                            result.AddRange(argument["value"]!.Select(value => value.ToString().Trim()));
                        }
                        break;
                }
            }
        }
        if (extraArguments is not null) {
            result.AddRange(extraArguments
                .Where(argument => !string.IsNullOrWhiteSpace(argument))
                .Select(argument => argument.Trim()));
        }
        return result;
    }

}
