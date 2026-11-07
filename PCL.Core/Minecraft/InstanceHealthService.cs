using System.Text;
using Newtonsoft.Json.Linq;
using PCL.Core.Mods;

namespace PCL.Core.Minecraft;

/// <summary>健康分等级。</summary>
public enum InstanceHealthGrade {
    S,
    A,
    B,
    C,
    D
}

/// <summary>健康分的单个维度。</summary>
public sealed class InstanceHealthDimension {

    public string Key { get; init; } = "";

    public string Name { get; init; } = "";

    /// <summary>0-100。</summary>
    public int Score { get; init; }

    public string Summary { get; init; } = "";

}

/// <summary>可执行的修复建议。</summary>
public enum InstanceFixKind {
    /// <summary>重新下载缺失的原版基础文件（JSON、客户端 JAR、libraries、资源索引）。</summary>
    RepairBaseFiles,
    /// <summary>把实例迁移到纯 ASCII 路径。</summary>
    MigrateAsciiPath,
    /// <summary>清理磁盘空间。</summary>
    FreeDiskSpace,
    /// <summary>处理重复/冲突的 Mod。</summary>
    ResolveModConflicts,
    /// <summary>补装缺失的前置 Mod。</summary>
    InstallMissingMods,
    /// <summary>需要人工判断的其它问题。</summary>
    Manual
}

/// <summary>一条修复建议；<see cref="CanAutoFix"/> 为 true 时可以对实例执行自动修复。</summary>
public sealed class InstanceFixAction {

    public string Title { get; init; } = "";

    public string Detail { get; init; } = "";

    public InstanceFixKind Kind { get; init; }

    public bool CanAutoFix { get; init; }

}

/// <summary>实例健康分报告。</summary>
public sealed class InstanceHealthReport {

    public string InstanceName { get; init; } = "";

    /// <summary>0-100 的总分。</summary>
    public int Score { get; init; }

    public InstanceHealthGrade Grade { get; init; }

    public IReadOnlyList<InstanceHealthDimension> Dimensions { get; init; } = [];

    public IReadOnlyList<InstanceFixAction> FixActions { get; init; } = [];

    public int ErrorCount { get; init; }

    public int WarningCount { get; init; }

    public int InfoCount { get; init; }

    public int ModCount { get; init; }

    public DateTime EvaluatedUtc { get; init; }

    /// <summary>一句话评语。</summary>
    public string Comment { get; init; } = "";

    /// <summary>是否建议执行「一键修复」。</summary>
    public bool CanAutoFix => FixActions.Any(action => action.CanAutoFix);

}

/// <summary>健康分趋势中的一条历史记录。</summary>
public sealed class InstanceHealthHistoryEntry {

    public string Instance { get; init; } = "";

    public int Score { get; init; }

    public InstanceHealthGrade Grade { get; init; }

    public int Errors { get; init; }

    public int Warnings { get; init; }

    public DateTime EvaluatedUtc { get; init; }

}

/// <summary>
/// 实例健康分：在 <see cref="InstanceCheckService"/> 的体检结果上做加权评分，并生成修复建议。
/// 评分规则固定且可测试：实例问题按严重度扣分，Mod 问题按严重度扣分，扣到 0 为止。
/// </summary>
public static class InstanceHealthService {

    public const int MaxScore = 100;

    // 扣分规则：数值一旦调整会影响历史趋势的可比性，改动时请同步文档
    public const int InstanceErrorPenalty = 25;
    public const int InstanceWarningPenalty = 6;
    public const int InstanceInfoPenalty = 1;
    public const int ModErrorPenalty = 20;
    public const int ModWarningPenalty = 4;
    public const int ModInfoPenalty = 1;

    public static InstanceHealthReport Evaluate(string instanceFolder, string minecraftFolder) {
        var check = InstanceCheckService.Check(instanceFolder, minecraftFolder);
        return Evaluate(instanceFolder, check);
    }

    /// <summary>基于已有体检结果评分，供页面复用同一次扫描。</summary>
    public static InstanceHealthReport Evaluate(string instanceFolder, InstanceCheckResult check) {
        ArgumentNullException.ThrowIfNull(check);
        var instanceName = string.IsNullOrWhiteSpace(instanceFolder)
            ? ""
            : Path.GetFileName(Path.GetFullPath(instanceFolder).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar));

