using PCL.Core.Minecraft;

namespace PCL.Core.Tests;

public class CrashHistoryServiceTests {

    private static string CreatePath() {
        return Path.Combine(Path.GetTempPath(), "pcl-crashhistory-" + Guid.NewGuid().ToString("N") + ".json");
    }

    [Test]
    public async Task 保存并读取崩溃历史() {
        var path = CreatePath();
        try {
            CrashHistoryService.Add(path, new CrashHistoryEntry {
                Instance = "test",
                ExitCode = -1073741819,
                Reasons = ["堆栈溢出"],
                Summary = "summary"
            });
            var latest = CrashHistoryService.Latest(path);
            await Assert.That(latest).IsNotNull();
            await Assert.That(latest!.Instance).IsEqualTo("test");
            await Assert.That(latest.ExitCode).IsEqualTo(-1073741819);
            await Assert.That(CrashHistoryService.Format(path)).Contains("访问冲突");
        } finally {
            File.Delete(path);
        }
    }

    [Test]
    public async Task 空文件返回空记录() {
        await Assert.That(CrashHistoryService.List(CreatePath()).Count).IsEqualTo(0);
    }

}
