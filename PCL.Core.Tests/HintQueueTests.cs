using PCL.Core.Ui;

namespace PCL.Core.Tests;

public class HintQueueTests {

    [Test]
    public async Task 多行文本压缩为一行() {
        await Assert.That(HintQueue.Normalize("第一行\r\n第二行\n第三行")).IsEqualTo("第一行 第二行 第三行");
        await Assert.That(HintQueue.Normalize("  多余   空白  ")).IsEqualTo("多余 空白");
        await Assert.That(HintQueue.Normalize(null)).IsEqualTo("");
    }

    [Test]
    public async Task 空提示被丢弃() {
        var queue = new HintQueue();
        var result = queue.Enqueue("   ");
        await Assert.That(result.Outcome).IsEqualTo(HintOutcome.Dropped);
        await Assert.That(queue.Visible.Count).IsEqualTo(0);
    }

    [Test]
    public async Task 相同内容刷新而不新增() {
        var queue = new HintQueue();
        var first = queue.Enqueue("保存成功");
        var second = queue.Enqueue("  保存成功  ");
        await Assert.That(first.Outcome).IsEqualTo(HintOutcome.Added);
        await Assert.That(second.Outcome).IsEqualTo(HintOutcome.Refreshed);
        await Assert.That(queue.Visible.Count).IsEqualTo(1);
        await Assert.That(second.Entry!.Revision).IsEqualTo(1);
    }

    [Test]
    public async Task 超出上限的提示被丢弃() {
        var queue = new HintQueue();
        for (var i = 0; i < HintQueue.MaxVisible; i++) {
            await Assert.That(queue.Enqueue("提示 " + i).Outcome).IsEqualTo(HintOutcome.Added);
        }
        var overflow = queue.Enqueue("第 21 条");
        await Assert.That(overflow.Outcome).IsEqualTo(HintOutcome.Dropped);
        await Assert.That(queue.Visible.Count).IsEqualTo(HintQueue.MaxVisible);
    }

    [Test]
    public async Task 显示时长按文本长度增长并钳制() {
        var shortText = HintQueue.ComputeDisplayMilliseconds("短");
        var longText = HintQueue.ComputeDisplayMilliseconds(new string('字', 40));
        await Assert.That(shortText).IsEqualTo(800 + 5 * 180);
        await Assert.That(longText).IsEqualTo(800 + 23 * 180);
        await Assert.That(longText).IsGreaterThan(shortText);
    }

    [Test]
    public async Task 动画速度影响显示时长() {
        var normal = HintQueue.ComputeDisplayMilliseconds("测试提示");
        var fast = HintQueue.ComputeDisplayMilliseconds("测试提示", 2);
        await Assert.That(fast).IsEqualTo(normal * 2);
    }

    [Test]
    public async Task 移除后可以继续添加() {
        var queue = new HintQueue();
        var result = queue.Enqueue("提示");
        await Assert.That(queue.Remove(result.Entry)).IsTrue();
        await Assert.That(queue.Visible.Count).IsEqualTo(0);
        await Assert.That(queue.Enqueue("提示").Outcome).IsEqualTo(HintOutcome.Added);
    }

    [Test]
    public async Task 清空队列() {
        var queue = new HintQueue();
        queue.Enqueue("a");
        queue.Enqueue("b");
        queue.Clear();
        await Assert.That(queue.Visible.Count).IsEqualTo(0);
    }

    [Test]
    public async Task 保留提示类型与日志标记() {
        var queue = new HintQueue();
        var result = queue.Enqueue("出错了", HintKind.Red, log: false);
        await Assert.That(result.Entry!.Kind).IsEqualTo(HintKind.Red);
        await Assert.That(result.Entry.Log).IsFalse();
    }

}