        var instanceErrors = check.Issues.Count(issue => issue.Severity == InstanceCheckSeverity.Error);
        var instanceWarnings = check.Issues.Count(issue => issue.Severity == InstanceCheckSeverity.Warning);
        var instanceInfos = check.Issues.Count(issue => issue.Severity == InstanceCheckSeverity.Info);
        var modErrors = check.ModIssues.Count(issue => issue.Severity == ModIssueSeverity.Error);
        var modWarnings = check.ModIssues.Count(issue => issue.Severity == ModIssueSeverity.Warning);
        var modInfos = check.ModIssues.Count(issue => issue.Severity == ModIssueSeverity.Info);

        var basePenalty = instanceErrors * InstanceErrorPenalty
                          + instanceWarnings * InstanceWarningPenalty
                          + instanceInfos * InstanceInfoPenalty;
        var modPenalty = modErrors * ModErrorPenalty
                         + modWarnings * ModWarningPenalty
                         + modInfos * ModInfoPenalty;

        var score = Math.Clamp(MaxScore - basePenalty - modPenalty, 0, MaxScore);
        var grade = GradeOf(score);

        var dimensions = new List<InstanceHealthDimension> {
            new() {
                Key = "base",
                Name = "基础文件",
                Score = Math.Clamp(MaxScore - basePenalty, 0, MaxScore),
                Summary = Describe(instanceErrors, instanceWarnings, "版本 JSON、客户端 JAR、libraries 与资源索引")
            },
            new() {
                Key = "mods",
                Name = "Mod 环境",
                Score = Math.Clamp(MaxScore - modPenalty, 0, MaxScore),
                Summary = check.Mods.Count == 0
                    ? "未安装 Mod。"
                    : Describe(modErrors, modWarnings, $"共 {check.Mods.Count} 个 Mod")
            }
        };

