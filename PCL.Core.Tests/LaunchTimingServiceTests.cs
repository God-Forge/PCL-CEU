using PCL.Core.Minecraft;

namespace PCL.Core.Tests;

public class LaunchTimingServiceTests {

    [Test]
    public async Task 记录并保存启动耗时() {
        var path = Path.Combine(Path.GetTempPath(), "pcl-timing-" + Guid.NewGuid().ToString("N") + ".json");
        try {
            LaunchTimingService.Begin("test");
            LaunchTimingService.Mark("Java 进程已启动");
            var record = LaunchTimingService.Finish(path);
            await Assert.That(record).IsNotNull();
            await Assert.That(record!.Marks.ContainsKey("Java 进程已启动")).IsTrue();
            await Assert.That(LaunchTimingService.List(path).Count).IsEqualTo(1);
            await Assert.That(LaunchTimingService.Format(LaunchTimingService.List(path))).Contains("test");
        } finally {
            File.Delete(path);
        }
    }

}
