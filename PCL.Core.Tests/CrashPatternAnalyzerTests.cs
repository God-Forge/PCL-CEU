using PCL.Core.Minecraft;

namespace PCL.Core.Tests;

public class CrashPatternAnalyzerTests {

    private static IReadOnlyList<CrashFinding> Analyze(string text) {
        return CrashPatternAnalyzer.Analyze(text, null, null, null);
    }

    [Test]
    public async Task 识别堆栈溢出() {
        var findings = Analyze("java.lang.StackOverflowError\n\tat a.b.c");
        await Assert.That(findings.Any(finding => finding.Reason == "堆栈溢出")).IsTrue();
    }

    [Test]
    public async Task 识别原生库加载失败并提取细节() {
        var findings = Analyze("java.lang.UnsatisfiedLinkError: no lwjgl in java.library.path");
        var finding = findings.Single(finding => finding.Reason == "原生库加载失败");
        await Assert.That(finding.Detail).Contains("no lwjgl in java.library.path");
    }

    [Test]
    public async Task 识别文件访问失败() {
        var findings = Analyze("java.nio.file.AccessDeniedException: C:\\mc\\config\\a.toml");
        var finding = findings.Single(finding => finding.Reason == "文件访问失败");
        await Assert.That(finding.Detail).Contains("C:\\mc\\config\\a.toml");
    }

    [Test]
    public async Task 识别命令行过长() {
        var findings = Analyze("CreateProcess error=206, 文件名或扩展名太长。");
        await Assert.That(findings.Any(finding => finding.Reason == "启动参数过长")).IsTrue();
    }

    [Test]
    public async Task 识别区块损坏() {
        var findings = Analyze("Region file C:\\mc\\saves\\world\\region\\r.0.0.mca is missing");
        await Assert.That(findings.Any(finding => finding.Reason == "存档或区块损坏")).IsTrue();
    }

    [Test]
    public async Task 识别光影与资源包错误() {
        var findings = Analyze("Shader compilation failed: invalid program");
        await Assert.That(findings.Any(finding => finding.Reason == "资源包或光影加载失败")).IsTrue();
    }

    [Test]
    public async Task 识别API不兼容() {
        var findings = Analyze("java.lang.AbstractMethodError: net.example.Mod.init()V");
        var finding = findings.Single(finding => finding.Reason == "Mod版本或API不兼容");
        await Assert.That(finding.Detail).Contains("net.example.Mod.init()V");
    }

    [Test]
    public async Task 相同原因与细节只保留一条() {
        var findings = Analyze("java.lang.StackOverflowError\njava.lang.StackOverflowError");
        await Assert.That(findings.Count(finding => finding.Reason == "堆栈溢出")).IsEqualTo(1);
    }

}
