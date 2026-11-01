using PCL.Core.Minecraft;

namespace PCL.Core.Tests;

public class ExitCodeAnalyzerTests {

    [Test]
    public async Task 识别常见退出码() {
        var accessViolation = ExitCodeAnalyzer.Analyze(-1073741819);
        await Assert.That(accessViolation).IsNotNull();
        await Assert.That(accessViolation!.Description).Contains("访问冲突");

        var stackOverflow = ExitCodeAnalyzer.Analyze(-1073741571);
        await Assert.That(stackOverflow!.Description).Contains("栈溢出");
    }

    [Test]
    public async Task 零与未知退出码返回空() {
        await Assert.That(ExitCodeAnalyzer.Analyze(0)).IsNull();
        await Assert.That(ExitCodeAnalyzer.Analyze(12345)).IsNull();
    }

}
