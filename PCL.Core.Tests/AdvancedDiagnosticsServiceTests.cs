using PCL.Core.Download;
using PCL.Core.Minecraft;

namespace PCL.Core.Tests;

public class AdvancedDiagnosticsServiceTests {

    [Test]
    public async Task 对比两份日志() {
        var root = Path.Combine(Path.GetTempPath(), "pcl-compare-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try {
            var oldPath = Path.Combine(root, "old.log");
            var newPath = Path.Combine(root, "new.log");
            File.WriteAllLines(oldPath, ["a", "b", "c"]);
            File.WriteAllLines(newPath, ["a", "c", "d"]);
            var result = AdvancedDiagnosticsService.CompareLogs(oldPath, newPath);
            await Assert.That(result).Contains("仅旧报告出现");
            await Assert.That(result).Contains("b");
            await Assert.That(result).Contains("d");
        } finally {
            Directory.Delete(root, true);
        }
    }

    [Test]
    public async Task 记录并选择最快下载源() {
        var path = Path.Combine(Path.GetTempPath(), "pcl-sources-" + Guid.NewGuid().ToString("N") + ".json");
        try {
            var results = new[] {
                new DownloadSpeedResult { Name = "A", Success = true, ElapsedMilliseconds = 300 },
                new DownloadSpeedResult { Name = "B", Success = true, ElapsedMilliseconds = 100 }
            };
            AdvancedDiagnosticsService.RecordSourceSpeed(path, results);
            await Assert.That(AdvancedDiagnosticsService.LoadSourceHistory(path).Count).IsEqualTo(1);
            await Assert.That(AdvancedDiagnosticsService.ChooseFastest(results)).IsEqualTo("B");
        } finally {
            File.Delete(path);
        }
    }

    [Test]
    public async Task 生成失败建议() {
        var text = AdvancedDiagnosticsService.BuildFailureAdvice(-1073741819, ["堆栈溢出"]);
        await Assert.That(text).Contains("访问冲突");
        await Assert.That(text).Contains("Xss4m");
    }

}
