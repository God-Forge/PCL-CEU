using PCL.Core.Tasks;

namespace PCL.Core.Tests;

public class LoaderTaskTests {

    [Test]
    public async Task LoaderTask_输入输出与进度() {
        var task = new LoaderTask<string, int>("test", loader => {
            loader.Report(0.5);
            loader.Output = loader.Input.Length;
        });
        var output = await task.WaitForExitAsync("abcd");
        await Assert.That(output).IsEqualTo(4);
        await Assert.That(task.State).IsEqualTo(TaskState.Finished);
    }

    [Test]
    public async Task LoaderTask_重载超时与强制重启() {
        var count = 0;
        var task = new LoaderTask<string, int>("test", loader => {
            count++;
            loader.Output = count;
        }) {
            ReloadTimeout = 60000
        };
        await task.WaitForExitAsync("same");
        await task.WaitForExitAsync("same");
        await Assert.That(count).IsEqualTo(1);

        await task.WaitForExitAsync("same", isForceRestart: true);
        await Assert.That(count).IsEqualTo(2);
    }

    [Test]
    public async Task LoaderTask_取消() {
        var task = new LoaderTask<string, int>("test", loader => {
            Thread.Sleep(5000);
            loader.Output = 1;
        });
        task.Start("x");
        task.Cancel();
        await task.WaitForExitAsync();
        await Assert.That(task.State).IsEqualTo(TaskState.Canceled);
    }

    [Test]
    public async Task LoaderTask_失败() {
        var task = new LoaderTask<string, int>("test", loader => throw new InvalidOperationException("boom"));
        var threw = false;
        try {
            await task.WaitForExitAsync("x");
        } catch (InvalidOperationException) {
            threw = true;
        }
        await Assert.That(threw).IsTrue();
        await Assert.That(task.State).IsEqualTo(TaskState.Failed);
    }

    [Test]
    public async Task LoaderCombo_顺序执行() {
        var first = new LoaderTask<int, int>("first", loader => loader.Output = loader.Input + 1);
        var second = new LoaderTask<int, int>("second", loader => loader.Output = loader.Input + 1);
        var combo = new LoaderCombo("combo", new TaskBase[] { first, second });
        combo.Start();
        await combo.WaitForExitAsync();
        await Assert.That(combo.State).IsEqualTo(TaskState.Finished);
        await Assert.That(combo.Progress).IsEqualTo(1d);
    }

}
