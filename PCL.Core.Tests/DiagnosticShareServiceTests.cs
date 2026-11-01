using PCL.Core.Minecraft;

namespace PCL.Core.Tests;

public class DiagnosticShareServiceTests {

    [Test]
    public async Task 生成诊断摘要() {
        var text = DiagnosticShareService.Build("test", ["堆栈溢出"], -1073741819, ["ERROR example"]);
        await Assert.That(text).Contains("实例：test");
        await Assert.That(text).Contains("访问冲突");
        await Assert.That(text).Contains("ERROR example");
    }

    [Test]
    public async Task 无原因时显示未确定() {
        var text = DiagnosticShareService.Build("test", [], 0);
        await Assert.That(text).Contains("未确定");
    }

}
