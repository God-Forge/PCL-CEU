namespace PCL.Core.Mods;

/// <summary>
/// 在 ModScanner 已有的「重复 ID / 缺前置」之外，补充三类更容易被忽略的冲突：
/// mixin 配置重名、核心库多版本共存、Mod 要求的 Java 高于实例 Java。
/// </summary>
public static class ModConflictAnalyzer {

    /// <summary>通常只应存在一份的核心库。</summary>
    private static readonly HashSet<string> CoreLibraries = new(StringComparer.OrdinalIgnoreCase) {
        "fabric-api", "architectury", "cloth-config", "fabric-language-kotlin",
        "quilted_fabric_api", "kotlinforforge", "geckolib", "patchouli", "curios"
    };

    public static IReadOnlyList<ModIssue> Analyze(IEnumerable<ModInfo>? mods, int? instanceJavaMajor = null) {
        if (mods is null) return [];
        var list = mods.Where(mod => mod.Enabled).ToList();
        if (list.Count == 0) return [];
        var issues = new List<ModIssue>();
        issues.AddRange(CheckMixinConflicts(list));
        issues.AddRange(CheckCoreLibraryVersions(list));
        issues.AddRange(CheckJavaRequirements(list, instanceJavaMajor));
        return issues;
    }

    /// <summary>两个 Mod 声明了同一个 mixin 配置文件，通常意味着其中一个打包有问题。</summary>
    private static IReadOnlyList<ModIssue> CheckMixinConflicts(IReadOnlyList<ModInfo> mods) {
        var issues = new List<ModIssue>();
        var owners = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        foreach (var mod in mods) {
            foreach (var config in mod.MixinConfigs) {
                if (!owners.TryGetValue(config, out var list)) {
                    list = [];
                    owners[config] = list;
                }
                list.Add(mod.FileName);
            }
        }
        foreach (var (config, files) in owners.Where(pair => pair.Value.Count > 1)) {
            issues.Add(new ModIssue {
                Severity = ModIssueSeverity.Warning,
                FileName = string.Join("、", files),
                Message = $"多个 Mod 声明了同一个 mixin 配置「{config}」，可能导致启动崩溃"
            });
        }
        return issues;
    }

    /// <summary>同一核心库出现多个版本。</summary>
    private static IReadOnlyList<ModIssue> CheckCoreLibraryVersions(IReadOnlyList<ModInfo> mods) {
        var issues = new List<ModIssue>();
        var byId = mods
            .Where(mod => !string.IsNullOrWhiteSpace(mod.ModId))
            .GroupBy(mod => mod.ModId, StringComparer.OrdinalIgnoreCase);
        foreach (var group in byId) {
            if (!CoreLibraries.Contains(group.Key)) continue;
            var distinctVersions = group
                .Select(mod => mod.Version)
                .Where(version => !string.IsNullOrWhiteSpace(version))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (distinctVersions.Count <= 1) continue;
            issues.Add(new ModIssue {
                Severity = ModIssueSeverity.Warning,
                FileName = string.Join("、", group.Select(mod => mod.FileName)),
                Message = $"核心库 {group.Key} 存在多个版本（{string.Join(" / ", distinctVersions)}），请只保留一个"
            });
        }
        return issues;
    }

    /// <summary>Mod 要求的 Java 版本高于实例使用的 Java。</summary>
    private static IReadOnlyList<ModIssue> CheckJavaRequirements(IReadOnlyList<ModInfo> mods, int? instanceJavaMajor) {
        if (instanceJavaMajor is null or <= 0) return [];
        var issues = new List<ModIssue>();
        foreach (var mod in mods) {
            if (mod.RequiredJavaMajor is not { } required) continue;
            if (required <= instanceJavaMajor) continue;
            issues.Add(new ModIssue {
                Severity = ModIssueSeverity.Error,
                FileName = mod.FileName,
                Message = $"该 Mod 需要 Java {required} 或更高，当前实例使用 Java {instanceJavaMajor}"
            });
        }
        return issues;
    }

}
