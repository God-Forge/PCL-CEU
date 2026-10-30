using PCL.Core.Tasks;

namespace PCL.Core.Tests;

public class TaskTests {

    [Test]
    public async Task ProgressTask_完成并报告进度() {
        var task = new ProgressTask(async (ct, progress) => {
            progress.Report(0.5);
            await Task.Delay(20, ct);
            progress.Report(1);
        });
        var progressValues = new List<double>();
        task.ProgressChanged += (_, args) => progressValues.Add(args.NewProgress);
        task.Start();
        await task.WaitForExitAsync();
        await Assert.That(task.State).IsEqualTo(TaskState.Finished);
        await Assert.That(task.Progress).IsEqualTo(1d);
        await Assert.That(progressValues.Contains(0.5)).IsTrue();
    }

    [Test]
    public async Task ProgressTask_取消() {
        var task = new ProgressTask(async (ct, progress) => {
            await Task.Delay(5000, ct);
        });
        task.Start();
        task.Cancel();
        await task.WaitForExitAsync();
        await Assert.That(task.State).IsEqualTo(TaskState.Canceled);
    }

    [Test]
    public async Task ProgressTask_失败() {
        var task = new ProgressTask((ct, progress) => Task.FromException(new InvalidOperationException("boom")));
        task.Start();
        var threw = false;
        try {
            await task.WaitForExitAsync();
        } catch (InvalidOperationException) {
            threw = true;
        }
        await Assert.That(threw).IsTrue();
        await Assert.That(task.State).IsEqualTo(TaskState.Failed);
        await Assert.That(task.Error).IsNotNull();
    }

    [Test]
    public async Task TaskCombo_顺序执行并汇总进度() {
        var first = new ProgressTask(async (ct, progress) => {
            progress.Report(1);
            await Task.Delay(10, ct);
        });
        var second = new ProgressTask(async (ct, progress) => {
            progress.Report(1);
            await Task.Delay(10, ct);
        });
        var combo = new TaskCombo(new[] { first, second });
        combo.Start();
        await combo.WaitForExitAsync();
        await Assert.That(combo.State).IsEqualTo(TaskState.Finished);
        await Assert.That(combo.Progress).IsEqualTo(1d);
    }

}
