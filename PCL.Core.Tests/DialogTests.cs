using PCL.Core.Ui;

namespace PCL.Core.Tests;

public class DialogTests {

    private static DialogRequest Request(string b1 = "确定", string b2 = "", string b3 = "") =>
        new() { Button1 = b1, Button2 = b2, Button3 = b3 };

    [Test]
    public async Task 空按钮被剔除并保证至少一个按钮() {
        await Assert.That(Request("确定", "", "").Buttons.Count).IsEqualTo(1);
        await Assert.That(Request("是", "否", "").Buttons.Count).IsEqualTo(2);
        await Assert.That(Request("是", "否", "取消").Buttons.Count).IsEqualTo(3);
        await Assert.That(Request("", "", "").Buttons[0]).IsEqualTo("确定");
    }

    [Test]
    public async Task 多按钮判定() {
        await Assert.That(Request("确定", "", "").HasMultipleButtons).IsFalse();
        await Assert.That(Request("确定", "取消", "").HasMultipleButtons).IsTrue();
    }

    [Test]
    public async Task 按钮文案两端空白被去掉() {
        var buttons = DialogRequest.NormalizeButtons(" 确定 ", "  取消", " ");
        await Assert.That(buttons[0]).IsEqualTo("确定");
        await Assert.That(buttons[1]).IsEqualTo("取消");
        await Assert.That(buttons.Count).IsEqualTo(2);
    }

    [Test]
    public async Task 队列按入队顺序出队() {
        var queue = new DialogQueue();
        var first = Request();
        var second = Request("是", "否");
        queue.Enqueue(first);
        queue.Enqueue(second);
        await Assert.That(queue.PendingCount).IsEqualTo(2);
        await Assert.That(queue.Dequeue()).IsSameReferenceAs(first);
        await Assert.That(queue.Current).IsSameReferenceAs(first);
    }

    [Test]
    public async Task 已有当前请求时不重复出队() {
        var queue = new DialogQueue();
        queue.Enqueue(Request());
        queue.Dequeue();
        await Assert.That(queue.Dequeue()).IsNull();
    }

    [Test]
    public async Task 完成后自动推进到下一个() {
        var queue = new DialogQueue();
        var first = Request();
        var second = Request("是", "否");
        queue.Enqueue(first);
        queue.Enqueue(second);
        queue.Dequeue();
        var next = queue.Complete();
        await Assert.That(next).IsSameReferenceAs(second);
        await Assert.That(queue.Current).IsSameReferenceAs(second);
        await Assert.That(queue.IsBusy).IsTrue();
    }

    [Test]
    public async Task 清空队列后不再忙碌() {
        var queue = new DialogQueue();
        queue.Enqueue(Request());
        queue.Dequeue();
        queue.Enqueue(Request());
        queue.Clear();
        await Assert.That(queue.IsBusy).IsFalse();
        await Assert.That(queue.Current).IsNull();
    }

    [Test]
    public async Task 结果对象区分主按钮与取消() {
        var primary = new DialogResult { ButtonIndex = 1, Input = "abc" };
        await Assert.That(primary.IsPrimaryButton).IsTrue();
        await Assert.That(primary.Input).IsEqualTo("abc");

        var cancel = DialogResult.Cancel(2);
        await Assert.That(cancel.IsPrimaryButton).IsFalse();
        await Assert.That(cancel.Input).IsNull();
        await Assert.That(cancel.Selection).IsNull();
    }

}
