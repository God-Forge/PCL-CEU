using System.Text.RegularExpressions;
using Newtonsoft.Json.Linq;

namespace PCL.Core.Minecraft;

/// <summary>
/// PCL 旧版规则判断：仅接受 Windows、排除 x86/ARM 与 Quick Play。
/// </summary>
public static class PclRuleEvaluator {

    public static bool IsAllowed(JToken? rules, string osVersion) {
        if (rules is null) return true;

        var required = false;
        foreach (var rule in rules.OfType<JObject>()) {
            var right = true;
            if (rule["os"] is JObject os) {
                var name = os["name"]?.ToString();
                if (!string.IsNullOrEmpty(name)) {
                    if (name == "unknown") {
                        // 不限制系统。
                    } else if (name == "windows") {
                        var version = os["version"]?.ToString();
                        if (version is not null) {
                            try {
                                right = right && Regex.IsMatch(osVersion, version);
                            } catch {
                                right = false;
                            }
                        }
                    } else {
                        right = false;
                    }
                }

                var arch = os["arch"]?.ToString();
                if (arch is not null) {
                    right = right && arch != "x86" && arch.IndexOf("arm", StringComparison.Ordinal) < 0;
                }
            }

            if (rule["features"] is JObject features) {
                right = right && features["is_demo_user"] is null;
                if (features.Properties().Any(property => property.Name.IndexOf("quick_play", StringComparison.Ordinal) >= 0)) {
                    right = false;
                }
            }

            if (rule["action"]?.ToString() == "allow") {
                if (right) required = true;
            } else {
                if (right) required = false;
            }
        }
        return required;
    }

}
