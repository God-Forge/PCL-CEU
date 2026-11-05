using PCL.Core.Minecraft;

namespace PCL.Core.Tests;

public class CrashAnalysisServiceTests {

    private const string StackOverflowLog = """
    [12:00:00] [Client thread/ERROR]: java.lang.StackOverflowError
        at net.minecraft.client.Minecraft.run(Minecraft.java:1)
    """;

    private const string NativeLog = """
    [12:00:00] [main/ERROR]: java.lang.UnsatisfiedLinkError: no lwjgl in java.library.path
    """;

    [Test]
    public async Task 空日志没有发现() {
        var report = CrashAnalysisService.Analyze(null, null, null, null);
        await Assert.That(report.HasFindings).IsFalse();
        await Assert.That(CrashAnalysisService.Format(report)).Contains("没有在日志中匹配到");
    }

    [Test]
    public async Task 识别堆栈溢出并给出建议() {
        var report = CrashAnalysisService.Analyze(StackOverflowLog);
        await Assert.That(report.HasFindings).IsTrue();
        var diagnosis = report.Diagnoses.First(item => item.Reason == "堆栈溢出");
        await Assert.That(diagnosis.Suggestion).IsNotNull();
        await Assert.That(diagnosis.Suggestion!).Contains("-Xss4m");
    }

    [Test]
    public async Task 识别原生库问题() {
        var report = CrashAnalysisService.Analyze(null, NativeLog);
        await Assert.That(report.Diagnoses.Any(item => item.Reason == "原生库加载失败")).IsTrue();
    }

    [Test]
    public async Task 多个问题按出现顺序输出() {
        var report = CrashAnalysisService.Analyze(StackOverflowLog + Environment.NewLine + NativeLog);
        await Assert.That(report.Diagnoses.Count).IsGreaterThanOrEqualTo(2);
        var text = CrashAnalysisService.Format(report);
        await Assert.That(text).Contains("堆栈溢出");
        await Assert.That(text).Contains("原生库加载失败");
        await Assert.That(text).Contains("1.");
    }

    [Test]
    public async Task 同一原因不会重复列出() {
        var doubled = StackOverflowLog + Environment.NewLine + StackOverflowLog;
        var report = CrashAnalysisService.Analyze(doubled, doubled, doubled, null);
        await Assert.That(report.Diagnoses.Count(item => item.Reason == "堆栈溢出")).IsEqualTo(1);
    }

    [Test]
    public async Task 未收录建议的原因也能输出() {
        var report = new CrashAnalysisReport {
            Diagnoses = [new CrashDiagnosis { Reason = "未知问题", Detail = "detail" }]
        };
        var text = CrashAnalysisService.Format(report);
        await Assert.That(text).Contains("未知问题");
        await Assert.That(text).Contains("未收录");
    }

    [Test]
    public async Task 读取文件分析() {
        var path = Path.Combine(Path.GetTempPath(), "pcl-crash-" + Guid.NewGuid().ToString("N") + ".txt");
        try {
            File.WriteAllText(path, StackOverflowLog);
            var report = CrashAnalysisService.AnalyzeFiles([path]);
            await Assert.That(report.HasFindings).IsTrue();
        } finally {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Test]
    public async Task 文件不存在时返回空报告() {
        var report = CrashAnalysisService.AnalyzeFiles([Path.Combine(Path.GetTempPath(), "no-such-crash.txt"), null]);
        await Assert.That(report.HasFindings).IsFalse();
    }

    [Test]
    public async Task 定位实例内的崩溃日志() {
        var root = Path.Combine(Path.GetTempPath(), "pcl-loc-" + Guid.NewGuid().ToString("N"));
        try {
            Directory.CreateDirectory(Path.Combine(root, "logs"));
            Directory.CreateDirectory(Path.Combine(root, "crash-reports"));
            File.WriteAllText(Path.Combine(root, "logs", "latest.log"), "log");
            File.WriteAllText(Path.Combine(root, "crash-reports", "crash-1.txt"), "crash");
            var logs = CrashAnalysisService.LocateCrashLogs(root);
            await Assert.That(logs.Count).IsEqualTo(2);
            await Assert.That(logs[0]).Contains("latest.log");
        } finally {
            if (Directory.Exists(root)) Directory.Delete(root, true);
        }
    }

    [Test]
    public async Task 没有日志目录时返回空列表() {
        await Assert.That(CrashAnalysisService.LocateCrashLogs(Path.Combine(Path.GetTempPath(), "pcl-none-" + Guid.NewGuid().ToString("N"))).Count).IsEqualTo(0);
        await Assert.That(CrashAnalysisService.LocateCrashLogs("").Count).IsEqualTo(0);
    }

}
