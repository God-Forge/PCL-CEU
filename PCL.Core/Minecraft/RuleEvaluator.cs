using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace PCL.Core.Minecraft;

public sealed class RuleContext {

    public string OsName { get; init; } = "windows";
    public string OsVersion { get; init; } = "";
    public string Arch { get; init; } = "";
    public IReadOnlyDictionary<string, bool> Features { get; init; } = new Dictionary<string, bool>();

    public static RuleContext Current() {
        string os;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows)) os = "windows";
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX)) os = "osx";
        else os = "linux";

        var arch = RuntimeInformation.ProcessArchitecture switch {
            Architecture.X86 => "x86",
            Architecture.X64 => "x86_64",
            Architecture.Arm64 => "arm64",
            _ => RuntimeInformation.ProcessArchitecture.ToString().ToLowerInvariant()
        };

        return new RuleContext {
            OsName = os,
            OsVersion = Environment.OSVersion.Version.ToString(),
            Arch = arch
        };
    }

}

/// <summary>
/// 评估 Minecraft 版本 JSON 中的 rules（os / features）。
/// </summary>
public static class RuleEvaluator {

    public static bool IsAllowed(JToken? rules, RuleContext context) {
        if (rules is not JArray array || array.Count == 0) return true;

        var allowed = false;
        foreach (var rule in array.OfType<JObject>()) {
            if (!Matches(rule, context)) continue;
            allowed = string.Equals(rule["action"]?.ToString(), "allow", StringComparison.OrdinalIgnoreCase);
        }
        return allowed;
    }

    private static bool Matches(JObject rule, RuleContext context) {
        if (rule["os"] is JObject os) {
            var name = os["name"]?.ToString();
            if (!string.IsNullOrWhiteSpace(name) && !string.Equals(name, context.OsName, StringComparison.OrdinalIgnoreCase)) return false;
            var arch = os["arch"]?.ToString();
            if (!string.IsNullOrWhiteSpace(arch) && !string.Equals(arch, context.Arch, StringComparison.OrdinalIgnoreCase)) return false;
            var version = os["version"]?.ToString();
            if (!string.IsNullOrWhiteSpace(version)) {
                try {
                    if (!Regex.IsMatch(context.OsVersion, version!)) return false;
                } catch {
                    return false;
                }
            }
        }

        if (rule["features"] is JObject features) {
            foreach (var property in features.Properties()) {
                var expected = property.Value.ToObject<bool>();
                var actual = context.Features.TryGetValue(property.Name, out var value) && value;
                if (expected != actual) return false;
            }
        }

        return true;
    }

}
