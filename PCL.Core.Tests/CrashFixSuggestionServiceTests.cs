using PCL.Core.Minecraft;

namespace PCL.Core.Tests;

public class CrashFixSuggestionServiceTests {

    [Test]
    public async Task 已知原因返回修复建议() {
        await Assert.That(CrashFixSuggestionService.GetSuggestion("堆栈溢出")).Contains("Xss4m");
        await Assert.That(CrashFixSuggestionService.GetSuggestion("启动参数过长")).Contains("命令行");
    }

    [Test]
    public async Task 未知原因返回空() {
        await Assert.That(CrashFixSuggestionService.GetSuggestion("unknown")).IsNull();
    }

}
