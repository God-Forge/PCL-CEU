using PCL.Core.Mods;

namespace PCL.Core.Minecraft;

/// <summary>
/// 把体检与 Mod 扫描结果格式化成可读文本。对应 VB 版 ModInstanceRepair 的
/// FormatCheck / FormatModScan。
/// </summary>
public static class InstanceReportFormatter {

    /// <summary>最多直接列出的问题条数。</summary>
    public const int MaxLines = 30;

    public static string SeverityText(InstanceCheckSeverity severity) => severity switch {
        InstanceCheckSeverity.Error => "错误",
        InstanceCheckSeverity.Warning => "警告",
        _ => "信息"
    };

    public static string SeverityText(ModIssueSeverity severity) => severity switch {
        ModIssueSeverity.Error => "错误",
        ModIssueSeverity.Warning => "警告",
        _ => "信息"
    };

    /// <summary>格式化实例体检结果：实例问题与 Mod 问题合并展示。</summary>
    public static string FormatCheck(InstanceCheckResult? result) {
        if (result is null) return "未获取到体检结果。";
        var lines = new List<string>();
        foreach (var issue in result.Issues) {
            lines.Add($"[{SeverityText(issue.Severity)}] {issue.Message}");
        }
        foreach (var issue in result.ModIssues) {
            lines.Add($"[Mod/{SeverityText(issue.Severity)}] {issue.FileName}：{issue.Message}");
        }
        return Compose(lines, "未发现明显问题。");
    }

    /// <summary>格式化 Mod 扫描结果。</summary>
    public static string FormatModScan(ModScanResult? result) {
        if (result is null) return "未获取到 Mod 扫描结果。";
        if (result.Issues.Count == 0) return $"共扫描 {result.Mods.Count} 个 Mod，未发现明显问题。";
        var lines = result.Issues
            .Select(issue => $"[{SeverityText(issue.Severity)}] {issue.FileName}：{issue.Message}")
            .ToList();
        var text = Compose(lines, "");
        return $"共扫描 {result.Mods.Count} 个 Mod。" + Environment.NewLine + text;
    }

    private static string Compose(IReadOnlyList<string> lines, string emptyText) {
        if (lines.Count == 0) return emptyText;
        var head = string.Join(Environment.NewLine, lines.Take(MaxLines));
        var rest = lines.Count - MaxLines;
        return rest > 0
            ? head + Environment.NewLine + $"... 另有 {rest} 条问题，请查看日志。"
            : head;
    }

}
