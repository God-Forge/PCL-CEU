using PCL.Core.Minecraft;

namespace PCL.Core.Tests;

public class LaunchLogServiceTests {

    [Test]
    public async Task 记录并清空启动日志() {
        LaunchLogService.Clear();
        string? lastLine = null;
        var cleared = false;
        void OnLine(string line) => lastLine = line;
        void OnCleared() => cleared = true;
        LaunchLogService.LineWritten += OnLine;
        LaunchLogService.Cleared += OnCleared;
        try {
            LaunchLogService.WriteLine("第一行");
            LaunchLogService.WriteLine("第二行");
            await Assert.That(lastLine).IsEqualTo("第二行");
            await Assert.That(LaunchLogService.Text).Contains("第一行");
            await Assert.That(LaunchLogService.Text).Contains("第二行");

            LaunchLogService.Clear();
            await Assert.That(cleared).IsTrue();
            await Assert.That(LaunchLogService.Text).IsEqualTo("");
        } finally {
            LaunchLogService.LineWritten -= OnLine;
            LaunchLogService.Cleared -= OnCleared;
            LaunchLogService.Clear();
        }
    }

}
