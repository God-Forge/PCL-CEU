using PCL.Core.Minecraft;

namespace PCL.Core.Tests;

public class MinecraftLogRulesTests {

    [Test]
    public async Task 首行日志推进到第一阶段() {
        var progress = MinecraftLogRules.AdvanceProgress("[12:00:00] [main/INFO]: Loading Minecraft", MinecraftLaunchProgress.None);
        await Assert.That(progress).IsEqualTo(MinecraftLaunchProgress.FirstLog);
    }

    [Test]
    public async Task 按顺序推进启动阶段() {
        var progress = MinecraftLaunchProgress.FirstLog;
        progress = MinecraftLogRules.AdvanceProgress("[main/INFO]: Setting user: Steve", progress);
        await Assert.That(progress).IsEqualTo(MinecraftLaunchProgress.UserSet);
        progress = MinecraftLogRules.AdvanceProgress("[main/INFO]: LWJGL Version: 3.3.1", progress);
        await Assert.That(progress).IsEqualTo(MinecraftLaunchProgress.LwjglReady);
        progress = MinecraftLogRules.AdvanceProgress("OpenAL initialized.", progress);
        await Assert.That(progress).IsEqualTo(MinecraftLaunchProgress.OpenAlReady);
        progress = MinecraftLogRules.AdvanceProgress("Created: 1024x1024 textures/minecraft -atlas", progress);
        await Assert.That(progress).IsEqualTo(MinecraftLaunchProgress.TexturesReady);
    }

    [Test]
    public async Task 同一行最多推进一级() {
        // 同时包含用户设置与 LWJGL 信息时，只应推进到 UserSet
        var progress = MinecraftLogRules.AdvanceProgress("Setting user: Steve  lwjgl version 3.3.1", MinecraftLaunchProgress.FirstLog);
        await Assert.That(progress).IsEqualTo(MinecraftLaunchProgress.UserSet);
    }

    [Test]
    public async Task 阶段不会倒退() {
        var progress = MinecraftLogRules.AdvanceProgress("随便一行日志", MinecraftLaunchProgress.TexturesReady);
        await Assert.That(progress).IsEqualTo(MinecraftLaunchProgress.TexturesReady);
    }

    [Test]
    public async Task 识别正常关闭日志() {
        var signal = MinecraftLogRules.DetectSignal("[Render thread/INFO]: Someone is closing me!");
        await Assert.That(signal).IsEqualTo(MinecraftLogSignal.CloseRequested);
    }

    [Test]
    public async Task 识别崩溃日志() {
        await Assert.That(MinecraftLogRules.DetectSignal("This crash report has been saved to: C:\\mc\\crash-reports"))
            .IsEqualTo(MinecraftLogSignal.CrashDetected);
        await Assert.That(MinecraftLogRules.DetectSignal("[Server thread/ERROR]: Unable to launch"))
            .IsEqualTo(MinecraftLogSignal.CrashDetected);
    }

    [Test]
    public async Task 聊天行不参与判定() {
        var signal = MinecraftLogRules.DetectSignal("[CHAT] Someone is closing me!");
        await Assert.That(signal).IsEqualTo(MinecraftLogSignal.None);
    }

    [Test]
    public async Task 识别加载器窗口标题() {
        await Assert.That(MinecraftLogRules.IsLoaderWindowTitle("FML Loading")).IsTrue();
        await Assert.That(MinecraftLogRules.IsLoaderWindowTitle("Quilt Loader 0.20")).IsTrue();
        await Assert.That(MinecraftLogRules.IsLoaderWindowTitle("Minecraft 1.20.1")).IsFalse();
    }

    [Test]
    public async Task 窗口出现后进度补到百分之九十五() {
        await Assert.That(MinecraftLogRules.EstimateProgress(MinecraftLaunchProgress.FirstLog, true)).IsEqualTo(0.95);
        await Assert.That(MinecraftLogRules.EstimateProgress(MinecraftLaunchProgress.TexturesReady, false)).IsEqualTo(0.95);
        await Assert.That(MinecraftLogRules.EstimateProgress(MinecraftLaunchProgress.LwjglReady, false)).IsEqualTo(0.9);
    }

}
