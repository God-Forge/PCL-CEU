using PCL.Core.Minecraft;

namespace PCL.Core.Tests;

public class MinecraftExitClassifierTests {

    [Test]
    public async Task 加载中退出判定为崩溃() {
        var verdict = MinecraftExitClassifier.Classify(MinecraftGameState.Loading, 0, 2023);
        await Assert.That(verdict).IsEqualTo(MinecraftExitVerdict.CrashedBeforeWindow);
    }

    [Test]
    public async Task 运行中非零退出判定为崩溃() {
        var verdict = MinecraftExitClassifier.Classify(MinecraftGameState.Running, 1, 2023);
        await Assert.That(verdict).IsEqualTo(MinecraftExitVerdict.CrashedAbnormal);
        await Assert.That(MinecraftExitClassifier.IsCrash(verdict)).IsTrue();
    }

    [Test]
    public async Task 运行中零退出判定为正常() {
        var verdict = MinecraftExitClassifier.Classify(MinecraftGameState.Running, 0, 2023);
        await Assert.That(verdict).IsEqualTo(MinecraftExitVerdict.Normal);
        await Assert.That(MinecraftExitClassifier.IsCrash(verdict)).IsFalse();
    }

    [Test]
    public async Task 旧版非零退出码不算崩溃() {
        var verdict = MinecraftExitClassifier.Classify(MinecraftGameState.Running, 1, 2011);
        await Assert.That(verdict).IsEqualTo(MinecraftExitVerdict.Normal);
    }

    [Test]
    public async Task 缺少发布年份时按现代版本处理() {
        var verdict = MinecraftExitClassifier.Classify(MinecraftGameState.Running, 2, null);
        await Assert.That(verdict).IsEqualTo(MinecraftExitVerdict.CrashedAbnormal);
    }

    [Test]
    public async Task 已被判定崩溃时保持崩溃() {
        var verdict = MinecraftExitClassifier.Classify(MinecraftGameState.Crashed, 0, 2023);
        await Assert.That(verdict).IsEqualTo(MinecraftExitVerdict.CrashedAbnormal);
    }

    [Test]
    public async Task 取消状态不视为崩溃() {
        var verdict = MinecraftExitClassifier.Classify(MinecraftGameState.Canceled, 1, 2023);
        await Assert.That(verdict).IsEqualTo(MinecraftExitVerdict.Normal);
    }

}
