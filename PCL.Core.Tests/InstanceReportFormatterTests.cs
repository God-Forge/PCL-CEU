using PCL.Core.Minecraft;
using PCL.Core.Mods;

namespace PCL.Core.Tests;

public class InstanceReportFormatterTests {

    private static InstanceCheckIssue CheckIssue(InstanceCheckSeverity severity, string message) =>
        new() { Severity = severity, Message = message };

    private static ModIssue ModIssue(ModIssueSeverity severity, string file, string message) =>
        new() { Severity = severity, FileName = file, Message = message };

    [Test]
    public async Task 无问题时给出友好文本() {
        var result = new InstanceCheckResult { Issues = [], ModIssues = [] };
        await Assert.That(InstanceReportFormatter.FormatCheck(result)).IsEqualTo("未发现明显问题。");
    }

    [Test]
    public async Task 合并实例与Mod问题() {
        var result = new InstanceCheckResult {
            Issues = [CheckIssue(InstanceCheckSeverity.Error, "客户端 JAR 缺失")],
            ModIssues = [ModIssue(ModIssueSeverity.Warning, "a.jar", "缺少依赖")]
        };
        var text = InstanceReportFormatter.FormatCheck(result);
        await Assert.That(text).Contains("[错误] 客户端 JAR 缺失");
        await Assert.That(text).Contains("[Mod/警告] a.jar：缺少依赖");
    }

    [Test]
    public async Task 超过上限时提示剩余条数() {
        var issues = Enumerable.Range(0, 35)
            .Select(index => CheckIssue(InstanceCheckSeverity.Warning, "问题" + index))
            .ToList();
        var text = InstanceReportFormatter.FormatCheck(new InstanceCheckResult { Issues = issues });
        await Assert.That(text).Contains("问题0");
        await Assert.That(text).Contains("另有 5 条问题");
        await Assert.That(text).DoesNotContain("问题34");
    }

    [Test]
    public async Task Mod扫描无问题时的文本() {
        var result = new ModScanResult { Mods = [new ModInfo { FileName = "a.jar" }], Issues = [] };
        await Assert.That(InstanceReportFormatter.FormatModScan(result)).IsEqualTo("共扫描 1 个 Mod，未发现明显问题。");
    }

    [Test]
    public async Task Mod扫描带问题时包含总数() {
        var result = new ModScanResult {
            Mods = [new ModInfo { FileName = "a.jar" }, new ModInfo { FileName = "b.jar" }],
            Issues = [ModIssue(ModIssueSeverity.Error, "b.jar", "加载器冲突")]
        };
        var text = InstanceReportFormatter.FormatModScan(result);
        await Assert.That(text).Contains("共扫描 2 个 Mod");
        await Assert.That(text).Contains("[错误] b.jar：加载器冲突");
    }

    [Test]
    public async Task 严重度映射() {
        await Assert.That(InstanceReportFormatter.SeverityText(InstanceCheckSeverity.Error)).IsEqualTo("错误");
        await Assert.That(InstanceReportFormatter.SeverityText(InstanceCheckSeverity.Warning)).IsEqualTo("警告");
        await Assert.That(InstanceReportFormatter.SeverityText(InstanceCheckSeverity.Info)).IsEqualTo("信息");
        await Assert.That(InstanceReportFormatter.SeverityText(ModIssueSeverity.Error)).IsEqualTo("错误");
    }

    [Test]
    public async Task 空结果不抛异常() {
        await Assert.That(InstanceReportFormatter.FormatCheck(null)).Contains("未获取");
        await Assert.That(InstanceReportFormatter.FormatModScan(null)).Contains("未获取");
    }

}
