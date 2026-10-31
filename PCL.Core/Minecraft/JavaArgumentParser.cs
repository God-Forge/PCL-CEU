using System.Globalization;
using System.Text.RegularExpressions;

namespace PCL.Core.Minecraft;

/// <summary>
/// Java 启动参数的拆分与去重规则，与 PCL 原有行为保持一致。
/// </summary>
public static class JavaArgumentParser {

    public static IReadOnlyList<string> Split(string arguments) {
        var normalized = arguments.Replace("\r\n", " ").Replace('\r', ' ').Replace('\n', ' ').Trim();
        if (string.IsNullOrWhiteSpace(normalized)) return Array.Empty<string>();

        var result = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;
        for (var i = 0; i < normalized.Length; i++) {
            var c = normalized[i];
            if (c == '\\' && i + 1 < normalized.Length && normalized[i + 1] == '"') {
                current.Append("\\\"");
                i++;
            } else if (c == '"') {
                inQuotes = !inQuotes;
                current.Append(c);
            } else if (c == ' ' && !inQuotes) {
                if (current.Length > 0) {
                    result.Add(current.ToString());
                    current.Clear();
                }
            } else {
                current.Append(c);
            }
        }
        if (current.Length > 0) result.Add(current.ToString());
        return result;
    }

    public static List<string> Deduplicate(IReadOnlyList<string> arguments, bool isJvmArguments, Action<string>? log = null) {
        var result = new List<string>();
        var label = isJvmArguments ? " JVM " : "游戏";
        for (var i = 0; i < arguments.Count;) {
            var key = arguments[i];
            var single = !key.StartsWith("-", StringComparison.Ordinal)
                         || i + 1 >= arguments.Count
                         || (arguments[i + 1].StartsWith("-", StringComparison.Ordinal)
                             && ParseVal(arguments[i + 1].Substring(1)) == 0);
            if (single) {
                i++;
                if (result.Contains(key)) {
                    log?.Invoke($"已去重单个{label}参数：{key}");
                    continue;
                }
                result.Add(key);
                continue;
            }

            var value = arguments[i + 1];
            i += 2;
            var merged = false;
            for (var j = 0; j < result.Count - 1; j++) {
                if (result[j] != key) continue;
                if (!isJvmArguments && key != "--tweakClass") {
                    log?.Invoke($"已覆盖重复的{label}参数：{key} {result[j + 1]} → {key} {value}");
                    result[j + 1] = value;
                    merged = true;
                    break;
                }
                if (result[j + 1] == value) {
                    log?.Invoke($"已去重{label}参数对：{key} {value}");
                    merged = true;
                    break;
                }
            }
            if (merged) continue;
            result.Add(key);
            result.Add(value);
        }
        return result;
    }

    private static double ParseVal(string value) {
        var match = Regex.Match(value, @"^\s*[+-]?(?:\d+(?:\.\d*)?|\.\d+)");
        return match.Success && double.TryParse(match.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : 0;
    }

}
