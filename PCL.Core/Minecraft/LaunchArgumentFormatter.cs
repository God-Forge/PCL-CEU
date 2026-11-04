namespace PCL.Core.Minecraft;

/// <summary>
/// 应用启动参数替换标记，并为含特殊字符的参数补引号。
/// </summary>
public static class LaunchArgumentFormatter {

    private static readonly char[] SpecialCharacters = ['&', '|', '<', '>', '^', ' '];

    public static string FormatSingle(string argument) {
        if (argument.IndexOfAny(SpecialCharacters) >= 0
            && !(argument.StartsWith("\"", StringComparison.Ordinal) && argument.EndsWith("\"", StringComparison.Ordinal))) {
            return "\"" + argument.Replace("\"", "\\\"") + "\"";
        }
        return argument;
    }

    public static List<string> Replace(
        IEnumerable<string> arguments,
        IEnumerable<(string Key, string Value)> replacements) {
        var replacementList = replacements.ToList();
        var result = new List<string>();
        foreach (var argument in arguments) {
            var value = argument;
            foreach (var (key, replacement) in replacementList) {
                value = value.Replace(key, replacement);
            }
            result.Add(value);
        }
        return result;
    }

    public static List<string> Format(
        IEnumerable<string> arguments,
        IEnumerable<(string Key, string Value)> replacements) {
        return Replace(arguments, replacements).Select(FormatSingle).ToList();
    }

    public static string JoinForDisplay(IEnumerable<string> arguments) {
        return string.Join(" ", arguments.Select(FormatSingle));
    }

}
