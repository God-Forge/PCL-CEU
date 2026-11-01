using System.Text;

namespace PCL.Core.Minecraft;

/// <summary>
/// 生成适合粘贴到聊天/Issue 的短诊断摘要。
/// </summary>
public static class DiagnosticShareService {

    public static string Build(
        string instanceName,
        IReadOnlyList<string> reasons,
        int exitCode,
        IReadOnlyList<string>? logLines = null) {
        var builder = new StringBuilder();
        builder.AppendLine("PCL-CEU 诊断摘要");
        builder.AppendLine("实例：" + instanceName);
        if (exitCode != 0) {
            var exit = ExitCodeAnalyzer.Analyze(exitCode);
            builder.AppendLine($"退出码：{exitCode}" + (exit is null ? "" : $"（{exit.Description}）"));
        }
        builder.AppendLine("崩溃原因：" + (reasons.Count == 0 ? "未确定" : string.Join("；", reasons)));
        if (logLines is { Count: > 0 }) {
            builder.AppendLine("最近日志：");
            foreach (var line in logLines.Take(12)) builder.AppendLine(line);
        }
        return builder.ToString().TrimEnd();
    }

}
