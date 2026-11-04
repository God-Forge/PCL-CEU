using System.Diagnostics;
using PCL.Core.Minecraft;

namespace PCL.Core.Tests;

public class GameProcessWatcherTests {

    private static Process StartEcho(params string[] commands) {
        var info = new ProcessStartInfo("cmd.exe") {
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };
        info.ArgumentList.Add("/c");
        // 末尾停一下，确保监视器能先读到日志再看到进程退出
        info.ArgumentList.Add(string.Join(" & ", commands.Append("ping -n 2 127.0.0.1 >nul")));
        return Process.Start(info) ?? throw new InvalidOperationException("无法启动测试进程");
    }

    private static GameProcessWatcherOptions TestOptions(MinecraftWindow? window = null) => new() {
        ProbeWindows = false,
        PollIntervalMilliseconds = 20,
        ReleaseYear = 2023,
        WindowProbe = window is null ? null : _ => window
    };

    private static Task<MinecraftGameState> WaitForFinalState(GameProcessWatcher watcher, TimeSpan timeout) {
        var completed = new TaskCompletionSource<MinecraftGameState>(TaskCreationOptions.RunContinuationsAsynchronously);
        watcher.StateChanged += (_, state) => {
            if (state is MinecraftGameState.Ended or MinecraftGameState.Crashed or MinecraftGameState.Canceled) {
                completed.TrySetResult(state);
            }
        };
        watcher.Start();
        return completed.Task.WaitAsync(timeout);
    }

    [Test]
    public async Task 捕获日志并在窗口出现后判定正常结束() {
        using var process = StartEcho("echo Setting user: Steve", "echo lwjgl version 3.3.1");
        using var watcher = new GameProcessWatcher(process, TestOptions(new MinecraftWindow((IntPtr)1, "Minecraft 1.20.1", false)));
        var final = await WaitForFinalState(watcher, TimeSpan.FromSeconds(30));
        await Assert.That(final).IsEqualTo(MinecraftGameState.Ended);
        await Assert.That(watcher.LatestLog.Any(line => line.Contains("Setting user: Steve"))).IsTrue();
    }

    [Test]
    public async Task 窗口出现后状态变为运行中() {
        using var process = StartEcho("echo Setting user: Steve");
        using var watcher = new GameProcessWatcher(process, TestOptions(new MinecraftWindow((IntPtr)1, "Minecraft 1.20.1", false)));
        var running = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        watcher.StateChanged += (_, state) => {
            if (state == MinecraftGameState.Running) running.TrySetResult(true);
        };
        watcher.Start();
        var reached = await running.Task.WaitAsync(TimeSpan.FromSeconds(30));
        await Assert.That(reached).IsTrue();
        // 状态先于日志到达，等日志被消费后再断言进度
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (watcher.Progress < MinecraftLaunchProgress.FirstLog && DateTime.UtcNow < deadline) {
            await Task.Delay(20);
        }
        await Assert.That(watcher.Progress >= MinecraftLaunchProgress.FirstLog).IsTrue();
    }

    [Test]
    public async Task 窗口未出现就退出判定为崩溃() {
        using var process = StartEcho("echo Setting user: Steve");
        using var watcher = new GameProcessWatcher(process, TestOptions());
        var final = await WaitForFinalState(watcher, TimeSpan.FromSeconds(30));
        await Assert.That(final).IsEqualTo(MinecraftGameState.Crashed);
    }

    [Test]
    public async Task 日志出现崩溃行立即判定崩溃() {
        using var process = StartEcho("echo This crash report has been saved to: C:\\crash");
        using var watcher = new GameProcessWatcher(process, TestOptions());
        var final = await WaitForFinalState(watcher, TimeSpan.FromSeconds(30));
        await Assert.That(final).IsEqualTo(MinecraftGameState.Crashed);
    }

    [Test]
    public async Task 强制结束标记为取消() {
        using var process = StartEcho("echo Setting user: Steve");
        using var watcher = new GameProcessWatcher(process, TestOptions(new MinecraftWindow((IntPtr)1, "Minecraft 1.20.1", false)));
        var canceled = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        watcher.StateChanged += (_, state) => {
            if (state == MinecraftGameState.Canceled) canceled.TrySetResult(true);
        };
        watcher.Start();
        await Task.Delay(300);
        watcher.Kill();
        await Assert.That(await canceled.Task.WaitAsync(TimeSpan.FromSeconds(30))).IsTrue();
        await Assert.That(watcher.State).IsEqualTo(MinecraftGameState.Canceled);
    }

}
