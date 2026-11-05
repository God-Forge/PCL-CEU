namespace PCL.Core.Mods;

/// <summary>Mod 问题的汇总视图。</summary>
public sealed record ModIssueSummary(int Total, int Errors, IReadOnlyList<ModIssue> Highlights, int Remaining) {

    public bool HasIssues => Total > 0;

    /// <summary>建议展示的主题色：有错误偏红，只有警告偏黄。</summary>
    public bool HasErrors => Errors > 0;

}

/// <summary>
/// 把扫描得到的问题整理成界面要展示的摘要：总数、错误数、前若干条与剩余条数。
/// </summary>
public static class ModIssueSummarizer {

    /// <summary>默认在界面上直接展示的条目数。</summary>
    public const int DefaultHighlightCount = 5;

    public static ModIssueSummary Summarize(IEnumerable<ModIssue>? issues, int highlightCount = DefaultHighlightCount) {
        if (issues is null) return new ModIssueSummary(0, 0, [], 0);
        var list = issues.ToList();
        if (list.Count == 0) return new ModIssueSummary(0, 0, [], 0);
        var take = Math.Clamp(highlightCount, 0, list.Count);
        return new ModIssueSummary(
            list.Count,
            list.Count(issue => issue.Severity == ModIssueSeverity.Error),
            list.Take(take).ToList(),
            list.Count - take);
    }

}