        return new InstanceHealthReport {
            InstanceName = instanceName,
            Score = score,
            Grade = grade,
            Dimensions = dimensions,
            FixActions = BuildFixActions(check),
            ErrorCount = instanceErrors + modErrors,
            WarningCount = instanceWarnings + modWarnings,
            InfoCount = instanceInfos + modInfos,
            ModCount = check.Mods.Count,
            EvaluatedUtc = DateTime.UtcNow,
            Comment = CommentOf(grade)
        };
    }

    public static InstanceHealthGrade GradeOf(int score) => score switch {
        >= 95 => InstanceHealthGrade.S,
        >= 85 => InstanceHealthGrade.A,
        >= 75 => InstanceHealthGrade.B,
        >= 60 => InstanceHealthGrade.C,
        _ => InstanceHealthGrade.D
    };

    public static string GradeText(InstanceHealthGrade grade) => grade.ToString();

    public static string CommentOf(InstanceHealthGrade grade) => grade switch {
        InstanceHealthGrade.S => "状态极佳，可以放心开玩。",
        InstanceHealthGrade.A => "整体健康，只有少量小问题。",
        InstanceHealthGrade.B => "能启动，但建议处理下列警告。",
        InstanceHealthGrade.C => "问题偏多，可能影响稳定性。",
        _ => "问题严重，建议先修复再启动。"
    };

    /// <summary>把报告格式化成可粘贴的文本。</summary>
    public static string FormatReport(InstanceHealthReport report) {
        ArgumentNullException.ThrowIfNull(report);
        var builder = new StringBuilder();
        builder.AppendLine($"实例：{report.InstanceName}");
        builder.AppendLine($"健康分：{report.Score} / {MaxScore}（{GradeText(report.Grade)}）");
        builder.AppendLine(report.Comment);
        builder.AppendLine();
        foreach (var dimension in report.Dimensions) {
            builder.AppendLine($"[{dimension.Name}] {dimension.Score} 分 —— {dimension.Summary}");
        }
        builder.AppendLine();
        builder.AppendLine($"问题统计：错误 {report.ErrorCount}，警告 {report.WarningCount}，信息 {report.InfoCount}；Mod {report.ModCount} 个。");
        if (report.FixActions.Count == 0) {
            builder.Append("没有需要处理的问题。");
            return builder.ToString();
        }
        builder.AppendLine("修复建议：");
        foreach (var action in report.FixActions) {
            builder.AppendLine($"- {(action.CanAutoFix ? "[可自动]" : "[手动]")} {action.Title}：{action.Detail}");
        }
        return builder.ToString().TrimEnd();
    }

    private static string Describe(int errors, int warnings, string scope) {
        if (errors == 0 && warnings == 0) return scope + "正常。";
        var parts = new List<string>();
        if (errors > 0) parts.Add($"{errors} 个错误");
        if (warnings > 0) parts.Add($"{warnings} 个警告");
        return $"{scope}存在 {string.Join("、", parts)}。";
    }

    private static List<InstanceFixAction> BuildFixActions(InstanceCheckResult check) {
        var actions = new List<InstanceFixAction>();
        var messages = check.Issues.Select(issue => issue.Message).ToList();
        var modMessages = check.ModIssues.Select(issue => issue.Message).ToList();

        var needsRepair = messages.Any(message =>
            message.Contains("缺少版本 JSON", StringComparison.Ordinal)
            || message.Contains("版本 JSON 解析失败", StringComparison.Ordinal)
            || message.Contains("客户端 JAR", StringComparison.Ordinal)
            || message.Contains("缺少", StringComparison.Ordinal) && message.Contains("libraries", StringComparison.Ordinal)
            || message.Contains("缺少资源索引", StringComparison.Ordinal)
            || message.Contains("缺少 assetIndex", StringComparison.Ordinal));
        if (needsRepair) {
            actions.Add(new InstanceFixAction {
                Title = "修复基础文件",
                Detail = "重新下载缺失的版本 JSON、客户端 JAR、libraries 与资源索引。",
                Kind = InstanceFixKind.RepairBaseFiles,
                CanAutoFix = true
            });
        }

        if (messages.Any(message => message.Contains("非 ASCII", StringComparison.Ordinal))) {
            actions.Add(new InstanceFixAction {
                Title = "迁移到英文路径",
                Detail = "实例路径含中文或其它非 ASCII 字符，建议迁移到纯英文目录。",
                Kind = InstanceFixKind.MigrateAsciiPath
            });
        }

        if (messages.Any(message => message.Contains("磁盘剩余空间不足", StringComparison.Ordinal))) {
            actions.Add(new InstanceFixAction {
                Title = "清理磁盘空间",
                Detail = "所在磁盘可用空间不足 2 GB，下载或更新可能失败。",
                Kind = InstanceFixKind.FreeDiskSpace
            });
        }

        if (modMessages.Any(message => message.Contains("重复的 Mod ID", StringComparison.Ordinal)
                                       || message.Contains("哈希重复", StringComparison.Ordinal)
                                       || message.Contains("无法启动", StringComparison.Ordinal))) {
            actions.Add(new InstanceFixAction {
                Title = "处理重复或冲突的 Mod",
                Detail = "存在重复 Mod ID、相同文件或加载器混装，建议禁用其中一份。",
                Kind = InstanceFixKind.ResolveModConflicts
            });
        }

        var missingDependencies = modMessages
            .Where(message => message.StartsWith("缺少前置 Mod：", StringComparison.Ordinal))
            .Select(message => message.Substring("缺少前置 Mod：".Length).Trim())
            .Where(id => id.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (missingDependencies.Count > 0) {
            actions.Add(new InstanceFixAction {
                Title = "补装前置 Mod",
                Detail = "缺少：" + string.Join("、", missingDependencies.Take(8))
                         + (missingDependencies.Count > 8 ? $" 等 {missingDependencies.Count} 个" : "") + "。",
                Kind = InstanceFixKind.InstallMissingMods
            });
        }

        if (actions.Count == 0 && check.Issues.Count > 0) {
            actions.Add(new InstanceFixAction {
                Title = "查看体检详情",
                Detail = "剩余问题需要人工判断，可运行「高级体检」或导出诊断包。",
                Kind = InstanceFixKind.Manual
            });
        }

        return actions;
    }

}

/// <summary>
/// 健康分历史：把每次评估追加到 <c>PCL/HealthHistory.json</c>，用于展示趋势。
/// </summary>
public static class InstanceHealthHistoryService {

    public const string FormatName = "pcl-health-history";
    public const int CurrentVersion = 1;

    /// <summary>单个实例最多保留的历史条数。</summary>
    public const int MaxEntriesPerInstance = 60;

    public static string GetDefaultPath(string pclFolder) => Path.Combine(pclFolder, "HealthHistory.json");

