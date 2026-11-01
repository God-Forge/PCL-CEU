using System.Text;

namespace PCL.Core.Minecraft;

public sealed class OptionsTxtUpdateResult {

    public string CurrentLanguage { get; init; } = "none";
    public string RequiredLanguage { get; init; } = "none";
    public bool LanguageChanged { get; init; }

}

/// <summary>
/// 更新 options.txt 的语言与全屏设置。
/// </summary>
public static class OptionsTxtUpdater {

    public static OptionsTxtUpdateResult Update(
        string path,
        bool hasSaves,
        bool versionValid,
        int vanillaMajor,
        bool helpChinese,
        int windowType) {
        var values = Read(path);
        var current = values.TryGetValue("lang", out var language) ? language : "none";
        var required = current == "none" || !hasSaves
            ? (helpChinese ? "zh_cn" : "en_us")
            : current.ToLowerInvariant();
        if (!versionValid || vanillaMajor <= 10) {
            required = required.Substring(0, required.Length - 2) + required.Substring(required.Length - 2).ToUpperInvariant();
        }

        var changed = current != required;
        if (changed) {
            values["lang"] = "-";
            Write(path, values);
            values["lang"] = required;
        }

        if (windowType == 0) {
            values["fullscreen"] = "true";
        } else if (windowType != 1) {
            values["fullscreen"] = "false";
        }
        Write(path, values);
        return new OptionsTxtUpdateResult {
            CurrentLanguage = current,
            RequiredLanguage = required,
            LanguageChanged = changed
        };
    }

    private static Dictionary<string, string> Read(string path) {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!File.Exists(path)) return result;
        foreach (var line in File.ReadAllLines(path, Encoding.UTF8)) {
            var separator = line.IndexOf(':');
            if (separator <= 0) continue;
            result[line.Substring(0, separator)] = line.Substring(separator + 1);
        }
        return result;
    }

    private static void Write(string path, Dictionary<string, string> values) {
        var builder = new StringBuilder();
        foreach (var pair in values) {
            builder.Append(pair.Key).Append(':').Append(pair.Value).Append("\r\n");
        }
        File.WriteAllText(path, builder.ToString(), Encoding.UTF8);
    }

}
