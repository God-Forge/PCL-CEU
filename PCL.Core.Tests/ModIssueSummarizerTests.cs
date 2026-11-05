using PCL.Core.Mods;

namespace PCL.Core.Tests;

public class ModIssueSummarizerTests {

    private static ModIssue Issue(ModIssueSeverity severity, string file, string message) =>
        new() { Severity = severity, FileName = file, Message = message };

    [Test]
    public async Task 空集合没有摘要() {
        var summary = ModIssueSummarizer.Summarize(null);
        await Assert.That(summary.HasIssues).IsFalse();
        var empty = ModIssueSummarizer.Summarize([]);
        await Assert.That(empty.Total).IsEqualTo(0);
        await Assert.That(empty.Highlights.Count).IsEqualTo(0);
    }

    [Test]
    public async Task 统计总数与错误数() {
        var summary = ModIssueSummarizer.Summarize([
            Issue(ModIssueSeverity.Error, "a.jar", "加载器冲突"),
            Issue(ModIssueSeverity.Warning, "b.jar", "缺少依赖"),
            Issue(ModIssueSeverity.Warning, "c.jar", "重复文件")
        ]);
        await Assert.That(summary.Total).IsEqualTo(3);
        await Assert.That(summary.Errors).IsEqualTo(1);
        await Assert.That(summary.HasErrors).IsTrue();
    }

    [Test]
    public async Task 只展示前若干条并记录剩余() {
        var issues = Enumerable.Range(0, 9)
            .Select(index => Issue(ModIssueSeverity.Warning, $"m{index}.jar", "警告"))
            .ToList();
        var summary = ModIssueSummarizer.Summarize(issues, 5);
        await Assert.That(summary.Highlights.Count).IsEqualTo(5);
        await Assert.That(summary.Remaining).IsEqualTo(4);
    }

    [Test]
    public async Task 条数少于上限时不产生剩余() {
        var summary = ModIssueSummarizer.Summarize([Issue(ModIssueSeverity.Info, "a.jar", "提示")], 5);
        await Assert.That(summary.Highlights.Count).IsEqualTo(1);
        await Assert.That(summary.Remaining).IsEqualTo(0);
    }

    [Test]
    public async Task 没有错误时标记为仅警告() {
        var summary = ModIssueSummarizer.Summarize([Issue(ModIssueSeverity.Warning, "a.jar", "缺少依赖")]);
        await Assert.That(summary.HasErrors).IsFalse();
        await Assert.That(summary.HasIssues).IsTrue();
    }

    [Test]
    public async Task 展示条数可被钳制() {
        var issues = Enumerable.Range(0, 3).Select(index => Issue(ModIssueSeverity.Warning, "x", "y")).ToList();
        await Assert.That(ModIssueSummarizer.Summarize(issues, -1).Highlights.Count).IsEqualTo(0);
        await Assert.That(ModIssueSummarizer.Summarize(issues, 99).Highlights.Count).IsEqualTo(3);
    }

}