    /// <summary>追加一条记录；同一实例超出上限时丢弃最旧的记录。</summary>
    public static IReadOnlyList<InstanceHealthHistoryEntry> Append(
        string historyPath,
        InstanceHealthReport report,
        int maxEntries = MaxEntriesPerInstance) {
        ArgumentNullException.ThrowIfNull(report);
        var entries = Load(historyPath).ToList();
        entries.Add(new InstanceHealthHistoryEntry {
            Instance = report.InstanceName,
            Score = report.Score,
            Grade = report.Grade,
            Errors = report.ErrorCount,
            Warnings = report.WarningCount,
            EvaluatedUtc = report.EvaluatedUtc
        });

        var trimmed = new List<InstanceHealthHistoryEntry>();
        foreach (var group in entries.GroupBy(entry => entry.Instance, StringComparer.OrdinalIgnoreCase)) {
            trimmed.AddRange(group.OrderByDescending(entry => entry.EvaluatedUtc).Take(Math.Max(1, maxEntries)));
        }
        trimmed = trimmed.OrderBy(entry => entry.EvaluatedUtc).ToList();
        Save(historyPath, trimmed);
        return trimmed;
    }

    public static IReadOnlyList<InstanceHealthHistoryEntry> Load(string historyPath) {
        if (!File.Exists(historyPath)) return [];
        try {
            var json = JObject.Parse(File.ReadAllText(historyPath));
            if (!string.Equals(json["format"]?.ToString(), FormatName, StringComparison.OrdinalIgnoreCase)) return [];
            if (json["entries"] is not JArray entries) return [];
            var result = new List<InstanceHealthHistoryEntry>();
            foreach (var item in entries.OfType<JObject>()) {
                var score = item["score"]?.ToObject<int>() ?? 0;
                result.Add(new InstanceHealthHistoryEntry {
                    Instance = item["instance"]?.ToString() ?? "",
                    Score = score,
                    Grade = Enum.TryParse<InstanceHealthGrade>(item["grade"]?.ToString(), true, out var grade)
                        ? grade
                        : InstanceHealthService.GradeOf(score),
                    Errors = item["errors"]?.ToObject<int>() ?? 0,
                    Warnings = item["warnings"]?.ToObject<int>() ?? 0,
                    EvaluatedUtc = DateTime.TryParse(item["evaluatedUtc"]?.ToString(), null,
                        System.Globalization.DateTimeStyles.RoundtripKind, out var time)
                        ? time.ToUniversalTime()
                        : DateTime.UtcNow
                });
            }
            return result.OrderBy(entry => entry.EvaluatedUtc).ToList();
        } catch {
            return [];
        }
    }

    /// <summary>取某个实例最近的记录（按时间正序返回）。</summary>
    public static IReadOnlyList<InstanceHealthHistoryEntry> GetTrend(string historyPath, string instanceName, int take = 10) {
        var all = Load(historyPath)
            .Where(entry => string.Equals(entry.Instance, instanceName, StringComparison.OrdinalIgnoreCase))
            .OrderBy(entry => entry.EvaluatedUtc)
            .ToList();
        return take > 0 && all.Count > take ? all.Skip(all.Count - take).ToList() : all;
    }

    /// <summary>格式化趋势，例如「62 → 75 → 88（↑26）」。</summary>
    public static string FormatTrend(IReadOnlyList<InstanceHealthHistoryEntry> entries) {
        if (entries is null || entries.Count == 0) return "还没有历史记录。";
        var scores = string.Join(" → ", entries.Select(entry => entry.Score));
        if (entries.Count == 1) return scores;
        var delta = entries[entries.Count - 1].Score - entries[0].Score;
        var arrow = delta > 0 ? "↑" + delta : delta < 0 ? "↓" + Math.Abs(delta) : "→ 0";
        return $"{scores}（{arrow}）";
    }

    private static void Save(string historyPath, IReadOnlyList<InstanceHealthHistoryEntry> entries) {
        var directory = Path.GetDirectoryName(Path.GetFullPath(historyPath));
        if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
        var json = new JObject {
            ["format"] = FormatName,
            ["version"] = CurrentVersion,
            ["entries"] = new JArray(entries.Select(entry => new JObject {
                ["instance"] = entry.Instance,
                ["score"] = entry.Score,
                ["grade"] = entry.Grade.ToString(),
                ["errors"] = entry.Errors,
                ["warnings"] = entry.Warnings,
                ["evaluatedUtc"] = entry.EvaluatedUtc.ToString("O")
            }))
        }.ToString(Newtonsoft.Json.Formatting.Indented);

        var temp = historyPath + ".tmp";
        File.WriteAllText(temp, json, new UTF8Encoding(false));
        File.Copy(temp, historyPath, true);
        File.Delete(temp);
    }

}
