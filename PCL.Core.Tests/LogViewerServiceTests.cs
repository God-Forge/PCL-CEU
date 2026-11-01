using PCL.Core.Minecraft;

namespace PCL.Core.Tests;

public class LogViewerServiceTests {

    [Test]
    public async Task 提取错误行() {
        var root = Path.Combine(Path.GetTempPath(), "pcl-logview-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Path.Combine(root, "logs"));
        try {
            File.WriteAllLines(Path.Combine(root, "logs", "latest.log"), new[] {
                "[12:00:00] [main/INFO] normal",
                "[12:00:01] [main/ERROR] something failed",
                "[12:00:02] [main/INFO] Caused by: example"
            });
            var result = LogViewerService.ExtractLatest(root);
            await Assert.That(result.Lines.Count).IsEqualTo(2);
            await Assert.That(result.Lines.Any(line => line.Contains("ERROR"))).IsTrue();
            await Assert.That(LogViewerService.Format(result)).Contains("something failed");
        } finally {
            Directory.Delete(root, true);
        }
    }

    [Test]
    public async Task 缺少日志时返回空() {
        var result = LogViewerService.ExtractLatest(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        await Assert.That(result.Lines.Count).IsEqualTo(0);
    }

}
