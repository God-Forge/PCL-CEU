namespace PCL.Core.Minecraft;

/// <summary>
/// 应用启动参数替换标记，并为含特殊字符的参数补引号。
/// </summary>
public static class LaunchArgumentFormatter {

    private static readonly char[] SpecialCharacters = ['&', '|', '<', '>', '^', ' '];

    public static List<string> Format(
        IEnumerable<string> arguments,
        IEnumerable<(string Key, string Value)> replacements) {
        var replacementList = replacements.ToList();
        var result = new List<string>();
        foreach (var argument in arguments) {
            var value = argument;
            foreach (var (key, replacement) in replacementList) {
                value = value.Replace(key, replacement);
            }
            if (value.IndexOfAny(SpecialCharacters) >= 0
                && !(value.StartsWith("\"", StringComparison.Ordinal) && value.EndsWith("\"", StringComparison.Ordinal))) {
                value = "\"" + value.Replace("\"", "\\\"") + "\"";
            }
            result.Add(value);
        }
        return result;
    }

}
