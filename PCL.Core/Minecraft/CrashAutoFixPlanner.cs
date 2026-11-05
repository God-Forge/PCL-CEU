namespace PCL.Core.Minecraft;

/// <summary>修复动作的类型。</summary>
public enum CrashFixKind {
    /// <summary>可以自动应用（例如追加 JVM 参数）。</summary>
    AutoApplicable,
    /// <summary>只能提示用户手动处理。</summary>
    SuggestOnly
}

/// <summary>一条修复动作。</summary>
public sealed class CrashAutoFixAction {

    /// <summary>触发该动作的崩溃原因。</summary>
    public string Reason { get; init; } = "";

    public CrashFixKind Kind { get; init; }

    /// <summary>给用户看的说明。</summary>
    public string Description { get; init; } = "";

    /// <summary>需要追加到实例 JVM 参数里的内容（自动类才有）。</summary>
    public string? JvmArgument { get; init; }

}

/// <summary>
/// 把崩溃诊断结果转成修复动作：能自动执行的（如增大线程栈）标记为可应用，
/// 其余保持提示性质。对应「诊断完让用户自己去治」到「一键修」的过渡。
/// </summary>
public static class CrashAutoFixPlanner {

    /// <summary>堆栈溢出时追加的参数。</summary>
    public const string StackOverflowArgument = "-Xss4m";

    public static IReadOnlyList<CrashAutoFixAction> Plan(CrashAnalysisReport? report) {
        if (report is null || !report.HasFindings) return [];
        var actions = new List<CrashAutoFixAction>();
        foreach (var diagnosis in report.Diagnoses) {
            var argument = diagnosis.Reason switch {
                "堆栈溢出" => StackOverflowArgument,
                _ => null
            };
            actions.Add(new CrashAutoFixAction {
                Reason = diagnosis.Reason,
                Kind = argument is null ? CrashFixKind.SuggestOnly : CrashFixKind.AutoApplicable,
                Description = argument is null
                    ? diagnosis.Suggestion ?? "请根据日志手动排查。"
                    : $"在实例 JVM 参数中追加 {argument}，并以同样的参数重试启动。",
                JvmArgument = argument
            });
        }
        return actions;
    }

    /// <summary>是否存在可自动应用的动作。</summary>
    public static bool HasAutoApplicable(IReadOnlyList<CrashAutoFixAction>? actions) =>
        actions?.Any(action => action.Kind == CrashFixKind.AutoApplicable) == true;

    /// <summary>把可自动应用的参数合并成一段，已有相同参数时不重复追加。</summary>
    public static string MergeArguments(string? existing, IEnumerable<CrashAutoFixAction> actions) {
        var current = existing ?? "";
        var additions = actions
            .Where(action => action.Kind == CrashFixKind.AutoApplicable && !string.IsNullOrWhiteSpace(action.JvmArgument))
            .Select(action => action.JvmArgument!.Trim())
            .Where(argument => !current.Contains(argument, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (additions.Count == 0) return current;
        return string.IsNullOrWhiteSpace(current)
            ? string.Join(" ", additions)
            : current.TrimEnd() + " " + string.Join(" ", additions);
    }

}
