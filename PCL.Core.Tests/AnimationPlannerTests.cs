using PCL.Core.Ui;

namespace PCL.Core.Tests;

public class AnimationPlannerTests {

    private static AnimationStep Step(int duration, int delay = 0) => new() {
        Property = "Opacity",
        To = 1,
        DurationMilliseconds = duration,
        DelayMilliseconds = delay
    };

    [Test]
    public async Task 顺序动画首尾相接() {
        var scheduled = AnimationPlanner.Sequential([Step(200), Step(300)]);
        await Assert.That(scheduled[0].BeginMilliseconds).IsEqualTo(0);
        await Assert.That(scheduled[0].EndMilliseconds).IsEqualTo(200);
        await Assert.That(scheduled[1].BeginMilliseconds).IsEqualTo(200);
        await Assert.That(scheduled[1].EndMilliseconds).IsEqualTo(500);
    }

    [Test]
    public async Task 顺序动画叠加各自延迟() {
        var scheduled = AnimationPlanner.Sequential([Step(100, delay: 50), Step(100, delay: 25)]);
        await Assert.That(scheduled[0].BeginMilliseconds).IsEqualTo(50);
        await Assert.That(scheduled[0].EndMilliseconds).IsEqualTo(150);
        await Assert.That(scheduled[1].BeginMilliseconds).IsEqualTo(175);
    }

    [Test]
    public async Task 并行动画同时开始() {
        var scheduled = AnimationPlanner.Parallel([Step(200), Step(400)]);
        await Assert.That(scheduled[0].BeginMilliseconds).IsEqualTo(0);
        await Assert.That(scheduled[1].BeginMilliseconds).IsEqualTo(0);
        await Assert.That(scheduled[1].EndMilliseconds).IsEqualTo(400);
    }

    [Test]
    public async Task 总时长取最晚结束时间() {
        var scheduled = AnimationPlanner.Parallel([Step(200), Step(400, delay: 100)]);
        await Assert.That(AnimationPlanner.TotalDuration(scheduled)).IsEqualTo(500);
    }

    [Test]
    public async Task 空集合时总时长为零() {
        await Assert.That(AnimationPlanner.TotalDuration(AnimationPlanner.Sequential([]))).IsEqualTo(0);
    }

    [Test]
    public async Task 负的时长与延迟被钳制为零() {
        var scheduled = AnimationPlanner.Sequential([Step(-100, delay: -20)]);
        await Assert.That(scheduled[0].BeginMilliseconds).IsEqualTo(0);
        await Assert.That(scheduled[0].EndMilliseconds).IsEqualTo(0);
    }

    [Test]
    public async Task 步骤保留原始描述() {
        var step = new AnimationStep { Property = "Scale", To = 1.5, Easing = EasingKind.BackOut, EasingPower = 2 };
        var scheduled = AnimationPlanner.Parallel([step]);
        await Assert.That(scheduled[0].Step.Property).IsEqualTo("Scale");
        await Assert.That(scheduled[0].Step.Easing).IsEqualTo(EasingKind.BackOut);
        await Assert.That(scheduled[0].Step.To).IsEqualTo(1.5);
    }

}
