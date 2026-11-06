using PCL.Core.Launch;

namespace PCL.Core.Tests;

public class LaunchCommandTests {

    private static LaunchCommandContext Context() => new() {
        LauncherPath = @"C:\pcl",
        MinecraftPath = @"C:\mc",
        InstancePath = @"C:\mc\versions\26.3",
        PlayerName = "Steve",
        JavaPath = @"C:\java\bin\java.exe"
    };

    [Test]
    public async Task 展开占位符() {
        var text = LaunchCommandExpander.Expand(
            "echo {user} 在 {version_path} 用 {java}", Context());
        await Assert.That(text).IsEqualTo(@"echo Steve 在 C:\mc\versions\26.3 用 C:\java\bin\java.exe");
    }

    [Test]
    public async Task 不区分大小写() {
        var text = LaunchCommandExpander.Expand("echo {PATH}", Context());
        await Assert.That(text).IsEqualTo(@"echo C:\pcl");
    }

    [Test]
    public async Task 没有占位符时原样返回() {
        await Assert.That(LaunchCommandExpander.Expand("echo hi", Context())).IsEqualTo("echo hi");
        await Assert.That(LaunchCommandExpander.Expand(null, Context())).IsEqualTo("");
        await Assert.That(LaunchCommandExpander.Expand("echo hi", null)).IsEqualTo("echo hi");
    }

    [Test]
    public async Task 未识别的占位符保留() {
        var text = LaunchCommandExpander.Expand("echo {unknown}", Context());
        await Assert.That(text).IsEqualTo("echo {unknown}");
        await Assert.That(LaunchCommandExpander.HasUnresolvedPlaceholder(text)).IsTrue();
    }

    [Test]
    public async Task 执行简单命令并取得退出码() {
        var result = await LaunchCommandRunner.RunAsync("exit 0", Path.GetTempPath(), wait: true);
        await Assert.That(result.Started).IsTrue();
        await Assert.That(result.Waited).IsTrue();
        await Assert.That(result.ExitCode).IsEqualTo(0);
        await Assert.That(result.Success).IsTrue();
    }

    [Test]
    public async Task 非零退出码被记录() {
        var result = await LaunchCommandRunner.RunAsync("exit 3", Path.GetTempPath(), wait: true);
        await Assert.That(result.ExitCode).IsEqualTo(3);
        await Assert.That(result.Success).IsTrue();
    }

    [Test]
    public async Task 空命令不执行() {
        var result = await LaunchCommandRunner.RunAsync("", Path.GetTempPath(), wait: true);
        await Assert.That(result.Started).IsFalse();
        await Assert.That(result.Error).Contains("为空");
    }

    [Test]
    public async Task 不等待时立即返回() {
        var result = await LaunchCommandRunner.RunAsync(
            "ping -n 3 127.0.0.1 > nul", Path.GetTempPath(), wait: false);
        await Assert.That(result.Started).IsTrue();
        await Assert.That(result.Waited).IsFalse();
    }

}
