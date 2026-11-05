using PCL.Core.Launch;

namespace PCL.Core.Tests;

public class LaunchStateMachineTests {

    [Test]
    public async Task 初始为等待状态() {
        var machine = new LaunchStateMachine();
        await Assert.That(machine.Phase).IsEqualTo(LaunchPhase.Waiting);
        await Assert.That(machine.Progress).IsEqualTo(0);
        await Assert.That(machine.IsActive).IsTrue();
    }

    [Test]
    public async Task 等待到加载并触发事件() {
        var machine = new LaunchStateMachine();
        var phases = new List<LaunchPhase>();
        machine.PhaseChanged += phase => phases.Add(phase);
        await Assert.That(machine.TryTransition(LaunchPhase.Loading, "正在检查 Java")).IsTrue();
        await Assert.That(machine.Phase).IsEqualTo(LaunchPhase.Loading);
        await Assert.That(machine.Message).IsEqualTo("正在检查 Java");
        await Assert.That(phases).IsEquivalentTo(new[] { LaunchPhase.Loading });
    }

    [Test]
    public async Task 相同状态不触发事件() {
        var machine = new LaunchStateMachine();
        var count = 0;
        machine.PhaseChanged += _ => count++;
        await Assert.That(machine.TryTransition(LaunchPhase.Waiting)).IsFalse();
        await Assert.That(count).IsEqualTo(0);
    }

    [Test]
    public async Task 完成后不能再回到加载() {
        var machine = new LaunchStateMachine();
        machine.TryTransition(LaunchPhase.Loading);
        await Assert.That(machine.Finish()).IsTrue();
        await Assert.That(machine.TryTransition(LaunchPhase.Loading)).IsFalse();
        await Assert.That(machine.Phase).IsEqualTo(LaunchPhase.Finished);
        await Assert.That(machine.Progress).IsEqualTo(1);
    }

    [Test]
    public async Task 失败后记录原因且不能自行恢复() {
        var machine = new LaunchStateMachine();
        machine.TryTransition(LaunchPhase.Loading);
        await Assert.That(machine.Fail("找不到 Java")).IsTrue();
        await Assert.That(machine.Phase).IsEqualTo(LaunchPhase.Failed);
        await Assert.That(machine.FailureReason).IsEqualTo("找不到 Java");
        await Assert.That(machine.IsActive).IsFalse();
        await Assert.That(machine.TryTransition(LaunchPhase.Loading)).IsFalse();
    }

    [Test]
    public async Task 重置后可重新启动() {
        var machine = new LaunchStateMachine();
        machine.TryTransition(LaunchPhase.Loading);
        machine.Fail("x");
        machine.Reset();
        await Assert.That(machine.Phase).IsEqualTo(LaunchPhase.Waiting);
        await Assert.That(machine.FailureReason).IsNull();
        await Assert.That(machine.Progress).IsEqualTo(0);
        await Assert.That(machine.TryTransition(LaunchPhase.Loading)).IsTrue();
    }

    [Test]
    public async Task 直接从等待阶段可以失败() {
        var machine = new LaunchStateMachine();
        await Assert.That(machine.Fail("实例不存在")).IsTrue();
    }

    [Test]
    public async Task 进度只在加载中有效且被钳制() {
        var machine = new LaunchStateMachine();
        await Assert.That(machine.ReportProgress(0.5)).IsFalse();
        machine.TryTransition(LaunchPhase.Loading);
        await Assert.That(machine.ReportProgress(0.5)).IsTrue();
        await Assert.That(machine.Progress).IsEqualTo(0.5);
        machine.ReportProgress(2.0);
        await Assert.That(machine.Progress).IsEqualTo(1);
        machine.ReportProgress(-1);
        await Assert.That(machine.Progress).IsEqualTo(0);
    }

    [Test]
    public async Task 进度文本() {
        var machine = new LaunchStateMachine();
        machine.TryTransition(LaunchPhase.Loading);
        machine.ReportProgress(0.42);
        await Assert.That(machine.ProgressText).IsEqualTo("42%");
        machine.Fail("x");
        await Assert.That(machine.ProgressText).IsEqualTo("已中止");
    }

    [Test]
    public async Task 完成时进度满() {
        var machine = new LaunchStateMachine();
        machine.TryTransition(LaunchPhase.Loading);
        machine.ReportProgress(0.3);
        machine.Finish();
        await Assert.That(machine.Progress).IsEqualTo(1);
        await Assert.That(machine.ProgressText).IsEqualTo("100%");
    }

    [Test]
    public async Task 等待状态重置进度与消息() {
        var machine = new LaunchStateMachine();
        machine.TryTransition(LaunchPhase.Loading);
        machine.ReportProgress(0.8, "加载中");
        machine.TryTransition(LaunchPhase.Waiting);
        await Assert.That(machine.Progress).IsEqualTo(0);
        await Assert.That(machine.Message).IsNull();
    }

    [Test]
    public async Task 失败原因为空时给默认文案() {
        var machine = new LaunchStateMachine();
        machine.Fail("");
        await Assert.That(machine.FailureReason).IsEqualTo("未知错误");
    }

}
