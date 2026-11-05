namespace PCL.Core.Minecraft;

/// <summary>单条崩溃诊断：原因、日志细节与修复建议。</summary>
public sealed class CrashDiagnosis {

    public string Reason { get; init; } = "";
    public string? Detail { get; init; }
    public string? Suggestion { get; init; }

}

/// <summary>崩溃分析报告。</summary>
public sealed class CrashAnalysisReport {

    public IReadOnlyList<CrashDiagnosis> Diagnoses { get; init; } = [];
    public bool HasFindings => Diagnoses.Count > 0;

    /// <summary>没有发现任何问题时给出的说明。</summary>
    public string EmptyMessage { get; init; } = "没有在日志中匹配到已知的崩溃特征。";

}

/// <summary>
/// 崩溃分析的编排层：把日志文本交给规则分析器，再为每条原因配上修复建议。
/// 对应 VB 版 ModCrash 的 Analyze / Output 两个阶段。
/// </summary>
public static class CrashAnalysisService {

    /// <summary>analyze the provided log contents.</summary>
    public static CrashAnalysisReport Analyze(
        string? logMc = null,
        string? logCrash = null,
        string? logHs = null,
        string? logDebug = null) {
        var findings = CrashPatternAnalyzer.Analyze(logMc, logCrash, logHs, logDebug);
        return Build(findings);
    }

    /// <summary>
    /// 直接读取文件分析。文件不存在或读取失败时返回空报告，不抛异常。
    /// </summary>
    public static CrashAnalysisReport AnalyzeFiles(IEnumerable<string?> paths) {
        var contents = new List<string>();
        foreach (var path in paths) {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) continue;
            try {
                contents.Add(File.ReadAllText(path));
            } catch (Exception ex) when (ex is not OutOfMemoryException) {
                Logger.Warn(ex, "读取崩溃日志失败：" + path);
            }
        }
        var joined = contents.Count == 1 ? contents[0] : string.Join("\n", contents);
        return Analyze(joined, joined, joined, joined);
    }

    /// <summary>从实例目录里挑选最可能包含崩溃信息的日志（latest.log 与最近的崩溃报告）。</summary>
    public static IReadOnlyList<string> LocateCrashLogs(string instanceFolder) {
        var result = new List<string>();
        if (string.IsNullOrWhiteSpace(instanceFolder)) return result;
        var latest = Path.Combine(instanceFolder, "logs", "latest.log");
        if (File.Exists(latest)) result.Add(latest);
        var crashReports = Path.Combine(instanceFolder, "crash-reports");
        if (Directory.Exists(crashReports)) {
            foreach (var file in Directory.EnumerateFiles(crashReports, "*.txt")
                         .OrderByDescending(File.GetLastWriteTimeUtc)
                         .Take(3)) {
                result.Add(file);
            }
        }
        return result;
    }

    /// <summary>把报告格式化成可读文本。</summary>
    public static string Format(CrashAnalysisReport? report) {
        if (report is null) return "未获取到分析结果。";
        if (!report.HasFindings) return report.EmptyMessage;
        var lines = new List<string>();
        var index = 1;
        foreach (var diagnosis in report.Diagnoses) {
            lines.Add($"{index}. {diagnosis.Reason}");
            if (!string.IsNullOrWhiteSpace(diagnosis.Detail)) {
                lines.Add("   细节：" + diagnosis.Detail!.Trim());
            }
            if (!string.IsNullOrWhiteSpace(diagnosis.Suggestion)) {
                lines.Add("   建议：" + diagnosis.Suggestion);
            } else {
                lines.Add("   建议：未收录该问题的修复建议，可查看日志或前往社区求助。");
            }
            index++;
        }
        return string.Join(Environment.NewLine, lines);
    }

    private static CrashAnalysisReport Build(IReadOnlyList<CrashFinding> findings) {
        if (findings.Count == 0) return new CrashAnalysisReport();
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var diagnoses = new List<CrashDiagnosis>();
        foreach (var finding in findings) {
            // 同一原因可能被多条规则命中，只保留第一条出现的细节
            if (!seen.Add(finding.Reason)) continue;
            diagnoses.Add(new CrashDiagnosis {
                Reason = finding.Reason,
                Detail = finding.Detail,
                Suggestion = CrashFixSuggestionService.GetSuggestion(finding.Reason)
            });
        }
        return new CrashAnalysisReport { Diagnoses = diagnoses };
    }

}
